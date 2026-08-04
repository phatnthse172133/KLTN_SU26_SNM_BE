using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApplicationLayer.AI.V2.Configuration;
using ApplicationLayer.AI.V2.MealPlans;
using ApplicationLayer.AI.V2.Models;
using ApplicationLayer.AI.V2.Recommendations;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.NightMarkets;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.AI.V2.Services;

public sealed class MealPlanV2Service(
    IAiIntentExtractor intentExtractor,
    IMealPlanCandidateRepository candidates,
    IMealPlanV2Repository repository,
    IFoodSemanticMetadataRepository metadata,
    IMealPlanPolicyResolver policies,
    IMealPlanRecalculationService recalculation,
    IMealPlanCartIntegrationService cartIntegration,
    IOptions<MealPlanV2Options> options,
    TimeProvider timeProvider,
    ILogger<MealPlanV2Service> logger) : IMealPlanV2Service
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly MealPlanV2Options _options = options.Value;

    public async Task<ApiResponse<MealPlanV2Response>> CreateAsync(Guid customerId, CreateMealPlanV2Request request, CancellationToken cancellationToken)
    {
        var naturalLanguageRequest = NormalizeNaturalLanguageRequest(request.NaturalLanguageRequest ?? request.Request);
        request.Request = naturalLanguageRequest;
        request.NaturalLanguageRequest = naturalLanguageRequest;
        request.InputLanguage = NormalizeLanguage(request.InputLanguage, true);
        request.ResponseLanguage = NormalizeLanguage(request.ResponseLanguage, false);
        var style = Validate(request);
        var hash = RequestHash(request, style);
        var existing = await repository.FindSessionAsync(customerId, request.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != hash) throw AppException.Conflict("The idempotency key was used with another payload.", "AI_IDEMPOTENCY_CONFLICT");
            return ApiResponse<MealPlanV2Response>.SuccessResponse(ToCreateResponse(existing));
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var catalogs = await metadata.GetActiveCatalogsAsync(cancellationToken);
        var taxonomy = new AiTaxonomyCodes(catalogs.Ingredients.Select(value => value.Code).ToArray(),
            catalogs.Allergens.Select(value => value.Code).ToArray(), catalogs.DietaryAttributes.Select(value => value.Code).ToArray(),
            catalogs.PreparationMethods.Select(value => value.Code).ToArray(), catalogs.TasteProfiles.Select(value => value.Code).ToArray(),
            Enum.GetNames<FoodCourse>(), Enum.GetNames<DiningPurpose>());
        var extraction = naturalLanguageRequest is null
            ? new MealPlanIntentExtractionResult
            {
                IsSuccess = true, UsedFallback = false, ProviderName = "NEUTRAL",
                ParsedResult = new MealPlanIntent
                {
                    Summary = "Kế hoạch cân bằng theo số người và ngân sách.", Confidence = 1,
                    InputLanguageHint = request.InputLanguage, ResponseLanguage = request.ResponseLanguage
                }
            }
            : await intentExtractor.ExtractMealPlanIntentAsync(
                new(naturalLanguageRequest, taxonomy, style.ToString(), request.InputLanguage, request.ResponseLanguage), cancellationToken);
        if (extraction.FailureCategory == AiProviderFailureCategory.CANCELLED) throw new OperationCanceledException(cancellationToken);
        if (!extraction.IsSuccess || extraction.ParsedResult is null)
        {
            if (extraction.ValidationWarnings.Contains("AI_LANGUAGE_PROVIDER_REQUIRED"))
                throw AppException.UnprocessableEntity("This language requires the advanced AI provider.", "AI_LANGUAGE_PROVIDER_REQUIRED");
            throw AppException.UnprocessableEntity("The meal-plan request could not be understood.", "AI_INVALID_REQUEST");
        }
        var extractedIntent = extraction.ParsedResult;
        if (request.PreviousSessionId.HasValue)
        {
            var previousSession = await repository.GetActiveSessionAsync(customerId, request.PreviousSessionId.Value, now, cancellationToken);
            if (previousSession?.ParsedPreferenceJson is not null)
            {
                try
                {
                    var previous = JsonSerializer.Deserialize<MealPlanIntent>(previousSession.ParsedPreferenceJson, Json);
                    if (previous is not null) extractedIntent = MergeFollowUp(previous, extractedIntent);
                }
                catch (JsonException) { logger.LogWarning("MealPlanV2 ignored invalid follow-up context. SessionId={SessionId}", request.PreviousSessionId); }
            }
        }
        var intent = Normalize(extractedIntent, taxonomy, request);
        intent.Provider = extraction.ProviderName ?? (extraction.UsedFallback ? "DETERMINISTIC_FALLBACK" : "UNKNOWN");
        intent.ProviderRuntime = extraction.RuntimeTrace;
        style = ResolveEffectiveStyle(style, intent);
        var policy = policies.Resolve(style);
        var loaded = await candidates.GetCandidatesAsync(now, Math.Clamp(_options.CandidateLimit, 1, 500),
            Math.Clamp(_options.MaximumCandidatesPerMarket, 1, 100), cancellationToken);
        var scoped = request.MarketId.HasValue ? loaded.Where(value => value.MarketId == request.MarketId.Value).ToArray() : loaded.ToArray();
        var eligible = scoped.Where(value => Eligible(value, intent, request, now)).ToArray();
        var plans = Generate(eligible, intent, request, policy, now);
        var warnings = extraction.ValidationWarnings.Concat(intent.Warnings).Distinct(StringComparer.Ordinal).OrderBy(value => value).ToList();
        if (!request.Latitude.HasValue) warnings.Add("LOCATION_NOT_PROVIDED_DISTANCE_UNAVAILABLE");
        if (plans.Count < intent.RequestedPlanCount)
        {
            warnings.Add("FEWER_THAN_REQUESTED_FEASIBLE_PLANS");
            warnings.Add(Limitation(eligible, plans, request, policy));
        }
        if (plans.Count == 0) warnings.Add("AI_NO_FEASIBLE_PLAN");
        LogDiagnostics(customerId, request, naturalLanguageRequest, extraction, intent, loaded, eligible, plans);
        var session = new AiMealPlanSession
        {
            Id = Guid.NewGuid(), CustomerId = customerId, PartySize = request.PartySize, Budget = request.Budget,
            DiningStyle = style.ToString(), OriginalRequest = naturalLanguageRequest, ParsedPreferenceJson = JsonSerializer.Serialize(intent, Json),
            // Coordinates are ephemeral for soft ranking. Retain them only when an explicit
            // radius must remain enforceable for later replacement/regeneration operations.
            Latitude = intent.MaximumDistanceMeters.HasValue ? request.Latitude : null,
            Longitude = intent.MaximumDistanceMeters.HasValue ? request.Longitude : null,
            MaxDistanceMeters = intent.MaximumDistanceMeters,
            IdempotencyKey = request.IdempotencyKey.Trim(), RequestHash = hash, UsedProviderFallback = extraction.UsedFallback,
            WarningsJson = JsonSerializer.Serialize(warnings.Distinct(StringComparer.Ordinal), Json), Status = AiSessionStatus.COMPLETED,
            CreatedAt = now, ExpiresAt = now.AddMinutes(Math.Clamp(_options.SessionLifetimeMinutes, 5, 1440)), Plans = plans
        };
        foreach (var plan in plans) plan.SessionId = session.Id;
        var persisted = await repository.SaveCreateAsync(session, cancellationToken);
        if (persisted.Status == MealPlanIdempotencyStatus.CONFLICT)
            throw AppException.Conflict("The idempotency key was used with another payload.", "AI_IDEMPOTENCY_CONFLICT");
        var loadedSession = await repository.FindSessionAsync(customerId, request.IdempotencyKey.Trim(), cancellationToken)
            ?? throw AppException.ServiceUnavailable("Meal-plan persistence could not be read back.", "AI_MEAL_PLAN_PERSISTENCE_FAILED");
        logger.LogInformation("AI V2 meal plan completed. SessionId={SessionId} CustomerId={CustomerId} Plans={PlanCount} UsedFallback={UsedFallback}",
            loadedSession.Id, customerId, loadedSession.Plans.Count, loadedSession.UsedProviderFallback);
        return ApiResponse<MealPlanV2Response>.SuccessResponse(ToCreateResponse(loadedSession));
    }

    public async Task<ApiResponse<MealPlanDetailResponse>> GetDetailAsync(Guid customerId, Guid planId, CancellationToken cancellationToken)
    {
        var plan = await repository.GetOwnedPlanAsync(customerId, planId, cancellationToken)
            ?? throw AppException.NotFound("Meal plan was not found.", "AI_PLAN_NOT_FOUND");
        return ApiResponse<MealPlanDetailResponse>.SuccessResponse(ToDetail(plan, timeProvider.GetUtcNow().UtcDateTime));
    }

    public async Task<ApiResponse<MealPlanAlternativePageResponse>> GetAlternativesAsync(Guid customerId, Guid planId, Guid itemId,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page <= 0 || pageSize <= 0 || pageSize > Math.Clamp(_options.MaximumAlternativesPageSize, 1, 100))
            throw AppException.BadRequest("Alternative paging is invalid.", "AI_INVALID_REQUEST");
        var plan = await repository.GetOwnedPlanAsync(customerId, planId, cancellationToken)
            ?? throw AppException.NotFound("Meal plan was not found.", "AI_PLAN_NOT_FOUND");
        EnsureEditable(plan, timeProvider.GetUtcNow().UtcDateTime);
        var item = plan.Items.SingleOrDefault(value => value.Id == itemId && !value.IsRemoved)
            ?? throw AppException.NotFound("Meal-plan item was not found.", "AI_PLAN_ITEM_NOT_FOUND");
        var all = await AlternativeCandidates(plan, item, cancellationToken);
        var projected = all.Select(value => Alternative(plan, item, value)).Where(value => value.ProjectedRemainingBudget >= 0).ToArray();
        return ApiResponse<MealPlanAlternativePageResponse>.SuccessResponse(new()
        {
            Page = page, PageSize = pageSize, Total = projected.Length, CurrentPlanVersion = plan.Version,
            Items = projected.Skip((page - 1) * pageSize).Take(pageSize).ToArray()
        });
    }

    public async Task<ApiResponse<MealPlanDetailResponse>> ReplaceAsync(Guid customerId, Guid planId, Guid itemId,
        ReplaceMealPlanItemRequest request, CancellationToken cancellationToken)
    {
        if (request.ReplacementFoodId == Guid.Empty) throw AppException.BadRequest("Replacement food is required.", "AI_REPLACEMENT_INVALID");
        await using var mutation = await repository.BeginOwnedMutationAsync(customerId, planId, cancellationToken)
            ?? throw AppException.NotFound("Meal plan was not found.", "AI_PLAN_NOT_FOUND");
        var plan = mutation.Plan; var now = timeProvider.GetUtcNow().UtcDateTime;
        EnsureMutation(plan, request.ExpectedPlanVersion, now);
        var item = plan.Items.SingleOrDefault(value => value.Id == itemId && !value.IsRemoved)
            ?? throw AppException.NotFound("Meal-plan item was not found.", "AI_PLAN_ITEM_NOT_FOUND");
        var replacement = (await AlternativeCandidates(plan, item, cancellationToken)).SingleOrDefault(value => value.FoodId == request.ReplacementFoodId)
            ?? throw AppException.UnprocessableEntity("Replacement food is not a valid same-market alternative.", "AI_REPLACEMENT_INVALID");
        if (plan.TotalPrice - item.TotalPriceSnapshot + replacement.CurrentPrice * item.Quantity > plan.Session.Budget)
            throw AppException.UnprocessableEntity("Replacement would exceed the plan budget.", "AI_BUDGET_EXCEEDED");
        var parsedIntent = JsonSerializer.Deserialize<MealPlanIntent>(plan.Session.ParsedPreferenceJson ?? "{}", Json) ?? new();
        var newItem = Item(replacement, item.Course, item.Quantity, plan.Items.Max(value => value.SortOrder), now, Compatibility(replacement, parsedIntent));
        item.MarkRemoved(now); plan.AddItem(newItem, replacement.MarketId, plan.Session.Budget, false);
        var score = recalculation.Recalculate(plan, policies.Resolve(ParseStyle(plan.Session.DiningStyle)), plan.Session.PartySize, plan.Session.Budget, now);
        if (!plan.IsComplete) throw AppException.UnprocessableEntity("Replacement would make serving or required courses incomplete.", "AI_SERVING_INSUFFICIENT");
        await mutation.CommitAsync(cancellationToken);
        var refreshed = await repository.GetOwnedPlanAsync(customerId, planId, cancellationToken) ?? plan;
        return ApiResponse<MealPlanDetailResponse>.SuccessResponse(ToDetail(refreshed, now));
    }

    public async Task<ApiResponse<MealPlanDetailResponse>> RemoveAsync(Guid customerId, Guid planId, Guid itemId,
        int expectedPlanVersion, CancellationToken cancellationToken)
    {
        await using var mutation = await repository.BeginOwnedMutationAsync(customerId, planId, cancellationToken)
            ?? throw AppException.NotFound("Meal plan was not found.", "AI_PLAN_NOT_FOUND");
        var plan = mutation.Plan; var now = timeProvider.GetUtcNow().UtcDateTime;
        EnsureMutation(plan, expectedPlanVersion, now);
        var item = plan.Items.SingleOrDefault(value => value.Id == itemId && !value.IsRemoved)
            ?? throw AppException.NotFound("Meal-plan item was not found.", "AI_PLAN_ITEM_NOT_FOUND");
        item.MarkRemoved(now);
        recalculation.Recalculate(plan, policies.Resolve(ParseStyle(plan.Session.DiningStyle)), plan.Session.PartySize, plan.Session.Budget, now);
        await mutation.CommitAsync(cancellationToken);
        var refreshed = await repository.GetOwnedPlanAsync(customerId, planId, cancellationToken) ?? plan;
        return ApiResponse<MealPlanDetailResponse>.SuccessResponse(ToDetail(refreshed, now));
    }

    public async Task<ApiResponse<MealPlanDetailResponse>> RegenerateCourseAsync(Guid customerId, Guid planId, FoodCourse course,
        RegenerateMealPlanCourseRequest request, CancellationToken cancellationToken)
    {
        await using var mutation = await repository.BeginOwnedMutationAsync(customerId, planId, cancellationToken)
            ?? throw AppException.NotFound("Meal plan was not found.", "AI_PLAN_NOT_FOUND");
        var plan = mutation.Plan; var now = timeProvider.GetUtcNow().UtcDateTime;
        EnsureMutation(plan, request.ExpectedPlanVersion, now);
        var courseItems = plan.Items.Where(value => !value.IsRemoved && value.Course == course).ToArray();
        if (courseItems.Length == 0) throw AppException.UnprocessableEntity("The requested course is not present.", "AI_COURSE_NOT_SUPPORTED");
        var alternatives = await AlternativeCandidates(plan, courseItems[0], cancellationToken);
        var quantity = courseItems.Sum(value => value.Quantity);
        var removedTotal = courseItems.Sum(value => value.TotalPriceSnapshot);
        var candidate = alternatives.FirstOrDefault(value => courseItems.All(item => item.FoodItemId != value.FoodId)
            && plan.TotalPrice - removedTotal + value.CurrentPrice * quantity <= plan.Session.Budget);
        if (candidate is null) throw AppException.UnprocessableEntity("No valid alternative exists for this course.", "AI_NO_ALTERNATIVE");
        foreach (var old in courseItems) old.MarkRemoved(now);
        var parsedIntent = JsonSerializer.Deserialize<MealPlanIntent>(plan.Session.ParsedPreferenceJson ?? "{}", Json) ?? new();
        plan.AddItem(Item(candidate, course, quantity, plan.Items.Max(value => value.SortOrder), now, Compatibility(candidate, parsedIntent)),
            candidate.MarketId, plan.Session.Budget, false);
        recalculation.Recalculate(plan, policies.Resolve(ParseStyle(plan.Session.DiningStyle)), plan.Session.PartySize, plan.Session.Budget, now);
        if (!plan.IsComplete) throw AppException.UnprocessableEntity("No alternative preserves plan feasibility.", "AI_NO_ALTERNATIVE");
        await mutation.CommitAsync(cancellationToken);
        var refreshed = await repository.GetOwnedPlanAsync(customerId, planId, cancellationToken) ?? plan;
        return ApiResponse<MealPlanDetailResponse>.SuccessResponse(ToDetail(refreshed, now));
    }

    public async Task<ApiResponse<MealPlanAddToCartResponse>> AddToCartAsync(
        Guid customerId, Guid planId, AddMealPlanToCartRequest request, CancellationToken cancellationToken)
    {
        var key = request.IdempotencyKey?.Trim() ?? string.Empty;
        if (key.Length is < 1 or > 100)
            throw AppException.BadRequest("Idempotency key is required and must not exceed 100 characters.", "AI_IDEMPOTENCY_KEY_REQUIRED");

        await using var mutation = await repository.BeginOwnedMutationAsync(customerId, planId, cancellationToken)
            ?? throw AppException.NotFound("Meal plan was not found.", "AI_PLAN_NOT_FOUND");
        var plan = mutation.Plan;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var requestHash = CartRequestHash(planId, request.ExpectedPlanVersion);
        var existing = await mutation.FindCartOperationAsync(customerId, key, cancellationToken);
        if (existing is not null)
        {
            if (existing.PlanId != planId || existing.PlanVersion != request.ExpectedPlanVersion || existing.RequestHash != requestHash)
                throw AppException.Conflict("The idempotency key was used for another meal plan or version.", "AI_IDEMPOTENCY_CONFLICT");
            var replay = JsonSerializer.Deserialize<MealPlanAddToCartResponse>(existing.ResponseJson, Json)
                ?? throw new InvalidOperationException("Stored meal-plan cart response is invalid.");
            replay.IdempotencyResult = "REPLAYED";
            return ApiResponse<MealPlanAddToCartResponse>.SuccessResponse(replay, "The existing cart operation was returned.");
        }

        EnsureMutation(plan, request.ExpectedPlanVersion, now);
        if (!plan.IsComplete || plan.Status is AiMealPlanStatus.DRAFT or AiMealPlanStatus.FAILED or AiMealPlanStatus.EXPIRED)
            throw AppException.UnprocessableEntity("Meal plan is not complete.", "AI_PLAN_INCOMPLETE");

        var active = plan.Items.Where(item => !item.IsRemoved).OrderBy(item => item.SortOrder).ToArray();
        if (active.Length == 0)
            throw AppException.UnprocessableEntity("Meal plan has no active items.", "AI_PLAN_INCOMPLETE");

        var unavailable = active.Select(item =>
        {
            var result = item.FoodItem is null
                ? new CustomerOrderabilityResult(false, "FOOD_NOT_FOUND")
                : CustomerOrderability.Evaluate(item.FoodItem, now);
            return (Item: item, Result: result);
        }).Where(value => !value.Result.CanOrder).Select(value => new MealPlanUnavailableItem
        {
            PlanItemId = value.Item.Id, FoodId = value.Item.FoodItemId, Course = value.Item.Course.ToString(),
            Reason = value.Result.ReasonCode ?? "FOOD_UNAVAILABLE", CanRequestAlternatives = value.Item.FoodItemId.HasValue
        }).ToArray();
        if (unavailable.Length > 0)
            throw AppException.UnprocessableEntity("One or more meal-plan items are unavailable.", "AI_ITEMS_UNAVAILABLE",
                new MealPlanUnavailableDetails { Items = unavailable });

        var changed = active.Select(item => new
        {
            Item = item,
            Current = FoodPriceResolver.GetCurrentPrice(item.FoodItem!, now)
        }).Where(value => value.Current != value.Item.UnitPriceSnapshot).Select(value => new MealPlanChangedPriceItem
        {
            PlanItemId = value.Item.Id, FoodId = value.Item.FoodItemId!.Value, FoodName = value.Item.FoodNameSnapshot,
            OldUnitPrice = value.Item.UnitPriceSnapshot, NewUnitPrice = value.Current, Quantity = value.Item.Quantity
        }).ToArray();
        if (changed.Length > 0)
            throw AppException.Conflict("Meal-plan prices have changed.", "AI_PRICE_CHANGED", new MealPlanPriceChangeDetails
            {
                PlanId = plan.Id, ExpectedPlanVersion = request.ExpectedPlanVersion, OldTotal = plan.TotalPrice,
                NewTotal = active.Sum(item => FoodPriceResolver.GetCurrentPrice(item.FoodItem!, now) * item.Quantity),
                ChangedItems = changed
            });

        var batch = await cartIntegration.AddItemsAsync(customerId,
            active.Select(item => (item.FoodItemId!.Value, item.Quantity)).ToArray(), cancellationToken);
        var response = new MealPlanAddToCartResponse
        {
            PlanId = plan.Id, PlanVersion = plan.Version, Cart = batch.Cart,
            AddedFoodItemIds = batch.AddedFoodItemIds, MergedFoodItemIds = batch.MergedFoodItemIds,
            IdempotencyResult = "CREATED"
        };
        mutation.AddCartOperation(new AiMealPlanCartOperation
        {
            Id = Guid.NewGuid(), CustomerId = customerId, PlanId = plan.Id, PlanVersion = plan.Version,
            IdempotencyKey = key, RequestHash = requestHash, ResponseJson = JsonSerializer.Serialize(response, Json), CreatedAt = now
        });
        await mutation.CommitAsync(cancellationToken);
        return ApiResponse<MealPlanAddToCartResponse>.SuccessResponse(response, "Meal plan added to cart.");
    }

    public async Task<ApiResponse<MealPlanDetailResponse>> RefreshPricesAsync(
        Guid customerId, Guid planId, RefreshMealPlanPricesRequest request, CancellationToken cancellationToken)
    {
        await using var mutation = await repository.BeginOwnedMutationAsync(customerId, planId, cancellationToken)
            ?? throw AppException.NotFound("Meal plan was not found.", "AI_PLAN_NOT_FOUND");
        var plan = mutation.Plan;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        EnsureMutation(plan, request.ExpectedPlanVersion, now);
        var active = plan.Items.Where(item => !item.IsRemoved).ToArray();
        if (active.Any(item => item.FoodItem is null || !CustomerOrderability.Evaluate(item.FoodItem, now).CanOrder))
            throw AppException.UnprocessableEntity("Unavailable items must be replaced before refreshing prices.", "AI_ITEMS_UNAVAILABLE");
        var total = active.Sum(item => FoodPriceResolver.GetCurrentPrice(item.FoodItem!, now) * item.Quantity);
        if (total > plan.Session.Budget)
            throw AppException.UnprocessableEntity("Updated prices exceed the meal-plan budget.", "AI_PRICE_REFRESH_EXCEEDS_BUDGET");
        foreach (var item in active)
        {
            item.UnitPriceSnapshot = FoodPriceResolver.GetCurrentPrice(item.FoodItem!, now);
            item.TotalPriceSnapshot = item.UnitPriceSnapshot * item.Quantity;
            item.UpdatedAt = now;
        }
        recalculation.Recalculate(plan, policies.Resolve(ParseStyle(plan.Session.DiningStyle)), plan.Session.PartySize,
            plan.Session.Budget, now);
        await mutation.CommitAsync(cancellationToken);
        return ApiResponse<MealPlanDetailResponse>.SuccessResponse(ToDetail(plan, now));
    }

    private List<AiMealPlan> Generate(IReadOnlyCollection<FoodRecommendationCandidate> eligible, MealPlanIntent intent,
        CreateMealPlanV2Request request, MealPlanStylePolicy policy, DateTime now)
    {
        var requested = Math.Clamp(request.RequestedPlanCount, 1, Math.Clamp(_options.MaximumPlans, 1, 3));
        var strategies = new[] { MealPlanStrategy.BEST_MATCH, MealPlanStrategy.BUDGET_FRIENDLY, MealPlanStrategy.DIVERSE };
        var selected = new List<AiMealPlan>();
        var usedFoods = new HashSet<Guid>();
        foreach (var strategy in strategies.Take(requested))
        {
            var pool = eligible.GroupBy(value => value.MarketId)
                .Select(group => BuildMarket(group.ToArray(), intent, request, policy, strategy, usedFoods, now))
                .Where(value => value is not null).Cast<AiMealPlan>()
                .Where(value => selected.All(previous => PlanOverlap(previous, value) < _options.MaximumPlanOverlap
                    && MeaningfullyDifferent(previous, value)))
                .ToArray();
            var next = strategy switch
            {
                MealPlanStrategy.BUDGET_FRIENDLY => pool.OrderByDescending(value => PlanRankingScore(value, request, intent, usedFoods, 12m))
                    .ThenBy(value => value.TotalPrice).FirstOrDefault(),
                MealPlanStrategy.DIVERSE => pool.OrderByDescending(value => PlanRankingScore(value, request, intent, usedFoods, 20m))
                    .ThenBy(value => value.Items.Count(item => !item.IsRemoved && usedFoods.Contains(item.FoodItemId!.Value)))
                    .ThenByDescending(value => value.Items.Where(item => !item.IsRemoved).Select(item => item.BoothId).Distinct().Count())
                    .ThenByDescending(value => value.CompatibilityScore).FirstOrDefault(),
                _ => pool.OrderByDescending(value => PlanRankingScore(value, request, intent, usedFoods, 8m))
                    .ThenByDescending(value => value.CompatibilityScore).FirstOrDefault()
            };
            if (next is null) continue;
            next.PlanCode = ((char)('A' + selected.Count)).ToString();
            next.PlanTitle = strategy switch
            {
                MealPlanStrategy.BUDGET_FRIENDLY => "Tiết kiệm hợp lý",
                MealPlanStrategy.DIVERSE => "Lựa chọn đa dạng",
                _ => next.CompatibilityScore < 70 ? "Lựa chọn gần nhất" : "Phù hợp nhất"
            };
            selected.Add(next);
            foreach (var id in ActiveFoodIds(next)) usedFoods.Add(id);
        }
        return selected;
    }

    private static decimal PlanRankingScore(AiMealPlan plan, CreateMealPlanV2Request request, MealPlanIntent intent,
        IReadOnlySet<Guid> usedFoods, decimal diversityWeight)
    {
        var compatibility = plan.CompatibilityScore * .70m;
        var budgetTarget = request.Budget * .80m;
        var budget = budgetTarget <= 0 ? 0 : Math.Clamp(1m - Math.Abs(plan.TotalPrice - budgetTarget) / budgetTarget, 0m, 1m) * 10m;
        var active = ActiveFoodIds(plan).ToArray();
        var diversity = active.Length == 0 ? 0 : active.Count(id => !usedFoods.Contains(id)) / (decimal)active.Length * diversityWeight;
        return compatibility + budget + diversity + DistanceContribution(plan.DistanceMeters, request.UseDistanceRanking, intent.PreferNearMe, intent.MaximumDistanceMeters);
    }

    private static decimal DistanceContribution(int? distanceMeters, bool enabled, bool explicitlyNear, int? maximumDistanceMeters)
    {
        if (!enabled || !distanceMeters.HasValue) return 0m;
        var scale = maximumDistanceMeters.GetValueOrDefault(20_000);
        var weight = explicitlyNear ? 15m : 8m;
        return Math.Round(Math.Clamp(1m - distanceMeters.Value / (decimal)Math.Max(scale, 1), 0m, 1m) * weight, 2, MidpointRounding.AwayFromZero);
    }

    private AiMealPlan? BuildMarket(FoodRecommendationCandidate[] values, MealPlanIntent intent, CreateMealPlanV2Request request,
        MealPlanStylePolicy policy, MealPlanStrategy strategy, IReadOnlySet<Guid> usedFoods, DateTime now)
    {
        var scored = values.Where(value => value.Courses.Count > 0 && value.EstimatedServingCount.HasValue)
            .Select(value => new Scored(value, Compatibility(value, intent))).ToArray();
        var main = scored.Where(value => value.Value.Courses.Contains(FoodCourse.MAIN_COURSE)).Take(Math.Clamp(_options.MaximumCandidatesPerCourse, 1, 30)).ToArray();
        if (main.Length == 0) return null;
        var requiredServing = (int)Math.Ceiling(request.PartySize * policy.ServingMultiplier);
        var selected = new List<(Scored Value, FoodCourse Course, int Quantity)>();
        var primaryPool = intent.IsShareablePreferred == true
            ? main.Where(candidate => values.Any(shared => shared.IsShareable == true && shared.EstimatedServingCount.HasValue
                && shared.Courses.Contains(FoodCourse.SHARED_DISH)
                && candidate.Value.CurrentPrice * Math.Ceiling(requiredServing / (decimal)candidate.Value.EstimatedServingCount!.Value)
                    + shared.CurrentPrice * Math.Ceiling(requiredServing / (decimal)shared.EstimatedServingCount.Value) <= request.Budget)).ToArray()
            : main;
        if (primaryPool.Length == 0) primaryPool = main;
        var primary = strategy switch
        {
            MealPlanStrategy.BUDGET_FRIENDLY => primaryPool.OrderBy(value => value.Value.CurrentPrice / value.Value.EstimatedServingCount!.Value)
                .ThenByDescending(value => value.Score).ThenBy(value => value.Value.FoodId).First(),
            MealPlanStrategy.DIVERSE => primaryPool.OrderBy(value => usedFoods.Contains(value.Value.FoodId))
                .ThenByDescending(value => value.Score).ThenBy(value => value.Value.CurrentPrice).ThenBy(value => value.Value.FoodId).First(),
            _ => primaryPool.OrderByDescending(value => value.Score).ThenByDescending(value => value.Value.Rating ?? 0)
                .ThenByDescending(value => value.Value.CurrentPrice).ThenBy(value => value.Value.FoodId).First()
        };
        var quantity = (int)Math.Ceiling(requiredServing / (decimal)primary.Value.EstimatedServingCount!.Value);
        if (primary.Value.CurrentPrice * quantity > request.Budget) return null;
        selected.Add((primary, FoodCourse.MAIN_COURSE, quantity));
        var targetRatio = strategy switch
        {
            MealPlanStrategy.BUDGET_FRIENDLY => .45m,
            MealPlanStrategy.DIVERSE => .80m,
            _ => .75m
        };
        var optional = scored.Where(value => value.Value.FoodId != primary.Value.FoodId)
            .Where(value => value.Value.Courses.Any(course => policy.OptionalCourses.Contains(course)))
            .OrderBy(value => strategy == MealPlanStrategy.DIVERSE && usedFoods.Contains(value.Value.FoodId))
            .ThenByDescending(value => value.Score)
            .ThenBy(value => strategy == MealPlanStrategy.BUDGET_FRIENDLY ? value.Value.CurrentPrice : 0).ThenBy(value => value.Value.FoodId);
        foreach (var candidate in optional)
        {
            var hasMinimum = selected.Count >= policy.MinimumFoods
                && selected.Select(value => value.Value.Value.BoothId).Distinct().Count() >= policy.MinimumBooths
                && (intent.IsShareablePreferred != true || selected.Any(value => value.Value.Value.IsShareable == true));
            var total = selected.Sum(value => value.Value.Value.CurrentPrice * value.Quantity);
            if (hasMinimum && (selected.Count >= policy.MaximumFoods || total >= request.Budget * targetRatio)) break;
            var course = candidate.Value.Courses.First(value => policy.OptionalCourses.Contains(value));
            var optionalQuantity = course switch
            {
                FoodCourse.SHARED_DISH => (int)Math.Ceiling(requiredServing / (decimal)candidate.Value.EstimatedServingCount!.Value),
                FoodCourse.DRINK => (int)Math.Ceiling(request.PartySize / (decimal)candidate.Value.EstimatedServingCount!.Value),
                _ => 1
            };
            if (total + candidate.Value.CurrentPrice * optionalQuantity > request.Budget) continue;
            selected.Add((candidate, course, optionalQuantity));
            if (selected.Count >= policy.MaximumFoods) break;
        }
        if (selected.Count < policy.MinimumFoods || selected.Select(value => value.Value.Value.BoothId).Distinct().Count() < policy.MinimumBooths
            || intent.IsShareablePreferred == true && !selected.Any(value => value.Value.Value.IsShareable == true)) return null;
        var distance = CalculateDistance(request.Latitude, request.Longitude, values[0].MarketLatitude, values[0].MarketLongitude);
        var plan = new AiMealPlan { Id = Guid.NewGuid(), MarketId = values[0].MarketId, PlanCode = "A", PlanTitle = "Meal plan",
            Strategy = strategy.ToString(), DistanceMeters = distance, Status = AiMealPlanStatus.READY,
            Summary = $"{selected.Count} món, đủ {requiredServing} khẩu phần tại {values[0].MarketName}", CreatedAt = now, UpdatedAt = now };
        var order = 0;
        foreach (var value in selected) plan.AddItem(Item(value.Value.Value, value.Course, value.Quantity, ++order, now, value.Value.Score, intent), value.Value.Value.MarketId, request.Budget, false);
        recalculation.Recalculate(plan, policy, request.PartySize, request.Budget, now);
        return plan.IsComplete ? plan : null;
    }

    private async Task<IReadOnlyCollection<FoodRecommendationCandidate>> AlternativeCandidates(AiMealPlan plan, AiMealPlanItem item, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var intent = JsonSerializer.Deserialize<MealPlanIntent>(plan.Session.ParsedPreferenceJson ?? "{}", Json) ?? new();
        var request = new CreateMealPlanV2Request { PartySize = plan.Session.PartySize, Budget = plan.Session.Budget,
            Latitude = plan.Session.Latitude, Longitude = plan.Session.Longitude, MaxDistanceMeters = plan.Session.MaxDistanceMeters };
        var loaded = await candidates.GetCandidatesAsync(now, Math.Clamp(_options.CandidateLimit, 1, 500),
            Math.Clamp(_options.MaximumCandidatesPerMarket, 1, 100), cancellationToken);
        var excluded = plan.Items.Select(value => value.FoodItemId).Where(value => value.HasValue).Select(value => value!.Value).ToHashSet();
        return loaded.Where(value => value.MarketId == plan.MarketId && value.Courses.Contains(item.Course)
            && !excluded.Contains(value.FoodId) && value.EstimatedServingCount.HasValue && Eligible(value, intent, request, now))
            .OrderByDescending(value => Compatibility(value, intent)).ThenBy(value => value.CurrentPrice).ThenBy(value => value.FoodId).ToArray();
    }

    private MealPlanAlternativeResponse Alternative(AiMealPlan plan, AiMealPlanItem old, FoodRecommendationCandidate value)
    {
        var total = plan.TotalPrice - old.TotalPriceSnapshot + value.CurrentPrice * old.Quantity;
        var serving = plan.EstimatedServingCount.HasValue && old.ServingCountSnapshot.HasValue && value.EstimatedServingCount.HasValue
            ? plan.EstimatedServingCount - old.ServingCountSnapshot + value.EstimatedServingCount * old.Quantity : null;
        var policy = policies.Resolve(ParseStyle(plan.Session.DiningStyle));
        return new() { FoodId = value.FoodId, FoodName = value.FoodName, ImageUrl = value.ImageUrl,
            Booth = new() { Id = value.BoothId, Name = value.BoothName }, Course = old.Course.ToString(), CurrentPrice = value.CurrentPrice,
            CompatibilityScore = Compatibility(value, JsonSerializer.Deserialize<MealPlanIntent>(plan.Session.ParsedPreferenceJson ?? "{}", Json) ?? new()),
            Reason = "Cùng market, cùng course và đáp ứng các ràng buộc hiện tại.", ProjectedTotalPrice = total,
            ProjectedRemainingBudget = plan.Session.Budget - total, ProjectedServingCount = serving,
            WillKeepPlanComplete = serving.HasValue && serving >= Math.Ceiling(plan.Session.PartySize * policy.ServingMultiplier), CurrentPlanVersion = plan.Version };
    }

    private static AiMealPlanItem Item(FoodRecommendationCandidate value, FoodCourse course, int quantity, int sort, DateTime now,
        decimal compatibilityScore, MealPlanIntent? intent = null)
        => new() { Id = Guid.NewGuid(), FoodItemId = value.FoodId, BoothId = value.BoothId, FoodNameSnapshot = value.FoodName,
            BoothNameSnapshot = value.BoothName, ImageUrlSnapshot = value.ImageUrl, Course = course, Quantity = quantity,
            UnitPriceSnapshot = value.CurrentPrice, TotalPriceSnapshot = value.CurrentPrice * quantity,
            ServingCountSnapshot = value.EstimatedServingCount * quantity, RatingSnapshot = value.Rating,
            ReviewCountSnapshot = value.ReviewCount, CompatibilityScore = compatibilityScore, Reason = EvidenceReason(value, intent, quantity),
            SortOrder = sort, CreatedAt = now, UpdatedAt = now };

    private static string EvidenceReason(FoodRecommendationCandidate value, MealPlanIntent? intent, int quantity)
    {
        var evidence = new List<string>();
        if (value.EstimatedServingCount.HasValue)
            evidence.Add($"đáp ứng {value.EstimatedServingCount.Value * quantity} khẩu phần");
        if (intent is not null && CodesIntersect(intent.PreparationMethodCodes, value.PreparationMethodCodes))
            evidence.Add("đúng cách chế biến bạn muốn");
        if (intent is not null && CodesIntersect(intent.PreferredTasteCodes, value.TasteCodes))
            evidence.Add("hợp khẩu vị đã chọn");
        if (intent is not null && intent.DietaryRequirementCodes.Count > 0)
            evidence.Add("đáp ứng chế độ ăn theo dữ liệu món");
        if (value.IsShareable == true) evidence.Add("phù hợp để dùng chung");
        return evidence.Count == 0 ? "Đang bán, có giá hợp lệ và nằm trong ngân sách kế hoạch."
            : char.ToUpperInvariant(evidence[0][0]) + evidence[0][1..] + (evidence.Count > 1 ? ", " + string.Join(", ", evidence.Skip(1)) : string.Empty) + ".";
    }

    private static decimal Compatibility(FoodRecommendationCandidate value, MealPlanIntent intent)
    {
        var signals = intent.PreferredIngredientCodes.Count + intent.PreferredTasteCodes.Count + intent.PreparationMethodCodes.Count
            + intent.PreferredCourseCodes.Count + intent.MealPurposeCodes.Count + intent.PreferredServingTemperatures.Count
            + (intent.IsShareablePreferred.HasValue ? 1 : 0);
        var matches = CountCodeMatches(intent.PreferredIngredientCodes, value.IngredientCodes)
            + CountCodeMatches(intent.PreferredTasteCodes, value.TasteCodes)
            + CountCodeMatches(intent.PreparationMethodCodes, value.PreparationMethodCodes)
            + intent.PreferredCourseCodes.Intersect(value.Courses.Select(course => course.ToString())).Count()
            + intent.MealPurposeCodes.Intersect(value.DiningPurposes.Select(purpose => purpose.ToString())).Count()
            + (value.ServingTemperature.HasValue && intent.PreferredServingTemperatures.Contains(value.ServingTemperature.Value) ? 1 : 0)
            + (intent.IsShareablePreferred.HasValue && value.IsShareable == intent.IsShareablePreferred ? 1 : 0);
        var relevance = signals == 0 ? 55m : 35m + 55m * matches / signals;
        var rating = value.Rating.HasValue && value.ReviewCount > 0 ? value.Rating.Value : 0;
        return Math.Round(Math.Clamp(relevance + rating * 2m, 0, 100), 2, MidpointRounding.AwayFromZero);
    }

    private static int CountCodeMatches(IEnumerable<string> requested, IEnumerable<string> actual)
    {
        var actualCodes = actual.Select(NormalizeSemanticCode).ToHashSet(StringComparer.Ordinal);
        return requested.Select(NormalizeSemanticCode).Distinct(StringComparer.Ordinal).Count(actualCodes.Contains);
    }

    private static bool CodesIntersect(IEnumerable<string> left, IEnumerable<string> right)
    {
        var codes = right.Select(NormalizeSemanticCode).ToHashSet(StringComparer.Ordinal);
        return left.Select(NormalizeSemanticCode).Any(codes.Contains);
    }

    private static bool CodesEqual(string left, string right) => NormalizeSemanticCode(left) == NormalizeSemanticCode(right);
    private static string NormalizeSemanticCode(string value)
    {
        var code = value.Trim().ToUpperInvariant();
        foreach (var prefix in new[] { "ING_", "METHOD_", "TASTE_", "DIET_", "ALLERGEN_" })
            if (code.StartsWith(prefix, StringComparison.Ordinal)) code = code[prefix.Length..];
        return code is "FISH" or "SHRIMP" or "SQUID" or "CRAB" or "SHELLFISH" ? "SEAFOOD" : code;
    }

    private static bool SatisfiesDietary(FoodRecommendationCandidate value, string required)
    {
        if (value.DietaryAttributes.Any(attribute => CodesEqual(attribute.Code, required) && attribute.IsConfirmed
            && attribute.Status == DietarySuitabilityStatus.SUITABLE)) return true;
        var normalized = NormalizeSemanticCode(required);
        if (normalized is not ("VEGETARIAN" or "VEGAN") || value.IngredientCodes.Count == 0) return false;
        var animalIngredients = new HashSet<string>(["BEEF", "CHICKEN", "PORK", "SEAFOOD", "MEAT"], StringComparer.Ordinal);
        if (normalized == "VEGAN") animalIngredients.UnionWith(["EGG", "MILK", "CHEESE", "DAIRY"]);
        var ingredients = value.IngredientCodes.Select(NormalizeSemanticCode).ToArray();
        var positivePlantEvidence = ingredients.Any(code => code is "VEGETABLE" or "TOFU" or "MUSHROOM" or "PLANT_BASED");
        return positivePlantEvidence && !ingredients.Any(animalIngredients.Contains);
    }

    private static bool Eligible(FoodRecommendationCandidate value, MealPlanIntent intent, CreateMealPlanV2Request request, DateTime now)
    {
        if (!value.IsAvailable || value.IsDeleted || value.CategoryDeleted || !value.CategoryIsActive || !value.CategoryIsSelectable
            || value.CurrentPrice <= 0 || value.BoothStatus != BoothStatus.Active || value.MarketDeleted
            || value.MarketModerationStatus != ModerationStatus.Active || value.MarketStatus != NightMarketStatus.Active) return false;
        var local = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(now));
        if (!CustomerAvailability.IsOpenNow(true, value.MarketOpenTime, value.MarketCloseTime, value.BoothOpenTime, value.BoothCloseTime, local)) return false;
        if (CodesIntersect(intent.ExcludedIngredientCodes, value.IngredientCodes)) return false;
        if (CodesIntersect(intent.AvoidedPreparationMethodCodes, value.PreparationMethodCodes)) return false;
        if (CodesIntersect(intent.AvoidedTasteCodes, value.TasteCodes)) return false;
        if (intent.DietaryRequirementCodes.Any(required => !SatisfiesDietary(value, required))) return false;
        if (intent.AllergenExclusionCodes.Any(excluded => value.Allergens.Any(allergen => CodesEqual(allergen.Code, excluded) && allergen.IsConfirmed))) return false;
        if (request.Latitude.HasValue && intent.MaximumDistanceMeters.HasValue)
        {
            var distance = CalculateDistance(request.Latitude, request.Longitude, value.MarketLatitude, value.MarketLongitude);
            if (!distance.HasValue || distance > intent.MaximumDistanceMeters) return false;
        }
        return true;
    }

    private MealPlanDiningStyle Validate(CreateMealPlanV2Request request)
    {
        if (request.PartySize <= 0 || request.PartySize > Math.Clamp(_options.MaximumPartySize, 1, 100)
            || request.Budget <= 0 || request.Budget > _options.MaximumBudget
            || request.Request?.Length > Math.Clamp(_options.MaximumRequestCharacters, 1, 4000)
            || request.RequestedPlanCount is < 1 or > 3
            || request.Scope?.Trim().Length > 100
            || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Trim().Length > 100
            || !Enum.TryParse<MealPlanDiningStyle>(request.DiningStyle?.Trim(), false, out var style))
            throw AppException.BadRequest("Meal-plan request is invalid.", "AI_INVALID_REQUEST");
        if (request.Latitude.HasValue != request.Longitude.HasValue || request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
            throw AppException.BadRequest("Location is invalid.", "AI_INVALID_LOCATION");
        request.MaxDistanceMeters ??= request.MaximumDistanceMeters;
        if (request.MaxDistanceMeters is <= 0 || request.MaxDistanceMeters > Math.Clamp(_options.MaximumDistanceMeters, 1, 500_000))
            throw AppException.BadRequest("Maximum distance is invalid.", "AI_INVALID_LOCATION");
        if (request.LocationAccuracyMeters is <= 0 or > 100_000)
            throw AppException.BadRequest("Location accuracy is invalid.", "AI_INVALID_LOCATION");
        return style;
    }

    private static MealPlanIntent Normalize(MealPlanIntent value, AiTaxonomyCodes allowed, CreateMealPlanV2Request request)
    {
        value.InputLanguageHint = request.InputLanguage;
        value.ResponseLanguage = request.ResponseLanguage;
        static string[] Keep(IEnumerable<string> values, IReadOnlyCollection<string> whitelist) => values
            .Where(whitelist.Contains).Distinct(StringComparer.Ordinal).OrderBy(code => code).Take(20).ToArray();
        value.PreferredIngredientCodes = Keep(value.PreferredIngredientCodes, allowed.Ingredients);
        value.ExcludedIngredientCodes = Keep(value.ExcludedIngredientCodes, allowed.Ingredients);
        value.AllergenExclusionCodes = Keep(value.AllergenExclusionCodes, allowed.Allergens);
        value.DietaryRequirementCodes = Keep(value.DietaryRequirementCodes, allowed.DietaryAttributes);
        value.PreferredTasteCodes = Keep(value.PreferredTasteCodes, allowed.TasteProfiles);
        value.AvoidedTasteCodes = Keep(value.AvoidedTasteCodes, allowed.TasteProfiles);
        value.PreparationMethodCodes = Keep(value.PreparationMethodCodes, allowed.PreparationMethods);
        value.AvoidedPreparationMethodCodes = Keep(value.AvoidedPreparationMethodCodes, allowed.PreparationMethods);
        value.PreferredCourseCodes = Keep(value.PreferredCourseCodes, allowed.Courses);
        value.RequestedCourseHints = Keep(value.RequestedCourseHints, allowed.Courses);
        value.MealPurposeCodes = Keep(value.MealPurposeCodes, allowed.DiningPurposes);
        value.MaximumDistanceMeters = request.MaxDistanceMeters ?? value.MaximumDistanceMeters;
        value.DistanceRankingEnabled = request.UseDistanceRanking;
        value.RequestedPlanCount = Math.Clamp(request.RequestedPlanCount, 1, 3);
        value.MarketId = request.MarketId;
        value.Scope = request.Scope?.Trim();
        return value;
    }

    private static string RequestHash(CreateMealPlanV2Request request, MealPlanDiningStyle style)
    {
        var canonical = FormattableString.Invariant($"{request.PartySize}|{request.Budget:0.00}|{style}|{request.Request}|{request.InputLanguage}|{request.ResponseLanguage}|{request.Latitude}|{request.Longitude}|{request.MaxDistanceMeters}|{request.UseDistanceRanking}|{request.MarketId}|{request.Scope}|{request.RequestedPlanCount}|{request.PreviousSessionId}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string CartRequestHash(Guid planId, int version)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{planId:D}|{version}")));

    private static string NormalizeLanguage(string? value, bool allowAuto)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? (allowAuto ? "auto" : "vi");
        if ((allowAuto && normalized == "auto") || normalized is "vi" or "en" or "ja" or "ko") return normalized;
        throw AppException.BadRequest("Language is not supported.", "AI_LANGUAGE_NOT_SUPPORTED");
    }

    private static string? NormalizeNaturalLanguageRequest(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureEditable(AiMealPlan plan, DateTime now)
    {
        if (plan.Session.IsExpired(now)) throw AppException.UnprocessableEntity("Meal-plan session has expired.", "AI_PLAN_EXPIRED");
    }
    private static void EnsureMutation(AiMealPlan plan, int expected, DateTime now)
    {
        EnsureEditable(plan, now);
        if (expected != plan.Version) throw AppException.Conflict($"Meal-plan version conflict. Expected {expected}; current {plan.Version}.", "AI_PLAN_VERSION_CONFLICT");
    }
    private static MealPlanDiningStyle ParseStyle(string value) => Enum.TryParse<MealPlanDiningStyle>(value, false, out var parsed) ? parsed : MealPlanDiningStyle.FULL_MEAL;
    private static MealPlanDiningStyle ResolveEffectiveStyle(MealPlanDiningStyle requested, MealPlanIntent intent)
    {
        if (string.Equals(intent.DesiredFullness, "LIGHT", StringComparison.OrdinalIgnoreCase)
            || intent.MealPurposeCodes.Contains("LIGHT_MEAL")) return MealPlanDiningStyle.LIGHT_MEAL;
        if (string.Equals(intent.SocialContext, "FRIEND_GROUP", StringComparison.OrdinalIgnoreCase)
            || intent.IsShareablePreferred == true || intent.MealPurposeCodes.Contains("FRIEND_GROUP")) return MealPlanDiningStyle.FRIEND_GROUP;
        if (string.Equals(intent.SocialContext, "DATE", StringComparison.OrdinalIgnoreCase)) return MealPlanDiningStyle.DATE;
        if (string.Equals(intent.DesiredFullness, "FULL", StringComparison.OrdinalIgnoreCase)
            || intent.MealPurposeCodes.Contains("FULL_MEAL")) return MealPlanDiningStyle.FULL_MEAL;
        return requested;
    }

    private static MealPlanIntent MergeFollowUp(MealPlanIntent previous, MealPlanIntent current)
    {
        static string[] Union(IEnumerable<string> before, IEnumerable<string> after)
            => before.Concat(after).Distinct(StringComparer.Ordinal).Take(20).ToArray();
        current.PreferredIngredientCodes = Union(previous.PreferredIngredientCodes, current.PreferredIngredientCodes);
        current.ExcludedIngredientCodes = Union(previous.ExcludedIngredientCodes, current.ExcludedIngredientCodes);
        current.AllergenExclusionCodes = Union(previous.AllergenExclusionCodes, current.AllergenExclusionCodes);
        current.DietaryRequirementCodes = Union(previous.DietaryRequirementCodes, current.DietaryRequirementCodes);
        current.PreferredTasteCodes = Union(previous.PreferredTasteCodes, current.PreferredTasteCodes);
        current.AvoidedTasteCodes = Union(previous.AvoidedTasteCodes, current.AvoidedTasteCodes);
        current.PreparationMethodCodes = Union(previous.PreparationMethodCodes, current.PreparationMethodCodes);
        current.AvoidedPreparationMethodCodes = Union(previous.AvoidedPreparationMethodCodes, current.AvoidedPreparationMethodCodes);
        current.PreferredCourseCodes = Union(previous.PreferredCourseCodes, current.PreferredCourseCodes);
        current.MealPurposeCodes = Union(previous.MealPurposeCodes, current.MealPurposeCodes);
        current.RequestedCourseHints = Union(previous.RequestedCourseHints, current.RequestedCourseHints);
        current.PreferredServingTemperatures = previous.PreferredServingTemperatures.Concat(current.PreferredServingTemperatures).Distinct().ToArray();
        current.PreferredSpiceLevel ??= previous.PreferredSpiceLevel; current.SocialContext ??= previous.SocialContext;
        current.DesiredFullness ??= previous.DesiredFullness; current.IsShareablePreferred ??= previous.IsShareablePreferred;
        current.TakeawayPreferred ??= previous.TakeawayPreferred; current.QuickServicePreferred ??= previous.QuickServicePreferred;
        current.HealthyPreference ??= previous.HealthyPreference; current.FreshPreference ??= previous.FreshPreference;
        current.PopularityPreference ??= previous.PopularityPreference; current.PreferNearMe |= previous.PreferNearMe;
        current.MaximumDistanceMeters ??= previous.MaximumDistanceMeters;
        var imported = previous.SignalEvidence.Select(value => new IntentSignalEvidence { Field = value.Field, Value = value.Value,
            Source = "FOLLOW_UP_SESSION", Confidence = value.Confidence, EvidenceSpan = value.EvidenceSpan });
        current.SignalEvidence = imported.Concat(current.SignalEvidence).GroupBy(value => (value.Field, value.Value, value.Source))
            .Select(group => group.OrderByDescending(value => value.Confidence).First()).Take(100).ToArray();
        return current;
    }

    private MealPlanV2Response ToCreateResponse(AiMealPlanSession session)
    {
        var intent = JsonSerializer.Deserialize<MealPlanIntent>(session.ParsedPreferenceJson ?? "{}", Json) ?? new();
        var warnings = ParseWarnings(session.WarningsJson);
        var requested = Math.Clamp(intent.RequestedPlanCount, 1, 3);
        var limitations = warnings.Where(IsLimitation).ToArray();
        return new() { SessionId = session.Id, Status = session.Plans.Count == 0 ? "NO_FEASIBLE_PLAN" : session.Plans.Count < requested ? "PARTIAL_PLANS" : "SUCCESS",
            UsedProviderFallback = session.UsedProviderFallback, Provider = intent.Provider, RequestedPlanCount = requested,
            GeneratedPlanCount = session.Plans.Count, Limitations = limitations,
            ProviderRuntime = intent.ProviderRuntime,
            UnderstoodRequest = new() { PartySize = session.PartySize, Budget = session.Budget,
                DiningStyle = session.DiningStyle, InputLanguageHint = intent.InputLanguageHint,
                DetectedLanguage = intent.DetectedLanguage, ResponseLanguage = intent.ResponseLanguage,
                LanguageConfidence = intent.LanguageConfidence, LanguageWarnings = intent.LanguageWarnings, Summary = intent.Summary,
                Preferences = intent.PreferredIngredientCodes.Concat(intent.PreferredTasteCodes).Concat(intent.PreparationMethodCodes).Distinct().ToArray(),
                Exclusions = intent.ExcludedIngredientCodes.Concat(intent.AllergenExclusionCodes).Concat(intent.DietaryRequirementCodes).Distinct().ToArray(),
                Warnings = intent.Warnings, SignalEvidence = intent.SignalEvidence }, Plans = session.Plans.OrderBy(value => value.PlanCode).Select(Summary).ToArray(), Warnings = warnings };
    }

    private MealPlanSummaryResponse Summary(AiMealPlan plan) => new()
    {
        PlanId = plan.Id, PlanCode = plan.PlanCode, Title = plan.PlanTitle, Strategy = plan.Strategy,
        Market = new() { Id = plan.MarketId, Name = plan.Market?.Name ?? string.Empty, ImageUrl = plan.Market?.ThumbnailUrl, DistanceMeters = plan.DistanceMeters, DistanceAvailable = plan.DistanceMeters.HasValue },
        PartySize = plan.Session.PartySize, Budget = plan.Session.Budget, TotalPrice = plan.TotalPrice, RemainingBudget = plan.RemainingBudget,
        BudgetUtilizationPercent = plan.Session.Budget <= 0 ? 0 : RoundPercent(plan.TotalPrice / plan.Session.Budget),
        ServingCoverage = ServingCoverage(plan), CourseCoverage = CourseCoverage(plan),
        FoodCount = plan.Items.Count(value => !value.IsRemoved), BoothCount = plan.Items.Where(value => !value.IsRemoved).Select(value => value.BoothId).Distinct().Count(),
        EstimatedServingCount = plan.EstimatedServingCount, CompatibilityScore = plan.CompatibilityScore,
        DistanceContribution = DistanceContribution(plan.DistanceMeters, plan.Session.ParsedPreferenceJson is not null
            && (JsonSerializer.Deserialize<MealPlanIntent>(plan.Session.ParsedPreferenceJson, Json)?.DistanceRankingEnabled ?? true),
            JsonSerializer.Deserialize<MealPlanIntent>(plan.Session.ParsedPreferenceJson ?? "{}", Json)?.PreferNearMe ?? false,
            plan.Session.MaxDistanceMeters),
        CompatibilityLabel = CompatibilityLabel(plan.CompatibilityScore), IsComplete = plan.IsComplete, Version = plan.Version
    };

    private MealPlanDetailResponse ToDetail(AiMealPlan plan, DateTime now)
    {
        var summary = Summary(plan); var style = policies.Resolve(ParseStyle(plan.Session.DiningStyle));
        return new() { PlanId = summary.PlanId, PlanCode = summary.PlanCode, Title = summary.Title, Strategy = summary.Strategy,
            Market = summary.Market, PartySize = summary.PartySize, Budget = summary.Budget, TotalPrice = summary.TotalPrice,
            RemainingBudget = summary.RemainingBudget, BudgetUtilizationPercent = summary.BudgetUtilizationPercent,
            ServingCoverage = summary.ServingCoverage, CourseCoverage = summary.CourseCoverage,
            FoodCount = summary.FoodCount, BoothCount = summary.BoothCount,
            EstimatedServingCount = summary.EstimatedServingCount, CompatibilityScore = summary.CompatibilityScore,
            DistanceContribution = summary.DistanceContribution,
            CompatibilityLabel = summary.CompatibilityLabel,
            IsComplete = summary.IsComplete, Version = summary.Version, SessionId = plan.SessionId, Status = plan.Status.ToString(),
            Warnings = ParseWarnings(plan.WarningsJson), CourseGroups = plan.Items.Where(value => !value.IsRemoved).GroupBy(value => value.Course)
                .OrderBy(group => group.Key).Select(group => new MealPlanCourseGroupResponse { Course = group.Key.ToString(),
                    IsRequired = style.RequiredCourses.Contains(group.Key), IsComplete = group.Any(), Items = group.OrderBy(value => value.SortOrder).Select(value => DetailItem(value, plan, now)).ToArray() }).ToArray() };
    }

    private static MealPlanItemResponse DetailItem(AiMealPlanItem item, AiMealPlan plan, DateTime now)
    {
        decimal? currentPrice = item.FoodItem is null ? null : FoodPriceResolver.GetCurrentPrice(item.FoodItem, now);
        var orderable = item.FoodItem is { IsDeleted: false, IsAvailable: true } && item.Booth?.Status == BoothStatus.Active
            && !plan.Market.IsDeleted && plan.Market.Status == NightMarketStatus.Active && plan.Market.ModerationStatus == ModerationStatus.Active;
        return new() { PlanItemId = item.Id, FoodId = item.FoodItemId, FoodName = item.FoodNameSnapshot, ImageUrl = item.ImageUrlSnapshot,
            Course = item.Course.ToString(), Quantity = item.Quantity, UnitPriceSnapshot = item.UnitPriceSnapshot,
            TotalPriceSnapshot = item.TotalPriceSnapshot, ServingCountSnapshot = item.ServingCountSnapshot,
            Booth = new() { Id = item.BoothId ?? Guid.Empty, Name = item.BoothNameSnapshot }, Rating = item.RatingSnapshot,
            ReviewCount = item.ReviewCountSnapshot, CompatibilityScore = item.CompatibilityScore, Reason = item.Reason,
            CanReplace = !plan.Session.IsExpired(now), CanRemove = !plan.Session.IsExpired(now), IsCurrentlyOrderable = orderable,
            CurrentPrice = currentPrice, HasPriceChanged = currentPrice.HasValue && currentPrice != item.UnitPriceSnapshot };
    }

    private static string[] ParseWarnings(string? json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json ?? "[]", Json) ?? []; }
        catch (JsonException) { return ["PLAN_WARNING_DATA_INVALID"]; }
    }

    private static IReadOnlyCollection<Guid> ActiveFoodIds(AiMealPlan plan) => plan.Items
        .Where(item => !item.IsRemoved && item.FoodItemId.HasValue).Select(item => item.FoodItemId!.Value).ToArray();

    private static decimal PlanOverlap(AiMealPlan left, AiMealPlan right)
    {
        var a = ActiveFoodIds(left).ToHashSet(); var b = ActiveFoodIds(right).ToHashSet();
        if (a.Count == 0 && b.Count == 0) return 1;
        return (decimal)a.Intersect(b).Count() / Math.Max(1, a.Union(b).Count());
    }

    private static bool MeaningfullyDifferent(AiMealPlan left, AiMealPlan right)
    {
        var a = left.Items.Where(item => !item.IsRemoved).ToArray();
        var b = right.Items.Where(item => !item.IsRemoved).ToArray();
        var differentMain = !a.Where(item => item.Course is FoodCourse.MAIN_COURSE or FoodCourse.SHARED_DISH)
            .Select(item => item.FoodItemId).ToHashSet().SetEquals(b
                .Where(item => item.Course is FoodCourse.MAIN_COURSE or FoodCourse.SHARED_DISH).Select(item => item.FoodItemId));
        var differentBooths = !a.Select(item => item.BoothId).ToHashSet().SetEquals(b.Select(item => item.BoothId));
        var differentCourses = !a.Select(item => item.Course).ToHashSet().SetEquals(b.Select(item => item.Course));
        var priceDifference = Math.Abs(left.TotalPrice - right.TotalPrice) >= Math.Max(left.TotalPrice, right.TotalPrice) * .10m;
        return differentMain || differentBooths || differentCourses || priceDifference;
    }

    private static string Limitation(IReadOnlyCollection<FoodRecommendationCandidate> eligible, IReadOnlyCollection<AiMealPlan> plans,
        CreateMealPlanV2Request request, MealPlanStylePolicy policy)
    {
        if (eligible.Count == 0) return "HARD_CONSTRAINTS_TOO_STRICT";
        var mains = eligible.Where(value => value.Courses.Contains(FoodCourse.MAIN_COURSE) && value.EstimatedServingCount.HasValue).ToArray();
        if (mains.Length == 0) return "INSUFFICIENT_COURSE_COVERAGE";
        var required = (int)Math.Ceiling(request.PartySize * policy.ServingMultiplier);
        if (!mains.Any(value => value.CurrentPrice * Math.Ceiling(required / (decimal)value.EstimatedServingCount!.Value) <= request.Budget))
            return "BUDGET_TOO_LOW";
        if (eligible.Select(value => value.FoodId).Distinct().Count() < policy.MinimumFoods + request.RequestedPlanCount - 1)
            return "INSUFFICIENT_DISTINCT_FOODS";
        return plans.Count == 0 ? "SERVING_INSUFFICIENT" : "DIVERSITY_NOT_ACHIEVABLE";
    }

    private static bool IsLimitation(string value) => value is "INSUFFICIENT_COURSE_COVERAGE" or "INSUFFICIENT_DISTINCT_FOODS"
        or "BUDGET_TOO_LOW" or "SERVING_INSUFFICIENT" or "HARD_CONSTRAINTS_TOO_STRICT" or "ONLY_ONE_MARKET_FEASIBLE"
        or "DIVERSITY_NOT_ACHIEVABLE";

    private void LogDiagnostics(Guid customerId, CreateMealPlanV2Request request, string? rawRequest,
        MealPlanIntentExtractionResult extraction, MealPlanIntent intent, IReadOnlyCollection<FoodRecommendationCandidate> loaded,
        IReadOnlyCollection<FoodRecommendationCandidate> eligible, IReadOnlyCollection<AiMealPlan> plans)
    {
        var courses = eligible.SelectMany(value => value.Courses).GroupBy(value => value)
            .ToDictionary(group => group.Key.ToString(), group => group.Count());
        var booths = eligible.GroupBy(value => value.BoothId).ToDictionary(group => group.Key, group => group.Count());
        var overlaps = plans.SelectMany((plan, index) => plans.Skip(index + 1)
            .Select(other => $"{plan.PlanCode}-{other.PlanCode}:{PlanOverlap(plan, other):0.00}")).ToArray();
        logger.LogInformation("MealPlanV2 diagnostics CustomerId={CustomerId} PartySize={PartySize} Budget={Budget} MarketId={MarketId} Scope={Scope} RawRequest={RawRequest} ParsedIntent={ParsedIntent} Provider={Provider} FallbackUsed={FallbackUsed} CandidateCount={CandidateCount} EligibleCandidateCount={EligibleCandidateCount} CandidatesByCourse={CandidatesByCourse} CandidatesByBooth={CandidatesByBooth} RequestedPlanCount={RequestedPlanCount} GeneratedRawPlanCount={GeneratedRawPlanCount} GeneratedDistinctPlanCount={GeneratedDistinctPlanCount} OverlapScores={OverlapScores} FinalPlanCount={FinalPlanCount}",
            customerId, request.PartySize, request.Budget, request.MarketId, request.Scope, rawRequest,
            JsonSerializer.Serialize(intent, Json), extraction.ProviderName ?? intent.Provider, extraction.UsedFallback,
            loaded.Count, eligible.Count, JsonSerializer.Serialize(courses, Json), JsonSerializer.Serialize(booths, Json),
            intent.RequestedPlanCount, eligible.GroupBy(value => value.MarketId).Count() * intent.RequestedPlanCount,
            plans.Count, overlaps, plans.Count);
    }

    private static decimal RoundPercent(decimal ratio) => Math.Round(Math.Clamp(ratio * 100m, 0, 100), 2, MidpointRounding.AwayFromZero);
    private static decimal ServingCoverage(AiMealPlan plan) => RoundPercent((plan.EstimatedServingCount ?? 0) / (decimal)Math.Max(1, plan.Session.PartySize));
    private decimal CourseCoverage(AiMealPlan plan)
    {
        var required = policies.Resolve(ParseStyle(plan.Session.DiningStyle)).RequiredCourses;
        return required.Count == 0 ? 100 : RoundPercent(required.Count(course => plan.Items.Any(item => !item.IsRemoved && item.Course == course)) / (decimal)required.Count);
    }
    private static string CompatibilityLabel(decimal score) => score switch
    {
        >= 85 => "Rất phù hợp", >= 70 => "Phù hợp", >= 55 => "Gần phù hợp", _ => "Lựa chọn gần nhất trong dữ liệu hiện có"
    };

    private static int? CalculateDistance(decimal? lat1, decimal? lon1, decimal? lat2, decimal? lon2)
    {
        if (!lat1.HasValue || !lon1.HasValue || !lat2.HasValue || !lon2.HasValue) return null;
        const double radius = 6_371_000; static double R(decimal value) => (double)value * Math.PI / 180d;
        var a = Math.Pow(Math.Sin((R(lat2.Value) - R(lat1.Value)) / 2), 2)
            + Math.Cos(R(lat1.Value)) * Math.Cos(R(lat2.Value)) * Math.Pow(Math.Sin((R(lon2.Value) - R(lon1.Value)) / 2), 2);
        return (int)Math.Round(radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)), MidpointRounding.AwayFromZero);
    }

    private sealed record Scored(FoodRecommendationCandidate Value, decimal Score);
}

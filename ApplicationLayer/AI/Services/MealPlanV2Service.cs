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
    IOptions<MealPlanV2Options> options,
    TimeProvider timeProvider,
    ILogger<MealPlanV2Service> logger) : IMealPlanV2Service
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly MealPlanV2Options _options = options.Value;

    public async Task<ApiResponse<MealPlanV2Response>> CreateAsync(Guid customerId, CreateMealPlanV2Request request, CancellationToken cancellationToken)
    {
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
        var extraction = await intentExtractor.ExtractMealPlanIntentAsync(
            new(request.Request.Trim(), taxonomy, style.ToString()), cancellationToken);
        if (extraction.FailureCategory == AiProviderFailureCategory.CANCELLED) throw new OperationCanceledException(cancellationToken);
        if (!extraction.IsSuccess || extraction.ParsedResult is null)
            throw AppException.UnprocessableEntity("The meal-plan request could not be understood.", "AI_INVALID_REQUEST");
        var intent = Normalize(extraction.ParsedResult, taxonomy, request);
        var policy = policies.Resolve(style);
        var loaded = await candidates.GetCandidatesAsync(now, Math.Clamp(_options.CandidateLimit, 1, 500),
            Math.Clamp(_options.MaximumCandidatesPerMarket, 1, 100), cancellationToken);
        var eligible = loaded.Where(value => Eligible(value, intent, request, now)).ToArray();
        var plans = Generate(eligible, intent, request, policy, now);
        var warnings = extraction.ValidationWarnings.Concat(intent.Warnings).Distinct(StringComparer.Ordinal).OrderBy(value => value).ToList();
        if (!request.Latitude.HasValue) warnings.Add("LOCATION_NOT_PROVIDED_DISTANCE_UNAVAILABLE");
        if (plans.Count < Math.Clamp(_options.MaximumPlans, 1, 3)) warnings.Add("FEWER_THAN_THREE_FEASIBLE_PLANS");
        if (plans.Count == 0) warnings.Add("AI_NO_FEASIBLE_PLAN");
        var session = new AiMealPlanSession
        {
            Id = Guid.NewGuid(), CustomerId = customerId, PartySize = request.PartySize, Budget = request.Budget,
            DiningStyle = style.ToString(), OriginalRequest = request.Request.Trim(), ParsedPreferenceJson = JsonSerializer.Serialize(intent, Json),
            Latitude = request.Latitude, Longitude = request.Longitude, MaxDistanceMeters = request.MaxDistanceMeters,
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

    private List<AiMealPlan> Generate(IReadOnlyCollection<FoodRecommendationCandidate> eligible, MealPlanIntent intent,
        CreateMealPlanV2Request request, MealPlanStylePolicy policy, DateTime now)
    {
        var feasible = eligible.GroupBy(value => value.MarketId).Select(group => BuildMarket(group.ToArray(), intent, request, policy, now))
            .Where(value => value is not null).Cast<MarketPlan>().ToArray();
        var strategies = request.Latitude.HasValue
            ? new[] { MealPlanStrategy.NEAREST, MealPlanStrategy.BEST_MATCH, MealPlanStrategy.BUDGET_FRIENDLY }
            : new[] { MealPlanStrategy.BEST_MATCH, MealPlanStrategy.BUDGET_FRIENDLY };
        var selected = new List<MarketPlan>();
        foreach (var strategy in strategies)
        {
            var pool = feasible.Where(value => selected.All(chosen => chosen.Plan.MarketId != value.Plan.MarketId));
            var next = strategy switch
            {
                MealPlanStrategy.NEAREST => pool.OrderBy(value => value.Plan.DistanceMeters).ThenByDescending(value => value.Plan.CompatibilityScore).FirstOrDefault(),
                MealPlanStrategy.BUDGET_FRIENDLY => pool.OrderBy(value => value.Plan.TotalPrice).ThenByDescending(value => value.Plan.CompatibilityScore).FirstOrDefault(),
                _ => pool.OrderByDescending(value => value.Plan.CompatibilityScore).ThenBy(value => value.Plan.TotalPrice).FirstOrDefault()
            };
            if (next is null) continue;
            next.Plan.Strategy = strategy.ToString(); next.Plan.PlanCode = ((char)('A' + selected.Count)).ToString();
            next.Plan.PlanTitle = strategy switch { MealPlanStrategy.NEAREST => "Gần bạn nhất", MealPlanStrategy.BUDGET_FRIENDLY => "Tiết kiệm hợp lý", _ => "Phù hợp nhất" };
            selected.Add(next);
            if (selected.Count >= Math.Clamp(_options.MaximumPlans, 1, 3)) break;
        }
        return selected.Select(value => value.Plan).ToList();
    }

    private MarketPlan? BuildMarket(FoodRecommendationCandidate[] values, MealPlanIntent intent, CreateMealPlanV2Request request,
        MealPlanStylePolicy policy, DateTime now)
    {
        var scored = values.Where(value => value.Courses.Count > 0 && value.EstimatedServingCount.HasValue)
            .Select(value => new Scored(value, Compatibility(value, intent))).OrderByDescending(value => value.Score)
            .ThenBy(value => value.Value.CurrentPrice).ThenBy(value => value.Value.FoodId).ToArray();
        var main = scored.Where(value => value.Value.Courses.Contains(FoodCourse.MAIN_COURSE)).Take(Math.Clamp(_options.MaximumCandidatesPerCourse, 1, 30)).ToArray();
        if (main.Length == 0) return null;
        var requiredServing = (int)Math.Ceiling(request.PartySize * policy.ServingMultiplier);
        var selected = new List<(Scored Value, FoodCourse Course, int Quantity)>();
        var primary = main.First();
        var quantity = (int)Math.Ceiling(requiredServing / (decimal)primary.Value.EstimatedServingCount!.Value);
        if (primary.Value.CurrentPrice * quantity > request.Budget) return null;
        selected.Add((primary, FoodCourse.MAIN_COURSE, quantity));
        foreach (var candidate in scored.Where(value => value.Value.FoodId != primary.Value.FoodId)
                     .Where(value => value.Value.Courses.Any(course => policy.OptionalCourses.Contains(course))))
        {
            if (selected.Count >= policy.MinimumFoods && selected.Select(value => value.Value.Value.BoothId).Distinct().Count() >= policy.MinimumBooths) break;
            var course = candidate.Value.Courses.First(value => policy.OptionalCourses.Contains(value));
            if (selected.Sum(value => value.Value.Value.CurrentPrice * value.Quantity) + candidate.Value.CurrentPrice > request.Budget) continue;
            selected.Add((candidate, course, 1));
            if (selected.Count >= policy.MaximumFoods) break;
        }
        if (selected.Count < policy.MinimumFoods || selected.Select(value => value.Value.Value.BoothId).Distinct().Count() < policy.MinimumBooths) return null;
        var distance = CalculateDistance(request.Latitude, request.Longitude, values[0].MarketLatitude, values[0].MarketLongitude);
        var plan = new AiMealPlan { Id = Guid.NewGuid(), MarketId = values[0].MarketId, PlanCode = "A", PlanTitle = "Meal plan",
            Strategy = MealPlanStrategy.BEST_MATCH.ToString(), DistanceMeters = distance, Status = AiMealPlanStatus.READY,
            Summary = $"{selected.Count} món tại {values[0].MarketName}", CreatedAt = now, UpdatedAt = now };
        var order = 0;
        foreach (var value in selected) plan.AddItem(Item(value.Value.Value, value.Course, value.Quantity, ++order, now, value.Value.Score), value.Value.Value.MarketId, request.Budget, false);
        recalculation.Recalculate(plan, policy, request.PartySize, request.Budget, now);
        return plan.IsComplete ? new(plan) : null;
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

    private static AiMealPlanItem Item(FoodRecommendationCandidate value, FoodCourse course, int quantity, int sort, DateTime now, decimal compatibilityScore)
        => new() { Id = Guid.NewGuid(), FoodItemId = value.FoodId, BoothId = value.BoothId, FoodNameSnapshot = value.FoodName,
            BoothNameSnapshot = value.BoothName, ImageUrlSnapshot = value.ImageUrl, Course = course, Quantity = quantity,
            UnitPriceSnapshot = value.CurrentPrice, TotalPriceSnapshot = value.CurrentPrice * quantity,
            ServingCountSnapshot = value.EstimatedServingCount * quantity, RatingSnapshot = value.Rating,
            ReviewCountSnapshot = value.ReviewCount, CompatibilityScore = compatibilityScore, Reason = "Được chọn bằng bộ lọc và chấm điểm deterministic.",
            SortOrder = sort, CreatedAt = now, UpdatedAt = now };

    private static decimal Compatibility(FoodRecommendationCandidate value, MealPlanIntent intent)
    {
        var signals = intent.PreferredIngredientCodes.Count + intent.PreferredTasteCodes.Count + intent.PreparationMethodCodes.Count
            + intent.PreferredCourseCodes.Count + intent.MealPurposeCodes.Count;
        var matches = intent.PreferredIngredientCodes.Intersect(value.IngredientCodes).Count()
            + intent.PreferredTasteCodes.Intersect(value.TasteCodes).Count()
            + intent.PreparationMethodCodes.Intersect(value.PreparationMethodCodes).Count()
            + intent.PreferredCourseCodes.Intersect(value.Courses.Select(course => course.ToString())).Count()
            + intent.MealPurposeCodes.Intersect(value.DiningPurposes.Select(purpose => purpose.ToString())).Count();
        var relevance = signals == 0 ? 55m : 35m + 55m * matches / signals;
        var rating = value.Rating.HasValue && value.ReviewCount > 0 ? value.Rating.Value : 0;
        return Math.Round(Math.Clamp(relevance + rating * 2m, 0, 100), 2, MidpointRounding.AwayFromZero);
    }

    private static bool Eligible(FoodRecommendationCandidate value, MealPlanIntent intent, CreateMealPlanV2Request request, DateTime now)
    {
        if (!value.IsAvailable || value.IsDeleted || value.CategoryDeleted || !value.CategoryIsActive || !value.CategoryIsSelectable
            || value.CurrentPrice <= 0 || value.BoothStatus != BoothStatus.Active || value.MarketDeleted
            || value.MarketModerationStatus != ModerationStatus.Active || value.MarketStatus != NightMarketStatus.Active) return false;
        var local = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(now));
        if (!CustomerAvailability.IsOpenNow(true, value.MarketOpenTime, value.MarketCloseTime, value.BoothOpenTime, value.BoothCloseTime, local)) return false;
        if (intent.ExcludedIngredientCodes.Intersect(value.IngredientCodes, StringComparer.Ordinal).Any()) return false;
        if (intent.AvoidedPreparationMethodCodes.Intersect(value.PreparationMethodCodes, StringComparer.Ordinal).Any()) return false;
        if (intent.AvoidedTasteCodes.Intersect(value.TasteCodes, StringComparer.Ordinal).Any()) return false;
        if (intent.DietaryRequirementCodes.Any(required => !value.DietaryAttributes.Any(attribute => attribute.Code == required
            && attribute.IsConfirmed && attribute.Status == DietarySuitabilityStatus.SUITABLE))) return false;
        if (intent.AllergenExclusionCodes.Count > 0) return false;
        if (request.Latitude.HasValue && request.MaxDistanceMeters.HasValue)
        {
            var distance = CalculateDistance(request.Latitude, request.Longitude, value.MarketLatitude, value.MarketLongitude);
            if (!distance.HasValue || distance > request.MaxDistanceMeters) return false;
        }
        return true;
    }

    private MealPlanDiningStyle Validate(CreateMealPlanV2Request request)
    {
        if (request.PartySize <= 0 || request.PartySize > Math.Clamp(_options.MaximumPartySize, 1, 100)
            || request.Budget <= 0 || request.Budget > _options.MaximumBudget
            || string.IsNullOrWhiteSpace(request.Request) || request.Request.Trim().Length > Math.Clamp(_options.MaximumRequestCharacters, 1, 4000)
            || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Trim().Length > 100
            || !Enum.TryParse<MealPlanDiningStyle>(request.DiningStyle?.Trim(), false, out var style))
            throw AppException.BadRequest("Meal-plan request is invalid.", "AI_INVALID_REQUEST");
        if (request.Latitude.HasValue != request.Longitude.HasValue || request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
            throw AppException.BadRequest("Location is invalid.", "AI_INVALID_LOCATION");
        if (request.MaxDistanceMeters is <= 0 || request.MaxDistanceMeters > Math.Clamp(_options.MaximumDistanceMeters, 1, 500_000))
            throw AppException.BadRequest("Maximum distance is invalid.", "AI_INVALID_LOCATION");
        return style;
    }

    private static MealPlanIntent Normalize(MealPlanIntent value, AiTaxonomyCodes allowed, CreateMealPlanV2Request request)
    {
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
        value.MaximumDistanceMeters = request.MaxDistanceMeters;
        return value;
    }

    private static string RequestHash(CreateMealPlanV2Request request, MealPlanDiningStyle style)
    {
        var canonical = FormattableString.Invariant($"{request.PartySize}|{request.Budget:0.00}|{style}|{request.Request.Trim()}|{request.Latitude}|{request.Longitude}|{request.MaxDistanceMeters}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

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

    private MealPlanV2Response ToCreateResponse(AiMealPlanSession session)
    {
        var intent = JsonSerializer.Deserialize<MealPlanIntent>(session.ParsedPreferenceJson ?? "{}", Json) ?? new();
        var warnings = ParseWarnings(session.WarningsJson);
        return new() { SessionId = session.Id, Status = session.Plans.Count == 0 ? "NO_FEASIBLE_PLAN" : session.Plans.Count < 3 ? "PARTIAL_PLANS" : "SUCCESS",
            UsedProviderFallback = session.UsedProviderFallback, UnderstoodRequest = new() { PartySize = session.PartySize, Budget = session.Budget,
                DiningStyle = session.DiningStyle, Summary = intent.Summary,
                Preferences = intent.PreferredIngredientCodes.Concat(intent.PreferredTasteCodes).Concat(intent.PreparationMethodCodes).Distinct().ToArray(),
                Exclusions = intent.ExcludedIngredientCodes.Concat(intent.AllergenExclusionCodes).Concat(intent.DietaryRequirementCodes).Distinct().ToArray(),
                Warnings = intent.Warnings }, Plans = session.Plans.OrderBy(value => value.PlanCode).Select(Summary).ToArray(), Warnings = warnings };
    }

    private MealPlanSummaryResponse Summary(AiMealPlan plan) => new()
    {
        PlanId = plan.Id, PlanCode = plan.PlanCode, Title = plan.PlanTitle, Strategy = plan.Strategy,
        Market = new() { Id = plan.MarketId, Name = plan.Market?.Name ?? string.Empty, ImageUrl = plan.Market?.ThumbnailUrl, DistanceMeters = plan.DistanceMeters },
        PartySize = plan.Session.PartySize, Budget = plan.Session.Budget, TotalPrice = plan.TotalPrice, RemainingBudget = plan.RemainingBudget,
        FoodCount = plan.Items.Count(value => !value.IsRemoved), BoothCount = plan.Items.Where(value => !value.IsRemoved).Select(value => value.BoothId).Distinct().Count(),
        EstimatedServingCount = plan.EstimatedServingCount, CompatibilityScore = plan.CompatibilityScore, IsComplete = plan.IsComplete, Version = plan.Version
    };

    private MealPlanDetailResponse ToDetail(AiMealPlan plan, DateTime now)
    {
        var summary = Summary(plan); var style = policies.Resolve(ParseStyle(plan.Session.DiningStyle));
        return new() { PlanId = summary.PlanId, PlanCode = summary.PlanCode, Title = summary.Title, Strategy = summary.Strategy,
            Market = summary.Market, PartySize = summary.PartySize, Budget = summary.Budget, TotalPrice = summary.TotalPrice,
            RemainingBudget = summary.RemainingBudget, FoodCount = summary.FoodCount, BoothCount = summary.BoothCount,
            EstimatedServingCount = summary.EstimatedServingCount, CompatibilityScore = summary.CompatibilityScore,
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

    private static int? CalculateDistance(decimal? lat1, decimal? lon1, decimal? lat2, decimal? lon2)
    {
        if (!lat1.HasValue || !lon1.HasValue || !lat2.HasValue || !lon2.HasValue) return null;
        const double radius = 6_371_000; static double R(decimal value) => (double)value * Math.PI / 180d;
        var a = Math.Pow(Math.Sin((R(lat2.Value) - R(lat1.Value)) / 2), 2)
            + Math.Cos(R(lat1.Value)) * Math.Cos(R(lat2.Value)) * Math.Pow(Math.Sin((R(lon2.Value) - R(lon1.Value)) / 2), 2);
        return (int)Math.Round(radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)), MidpointRounding.AwayFromZero);
    }

    private sealed record Scored(FoodRecommendationCandidate Value, decimal Score);
    private sealed record MarketPlan(AiMealPlan Plan);
}

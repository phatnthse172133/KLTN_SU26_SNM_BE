using System.Diagnostics;
using System.Text.Json;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Carts;
using ApplicationLayer.Services.CustomerDiscovery;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services.Assistant;

public sealed partial class AssistantService(
    IAssistantConversationRepository conversations,
    IAssistantFoodQueryRepository foods,
    IFoodSemanticMetadataRepository metadata,
    INightMarketRepository markets,
    ICartService carts,
    AssistantIntentInterpreter interpreter,
    AssistantSemanticMatcher semanticMatcher,
    AssistantCompatibilityScorer scorer,
    AssistantMealPlanComposer mealPlanComposer,
    AssistantReplyComposer replyComposer,
    IOptions<AssistantOptions> assistantOptions,
    IOptions<OpenAiOptions> openAiOptions,
    TimeProvider timeProvider,
    ILogger<AssistantService> logger) : IAssistantService
{
    private readonly AssistantOptions _options = assistantOptions.Value;
    private readonly OpenAiOptions _openAi = openAiOptions.Value;

    public async Task<ApiResponse<CreateAssistantConversationResponse>> CreateConversationAsync(
        Guid customerId,
        CreateAssistantConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid? marketId = null;
        if (request.MarketId.HasValue && request.MarketId.Value != Guid.Empty)
        {
            if (!await markets.CustomerVisibleExistsAsync(request.MarketId.Value, cancellationToken))
                throw AssistantErrors.MarketNotFound();
            marketId = request.MarketId;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var conversation = new AssistantConversation
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            MarketId = marketId,
            Status = AssistantConversationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        conversations.Add(conversation);
        await conversations.SaveChangesAsync(cancellationToken);
        return ApiResponse<CreateAssistantConversationResponse>.SuccessResponse(new CreateAssistantConversationResponse
        {
            Id = conversation.Id,
            Status = conversation.Status
        });
    }

    public async Task<ApiResponse<AssistantTurnResponse>> SendMessageAsync(
        Guid customerId,
        Guid conversationId,
        SendAssistantMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var conversation = await conversations.GetOwnedAsync(conversationId, customerId, cancellationToken)
            ?? throw AssistantErrors.ConversationNotFound();

        var incoming = (request.Message ?? string.Empty).Trim();
        if (incoming.Length > _options.MaxMessageLength)
            throw AssistantErrors.InvalidRequest("Message is too long.");

        var resumingLocation = conversation.Status == AssistantConversationStatus.LocationPending
            && HasValidCoordinates(request.Latitude, request.Longitude);
        var message = incoming;
        if (string.IsNullOrWhiteSpace(message) && resumingLocation)
            message = conversation.PendingUserMessage?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
            throw AssistantErrors.InvalidRequest("Message is required.");

        var marketId = request.MarketId ?? conversation.MarketId;
        var hasGps = HasValidCoordinates(request.Latitude, request.Longitude);
        if (request.MarketId.HasValue && request.MarketId.Value != Guid.Empty)
        {
            if (!await markets.CustomerVisibleExistsAsync(request.MarketId.Value, cancellationToken))
                throw AssistantErrors.MarketNotFound();
            conversation.MarketId = request.MarketId;
            marketId = request.MarketId;
        }
        else if (marketId.HasValue && !await markets.CustomerVisibleExistsAsync(marketId.Value, cancellationToken))
            throw AssistantErrors.MarketNotFound();

        if (!_openAi.Enabled)
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.Disabled);
        if (string.IsNullOrWhiteSpace(_openAi.ApiKey))
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.MissingKey);

        var started = Stopwatch.StartNew();
        var catalogs = await metadata.GetActiveCatalogsAsync(cancellationToken);
        var history = await conversations.GetRecentMessagesAsync(conversationId, _options.ConversationHistoryLimit, cancellationToken);
        var totalFoodCount = await foods.CountNotDeletedFoodItemsAsync(cancellationToken);
        var stageA = new AssistantStageAContext
        {
            MarketId = marketId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            MaxDistanceMeters = request.MaxDistanceMeters,
            PartySize = request.PartySize,
            Budget = request.Budget
        };

        var intent = TryReusePendingIntent(conversation, incoming, hasGps, marketId, catalogs, stageA)
            ?? await interpreter.InterpretAsync(
                message,
                history,
                catalogs,
                stageA,
                cancellationToken);
        intent = AssistantIntentInterpreter.ApplyExplicitPlanningContext(intent, stageA);
        if (intent.Intent == AssistantIntentKind.MEAL_PLAN)
        {
            if (intent.PartySize is not >= 1)
                throw AssistantErrors.InvalidRequest("PartySize is required for meal-plan requests.");
            if (intent.BudgetMax is not > 0)
                throw AssistantErrors.InvalidRequest("Budget is required for meal-plan requests.");
        }
        if (intent.NeedsLocation && !hasGps && marketId is null)
        {
            conversation.Status = AssistantConversationStatus.LocationPending;
            conversation.PendingUserMessage = message;
            conversation.PendingParsedIntentJson = JsonSerializer.Serialize(intent, AssistantJson.Options);
            conversation.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            await conversations.SaveChangesAsync(cancellationToken);
            throw AssistantErrors.LocationRequired();
        }

        conversation.Status = AssistantConversationStatus.Active;
        if (conversation.PendingUserMessage is not null)
            conversation.PendingUserMessage = null;
        if (conversation.PendingParsedIntentJson is not null)
            conversation.PendingParsedIntentJson = null;

        IReadOnlyList<AssistantScoredFood> ranked = [];
        IReadOnlyList<AssistantMealPlanDraft> mealDrafts = [];
        IReadOnlyList<AssistantEligibleFood> eligible = [];
        AssistantSemanticMatchResult semantic = new();
        var queryAt = timeProvider.GetUtcNow().UtcDateTime;

        var skipSearch = intent.Intent is AssistantIntentKind.CHITCHAT or AssistantIntentKind.CLARIFY;
        if (!skipSearch)
        {
            var criteria = BuildCriteria(intent, catalogs, marketId, request, queryAt);
            eligible = await foods.GetEligibleFoodsAsync(criteria, cancellationToken);
            if (eligible.Count > 0)
            {
                semantic = await semanticMatcher.ScoreAsync(message, intent, eligible, cancellationToken);
                ranked = scorer.Score(eligible, intent, semantic, queryAt);
                if (intent.Intent == AssistantIntentKind.MEAL_PLAN)
                    mealDrafts = await mealPlanComposer.ComposeAsync(message, intent, ranked, cancellationToken);
            }
        }

        var recommendations = ranked.Take(_options.MaxRecommendations).ToArray();
        var persistedPlans = new List<(AssistantMealPlan Plan, AssistantMealPlanDraft Draft)>(mealDrafts.Count);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var draft in mealDrafts)
        {
            var plan = PersistMealPlan(conversation, customerId, draft, now);
            conversation.MealPlans.Add(plan);
            persistedPlans.Add((plan, draft));
        }

        var reply = replyComposer.Compose(intent, recommendations, mealDrafts);
        conversation.Messages.Add(new AssistantMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = AssistantMessageRole.User,
            Content = message,
            CreatedAt = now
        });
        conversation.Messages.Add(new AssistantMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = AssistantMessageRole.Assistant,
            Content = reply,
            CreatedAt = now
        });
        conversation.UpdatedAt = now;
        await conversations.SaveChangesAsync(cancellationToken);

        started.Stop();
        var diagnostics = BuildDiagnostics(intent, totalFoodCount, eligible, semantic, started.ElapsedMilliseconds);
        logger.LogInformation(
            "Assistant turn {ConversationId} eligible={EligibleCount} totalFoods={TotalFoodCount} batches={BatchCount} idsSent={IdsSent} idsEvaluated={IdsEvaluated} latencyMs={LatencyMs} intent={Intent}",
            conversation.Id,
            diagnostics.EligibleCount,
            diagnostics.TotalFoodCount,
            diagnostics.BatchCount,
            diagnostics.IdsSent.Count,
            diagnostics.IdsEvaluated.Count,
            diagnostics.LatencyMs,
            intent.Intent);

        var mappedPlans = persistedPlans.Select(item => MapMealPlan(item.Plan, item.Draft)).ToArray();
        return ApiResponse<AssistantTurnResponse>.SuccessResponse(new AssistantTurnResponse
        {
            ConversationId = conversation.Id,
            Status = conversation.Status,
            Intent = intent.Intent,
            Reply = reply,
            LocationRequired = false,
            Recommendations = recommendations.Select(MapRecommendation).ToArray(),
            MealPlan = mappedPlans.FirstOrDefault(),
            MealPlans = mappedPlans,
            PreferenceSummary = MapSummary(intent, recommendations, mealDrafts),
            Diagnostics = diagnostics
        });
    }

    public async Task<ApiResponse<CartBatchAddResponse>> AddMealPlanToCartAsync(
        Guid customerId,
        Guid conversationId,
        Guid mealPlanId,
        CancellationToken cancellationToken = default)
    {
        var plan = await conversations.GetOwnedMealPlanAsync(conversationId, mealPlanId, customerId, cancellationToken)
            ?? throw AssistantErrors.MealPlanNotFound();
        if (plan.Items.Count == 0)
            throw AssistantErrors.MealPlanEmpty();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var item in plan.Items.OrderBy(value => value.DisplayOrder))
        {
            if (item.Quantity <= 0)
                throw AssistantErrors.MealPlanUnavailable();

            var current = await foods.GetCurrentByIdAsync(item.FoodItemId, now, cancellationToken)
                ?? throw AssistantErrors.MealPlanUnavailable();
            if (!CustomerOrderability.EvaluateForCartAdd(current.FoodItem, now).CanOrder)
                throw AssistantErrors.MealPlanUnavailable();
            if (current.EffectivePrice != item.UnitPriceSnapshot)
                throw AssistantErrors.MealPlanPriceChanged();
        }

        return await carts.AddItemsAsync(customerId, new AddCartItemsRequest
        {
            Items = plan.Items
                .OrderBy(item => item.DisplayOrder)
                .Select(item => new AddCartItemRequest
                {
                    FoodItemId = item.FoodItemId,
                    Quantity = item.Quantity
                })
                .ToArray()
        }, cancellationToken);
    }

    private AssistantFoodQueryCriteria BuildCriteria(
        ParsedAssistantIntent intent,
        FoodSemanticCatalogSet catalogs,
        Guid? marketId,
        SendAssistantMessageRequest request,
        DateTime utcNow)
    {
        var allergenIds = Ids(catalogs.Allergens, intent.HardConstraints.AllergenCodes).Distinct().ToArray();
        var avoidedIngredients = Ids(catalogs.Ingredients, intent.HardConstraints.AvoidedIngredientCodes).Distinct().ToArray();
        var dietary = Ids(catalogs.DietaryAttributes, intent.HardConstraints.DietaryCodes).Distinct().ToArray();
        var avoidedTastes = Ids(catalogs.TasteProfiles, intent.HardConstraints.AvoidedTasteCodes).Distinct().ToArray();

        return new AssistantFoodQueryCriteria
        {
            MarketId = marketId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            MaxDistanceMeters = request.MaxDistanceMeters ?? _options.DefaultMaxDistanceMeters,
            BudgetMin = intent.BudgetMin,
            BudgetMax = intent.BudgetMax,
            AllergenExclusionIds = allergenIds,
            AvoidedIngredientIds = avoidedIngredients,
            DietaryRequirementIds = dietary,
            AvoidedTasteProfileIds = avoidedTastes,
            MaxSpiceLevel = intent.HardConstraints.MaxSpiceLevel,
            TreatMayContainAsHard = _options.TreatMayContainAsHard,
            UtcNow = utcNow
        };
    }

    private static IEnumerable<Guid> Ids(IReadOnlyCollection<SemanticCatalogEntity> catalog, IEnumerable<string> codes)
    {
        var lookup = catalog.ToDictionary(item => item.Code, item => item.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var code in codes)
        {
            if (lookup.TryGetValue(code, out var id))
                yield return id;
        }
    }

    private ParsedAssistantIntent? TryReusePendingIntent(
        AssistantConversation conversation,
        string incoming,
        bool hasGps,
        Guid? marketId,
        FoodSemanticCatalogSet catalogs,
        AssistantStageAContext context)
    {
        if (conversation.Status != AssistantConversationStatus.LocationPending)
            return null;
        if (string.IsNullOrWhiteSpace(conversation.PendingParsedIntentJson))
            return null;
        if (!hasGps && marketId is null)
            return null;
        if (!string.IsNullOrWhiteSpace(incoming)
            && !string.Equals(incoming, conversation.PendingUserMessage, StringComparison.Ordinal))
            return null;

        try
        {
            var parsed = JsonSerializer.Deserialize<ParsedAssistantIntent>(
                conversation.PendingParsedIntentJson, AssistantJson.Options);
            if (parsed is null)
                return null;
            logger.LogInformation("Assistant conversation {ConversationId} resumed pending intent without Stage A.", conversation.Id);
            return AssistantIntentInterpreter.ApplyExplicitPlanningContext(
                AssistantIntentInterpreter.Sanitize(parsed, catalogs),
                context);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AssistantTurnDiagnostics BuildDiagnostics(
        ParsedAssistantIntent intent,
        int totalFoodCount,
        IReadOnlyList<AssistantEligibleFood> eligible,
        AssistantSemanticMatchResult semantic,
        long latencyMs)
    {
        var idsSent = semantic.IdsSent.Count > 0
            ? semantic.IdsSent
            : eligible.Select(item => item.FoodItem.Id).ToArray();
        var idsEvaluated = semantic.Scores.Count > 0
            ? semantic.Scores.Keys.ToArray()
            : [];
        var summary = MapSummary(intent, []);
        return new AssistantTurnDiagnostics
        {
            EligibleCount = eligible.Count,
            TotalFoodCount = totalFoodCount,
            BatchCount = semantic.BatchCount,
            IdsSent = idsSent,
            IdsEvaluated = idsEvaluated,
            LatencyMs = latencyMs,
            ParsedIntent = new AssistantParsedIntentDiagnostics
            {
                Intent = intent.Intent,
                NeedsLocation = intent.NeedsLocation,
                BudgetMin = intent.BudgetMin,
                BudgetMax = intent.BudgetMax,
                PartySize = intent.PartySize,
                HardConstraints = summary.HardConstraints,
                StructuredPreferences = summary.StructuredPreferences,
                SemanticPreferences = intent.SemanticPreferences,
                SemanticAvoidances = intent.SemanticAvoidances,
                DiningContext = intent.DiningContext,
                UserGoal = intent.UserGoal,
                AdditionalMeaning = intent.AdditionalMeaning
            }
        };
    }

    private static bool HasValidCoordinates(double? latitude, double? longitude)
        => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private static AssistantMealPlan PersistMealPlan(
        AssistantConversation conversation,
        Guid customerId,
        AssistantMealPlanDraft draft,
        DateTime now)
    {
        var plan = new AssistantMealPlan
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            CustomerId = customerId,
            NightMarketId = draft.NightMarketId,
            EstimatedTotal = draft.EstimatedTotal,
            BudgetMax = draft.BudgetMax,
            PartySize = draft.PartySize,
            CreatedAt = now
        };
        var order = 0;
        foreach (var item in draft.Items)
        {
            plan.Items.Add(new AssistantMealPlanItem
            {
                Id = Guid.NewGuid(),
                MealPlanId = plan.Id,
                FoodItemId = item.FoodItem.Id,
                Quantity = item.Quantity,
                UnitPriceSnapshot = item.UnitPrice,
                DisplayOrder = order++
            });
        }
        return plan;
    }

    private static AssistantRecommendationResponse MapRecommendation(AssistantScoredFood item)
    {
        var food = item.Eligible.FoodItem;
        return new AssistantRecommendationResponse
        {
            FoodItemId = food.Id,
            Name = food.Name,
            Description = food.Description,
            ThumbnailUrl = food.ThumbnailUrl,
            BoothId = food.BoothId,
            BoothName = food.Booth.BoothName,
            NightMarketId = food.Booth.NightMarketId,
            NightMarketName = food.Booth.NightMarket.Name,
            EffectivePrice = item.Eligible.EffectivePrice,
            DistanceMeters = item.Eligible.DistanceMeters,
            AverageRating = food.AverageRating,
            ReviewCount = food.ReviewCount,
            FinalScore = Math.Round(item.FinalScore, 4),
            SemanticScore = Math.Round(item.SemanticScore, 4),
            Reasons = item.Reasons,
            UnknownDataFacets = item.UnknownDataFacets,
            IsFeatured = food.IsFeatured,
            HasPromotion = item.Eligible.HasActivePromotion,
            IsOpenNow = item.IsOpenNow
        };
    }

    private static AssistantMealPlanResponse MapMealPlan(AssistantMealPlan plan, AssistantMealPlanDraft draft)
    {
        var persisted = plan.Items.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).ToArray();
        var items = new List<AssistantMealPlanItemResponse>(draft.Items.Count);
        for (var index = 0; index < draft.Items.Count; index++)
        {
            var planItemId = index < persisted.Length ? persisted[index].Id : Guid.Empty;
            items.Add(MapMealPlanItem(planItemId, draft.Items[index]));
        }

        var warnings = draft.Warnings.ToList();
        if (ServingsInsufficient(draft.Items.Select(item => (item.FoodItem, item.Quantity)), plan.PartySize))
            warnings.Add("SERVINGS_INSUFFICIENT");
        return ComposePlanResponse(
            plan,
            draft.Title,
            draft.NightMarketName,
            draft.OverallPlanReason,
            warnings,
            draft.UnknownDataFacets,
            items);
    }

    private static AssistantMealPlanItemResponse MapMealPlanItem(Guid planItemId, AssistantMealPlanDraftItem item)
        => MapMealPlanItem(
            planItemId,
            item.FoodItem,
            item.Quantity,
            item.UnitPrice,
            item.Course,
            item.CompatibilityScore,
            item.Reasons,
            item.DistanceMeters);

    private static AssistantMealPlanItemResponse MapMealPlanItem(
        Guid planItemId,
        FoodItem food,
        int quantity,
        decimal unitPrice,
        FoodCourse course,
        double? compatibilityScore,
        IReadOnlyList<string> reasons,
        double? distanceMeters)
        => new()
        {
            PlanItemId = planItemId,
            FoodItemId = food.Id,
            FoodName = food.Name,
            Name = food.Name,
            ThumbnailUrl = food.ThumbnailUrl,
            Course = course.ToString(),
            Quantity = quantity,
            UnitPrice = unitPrice,
            LineTotal = unitPrice * quantity,
            BoothId = food.BoothId,
            BoothName = food.Booth.BoothName,
            NightMarketId = food.Booth.NightMarketId,
            NightMarketName = food.Booth.NightMarket.Name,
            CompatibilityScore = compatibilityScore,
            Reasons = reasons,
            DistanceMeters = distanceMeters
        };

    private static AssistantMealPlanResponse ComposePlanResponse(
        AssistantMealPlan plan,
        string? title,
        string nightMarketName,
        string? overallPlanReason,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> unknownData,
        IReadOnlyList<AssistantMealPlanItemResponse> items)
    {
        var total = items.Sum(item => item.LineTotal);
        var remaining = plan.BudgetMax is null ? (decimal?)null : plan.BudgetMax.Value - total;
        var over = plan.BudgetMax is null ? 0m : Math.Max(0m, total - plan.BudgetMax.Value);
        var uniqueWarnings = warnings
            .Concat(over > 0 ? ["OVER_BUDGET"] : [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var scores = items.Where(item => item.CompatibilityScore.HasValue).Select(item => item.CompatibilityScore!.Value).ToArray();
        var distances = items.Where(item => item.DistanceMeters.HasValue).Select(item => item.DistanceMeters!.Value).ToArray();
        var grouped = items
            .GroupBy(item => AssistantMealPlanValidator.ResolveCourseFromToken(item.Course))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var courses = AssistantMealPlanValidator.SectionOrder(grouped.Keys);
        return new AssistantMealPlanResponse
        {
            Id = plan.Id,
            Title = title,
            NightMarketId = plan.NightMarketId,
            NightMarketName = string.IsNullOrWhiteSpace(nightMarketName)
                ? items.FirstOrDefault()?.NightMarketName ?? string.Empty
                : nightMarketName,
            PartySize = plan.PartySize,
            Budget = plan.BudgetMax,
            BudgetMax = plan.BudgetMax,
            TotalPrice = total,
            EstimatedTotal = total,
            RemainingBudget = remaining,
            OverBudgetAmount = over,
            DistanceMeters = distances.Length == 0 ? null : distances.Min(),
            PlanCompatibility = scores.Length == 0 ? null : Math.Round(scores.Average(), 4),
            OverallPlanReason = overallPlanReason,
            Warnings = uniqueWarnings,
            UnknownData = unknownData,
            Sections = courses
                .Where(course => grouped.ContainsKey(course) && grouped[course].Length > 0)
                .Select(course => new AssistantMealPlanSectionResponse
                {
                    Course = course.ToString(),
                    Items = grouped[course]
                })
                .ToArray(),
            Items = items.ToArray()
        };
    }

    private static bool ServingsInsufficient(IEnumerable<(FoodItem Food, int Quantity)> lines, int partySize)
    {
        if (partySize <= 0)
            return false;
        var servings = lines.Sum(line =>
        {
            var perItem = line.Food.EstimatedServingCount is > 0 ? line.Food.EstimatedServingCount.Value : 1;
            return perItem * line.Quantity;
        });
        return servings < partySize;
    }

    private static AssistantPreferenceSummaryResponse MapSummary(
        ParsedAssistantIntent intent,
        IReadOnlyList<AssistantScoredFood> recommendations,
        IReadOnlyList<AssistantMealPlanDraft>? mealPlans = null)
        => new()
        {
            HardConstraints = Concat(
                intent.HardConstraints.AllergenCodes.Select(code => $"allergen:{code}"),
                intent.HardConstraints.AvoidedIngredientCodes.Select(code => $"avoid:{code}"),
                intent.HardConstraints.DietaryCodes.Select(code => $"diet:{code}"),
                intent.HardConstraints.AvoidedTasteCodes.Select(code => $"avoid-taste:{code}"),
                intent.HardConstraints.MaxSpiceLevel is null ? [] : [$"spice<={intent.HardConstraints.MaxSpiceLevel}"]),
            StructuredPreferences = Concat(
                intent.StructuredPreferences.PreferredIngredientCodes,
                intent.StructuredPreferences.PreferredTasteCodes,
                intent.StructuredPreferences.PreferredPreparationCodes,
                intent.StructuredPreferences.PreferredCourseCodes),
            SemanticPreferences = intent.SemanticPreferences,
            SemanticAvoidances = intent.SemanticAvoidances,
            DiningContext = intent.DiningContext,
            UserGoal = intent.UserGoal,
            AdditionalMeaning = intent.AdditionalMeaning,
            UnknownDataFacets = recommendations
                .SelectMany(item => item.UnknownDataFacets)
                .Concat((mealPlans ?? []).SelectMany(plan => plan.UnknownDataFacets))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

    private static IReadOnlyList<string> Concat(params IEnumerable<string>[] parts)
        => parts.SelectMany(part => part).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToArray();
}

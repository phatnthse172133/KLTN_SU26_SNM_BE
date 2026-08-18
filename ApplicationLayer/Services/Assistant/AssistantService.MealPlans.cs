using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;

namespace ApplicationLayer.Services.Assistant;

public sealed partial class AssistantService
{
    public async Task<ApiResponse<AssistantMealPlanResponse>> UpdateMealPlanItemQuantityAsync(
        Guid customerId,
        Guid conversationId,
        Guid mealPlanId,
        Guid itemId,
        UpdateAssistantMealPlanItemQuantityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity < 1)
            throw AssistantErrors.InvalidRequest("Quantity must be greater than zero.");

        var plan = await LoadOwnedPlanAsync(customerId, conversationId, mealPlanId, cancellationToken);
        var item = plan.Items.FirstOrDefault(value => value.Id == itemId)
            ?? throw AssistantErrors.MealPlanItemNotFound();
        item.Quantity = request.Quantity;
        return ApiResponse<AssistantMealPlanResponse>.SuccessResponse(
            await RecalculateAndMapAsync(plan, cancellationToken));
    }

    public async Task<ApiResponse<AssistantMealPlanResponse>> RemoveMealPlanItemAsync(
        Guid customerId,
        Guid conversationId,
        Guid mealPlanId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var plan = await LoadOwnedPlanAsync(customerId, conversationId, mealPlanId, cancellationToken);
        var item = plan.Items.FirstOrDefault(value => value.Id == itemId)
            ?? throw AssistantErrors.MealPlanItemNotFound();
        plan.Items.Remove(item);
        return ApiResponse<AssistantMealPlanResponse>.SuccessResponse(
            await RecalculateAndMapAsync(plan, cancellationToken));
    }

    public async Task<ApiResponse<AssistantMealPlanReplacementsResponse>> GetMealPlanItemReplacementsAsync(
        Guid customerId,
        Guid conversationId,
        Guid mealPlanId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var plan = await LoadOwnedPlanAsync(customerId, conversationId, mealPlanId, cancellationToken);
        var item = plan.Items.FirstOrDefault(value => value.Id == itemId)
            ?? throw AssistantErrors.MealPlanItemNotFound();
        var scored = await ScoreReplacementsAsync(plan, item.FoodItemId, cancellationToken);
        return ApiResponse<AssistantMealPlanReplacementsResponse>.SuccessResponse(new AssistantMealPlanReplacementsResponse
        {
            Replacements = scored.Take(_options.MaxRecommendations).Select(MapRecommendation).ToArray()
        });
    }

    public async Task<ApiResponse<AssistantMealPlanResponse>> ReplaceMealPlanItemAsync(
        Guid customerId,
        Guid conversationId,
        Guid mealPlanId,
        Guid itemId,
        ReplaceAssistantMealPlanItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ReplacementFoodItemId == Guid.Empty)
            throw AssistantErrors.InvalidRequest("Replacement food item id is required.");

        var plan = await LoadOwnedPlanAsync(customerId, conversationId, mealPlanId, cancellationToken);
        var item = plan.Items.FirstOrDefault(value => value.Id == itemId)
            ?? throw AssistantErrors.MealPlanItemNotFound();
        var scored = await ScoreReplacementsAsync(plan, item.FoodItemId, cancellationToken);
        var replacement = scored.FirstOrDefault(value => value.Eligible.FoodItem.Id == request.ReplacementFoodItemId)
            ?? throw AssistantErrors.PlanChanged();

        item.FoodItemId = replacement.Eligible.FoodItem.Id;
        item.UnitPriceSnapshot = replacement.Eligible.EffectivePrice;
        return ApiResponse<AssistantMealPlanResponse>.SuccessResponse(
            await RecalculateAndMapAsync(plan, cancellationToken, new Dictionary<Guid, AssistantScoredFood>
            {
                [replacement.Eligible.FoodItem.Id] = replacement
            }));
    }

    private async Task<AssistantMealPlan> LoadOwnedPlanAsync(
        Guid customerId,
        Guid conversationId,
        Guid mealPlanId,
        CancellationToken cancellationToken)
        => await conversations.GetOwnedMealPlanAsync(conversationId, mealPlanId, customerId, cancellationToken)
            ?? throw AssistantErrors.MealPlanNotFound();

    private async Task<IReadOnlyList<AssistantScoredFood>> ScoreReplacementsAsync(
        AssistantMealPlan plan,
        Guid excludeFoodItemId,
        CancellationToken cancellationToken)
    {
        EnsureOpenAiConfigured();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var history = await conversations.GetRecentMessagesAsync(plan.ConversationId, _options.ConversationHistoryLimit, cancellationToken);
        var originalMessage = history.LastOrDefault(item => item.Role == AssistantMessageRole.User)?.Content ?? string.Empty;
        var intent = new ParsedAssistantIntent
        {
            Intent = AssistantIntentKind.MEAL_PLAN,
            PartySize = plan.PartySize,
            BudgetMax = plan.BudgetMax
        };
        var eligible = await foods.GetEligibleFoodsAsync(new AssistantFoodQueryCriteria
        {
            MarketId = plan.NightMarketId,
            BudgetMax = plan.BudgetMax,
            TreatMayContainAsHard = _options.TreatMayContainAsHard,
            UtcNow = now
        }, cancellationToken);
        var remaining = eligible.Foods
            .Where(item => item.FoodItem.Id != excludeFoodItemId)
            .Take(Math.Max(1, _options.MaxMealPlanCandidates))
            .ToArray();
        if (remaining.Length == 0)
            return [];

        var semantic = await semanticMatcher.ScoreAsync(originalMessage, intent, remaining, cancellationToken);
        return scorer.Score(remaining, intent, semantic, now);
    }

    private async Task<AssistantMealPlanResponse> RecalculateAndMapAsync(
        AssistantMealPlan plan,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<Guid, AssistantScoredFood>? scoredByFoodId = null)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var mapped = new List<AssistantMealPlanItemResponse>();
        var servings = new List<(FoodItem Food, int Quantity)>();
        foreach (var item in plan.Items.OrderBy(value => value.DisplayOrder).ThenBy(value => value.Id))
        {
            var current = await foods.GetCurrentByIdAsync(item.FoodItemId, now, cancellationToken)
                ?? throw AssistantErrors.MealPlanUnavailable();
            item.UnitPriceSnapshot = current.EffectivePrice;
            AssistantScoredFood? scored = null;
            scoredByFoodId?.TryGetValue(item.FoodItemId, out scored);
            mapped.Add(MapMealPlanItem(
                item.Id,
                current.FoodItem,
                item.Quantity,
                current.EffectivePrice,
                AssistantMealPlanValidator.ResolveCourse(current.FoodItem, null),
                scored?.FinalScore,
                scored?.Reasons ?? [],
                current.DistanceMeters ?? scored?.Eligible.DistanceMeters));
            servings.Add((current.FoodItem, item.Quantity));
        }

        plan.EstimatedTotal = mapped.Sum(item => item.LineTotal);
        var warnings = new List<string>();
        if (ServingsInsufficient(servings, plan.PartySize))
            warnings.Add("SERVINGS_INSUFFICIENT");

        await conversations.SaveChangesAsync(cancellationToken);
        return ComposePlanResponse(
            plan,
            title: null,
            nightMarketName: plan.NightMarket?.Name ?? mapped.FirstOrDefault()?.NightMarketName ?? string.Empty,
            overallPlanReason: null,
            warnings,
            unknownData: [],
            mapped);
    }

    private void EnsureOpenAiConfigured()
    {
        if (!_openAi.Enabled)
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.Disabled);
        if (string.IsNullOrWhiteSpace(_openAi.ApiKey))
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.MissingKey);
    }
}

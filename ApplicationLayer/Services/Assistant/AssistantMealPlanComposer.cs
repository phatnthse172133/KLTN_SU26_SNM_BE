using System.Text.Json;
using ApplicationLayer.Exceptions;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services.Assistant;

public sealed class AssistantMealPlanDraft
{
    public Guid NightMarketId { get; init; }
    public string NightMarketName { get; init; } = string.Empty;
    public decimal EstimatedTotal { get; init; }
    public int PartySize { get; init; }
    public decimal? BudgetMax { get; init; }
    public decimal? RemainingBudget { get; init; }
    public string? Title { get; init; }
    public string? Strategy { get; init; }
    public string? OverallPlanReason { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> UnknownDataFacets { get; init; } = [];
    public IReadOnlyList<AssistantMealPlanDraftItem> Items { get; init; } = [];
}

public sealed class AssistantMealPlanDraftItem
{
    public required FoodItem FoodItem { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public FoodCourse Course { get; init; }
}

public sealed class AssistantMealPlanProposal
{
    public string? Title { get; init; }
    public string? Strategy { get; init; }
    public string? OverallReason { get; init; }
    public IReadOnlyList<AssistantMealPlanProposalItem> Items { get; init; } = [];
}

public sealed class AssistantMealPlanProposalItem
{
    public Guid FoodItemId { get; init; }
    public int Quantity { get; init; }
    public string? Course { get; init; }
}

public sealed class AssistantMealPlanComposer(
    ILanguageModelClient languageModel,
    AssistantMealPlanValidator validator,
    IOptions<AssistantOptions> assistantOptions,
    IOptions<OpenAiOptions> openAiOptions)
{
    private readonly AssistantOptions _assistant = assistantOptions.Value;
    private readonly OpenAiOptions _openAi = openAiOptions.Value;

    public async Task<IReadOnlyList<AssistantMealPlanDraft>> ComposeAsync(
        string originalMessage,
        ParsedAssistantIntent intent,
        IReadOnlyList<AssistantScoredFood> ranked,
        CancellationToken cancellationToken)
    {
        if (ranked.Count == 0)
            return [];

        var cap = Math.Max(1, _assistant.MaxMealPlanCandidates);
        var candidates = ranked.Take(cap).ToArray();
        var raw = await CompleteAsync(originalMessage, intent, candidates, cancellationToken);
        var allowed = candidates.Select(item => item.Eligible.FoodItem.Id).ToHashSet();
        var proposals = ParseProposals(raw, allowed);
        return validator.Validate(proposals, candidates, intent);
    }

    public static int ResolveQuantity(FoodItem food, int partySize)
    {
        var serving = food.EstimatedServingCount is > 0 ? food.EstimatedServingCount.Value : 1;
        if (food.IsShareable == true && serving >= partySize)
            return 1;
        return (int)Math.Ceiling(partySize / (double)serving);
    }

    public static IReadOnlyList<AssistantMealPlanProposal> ParseProposals(string raw, ISet<Guid> allowedIds)
    {
        MealPlanBatchDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<MealPlanBatchDto>(raw, AssistantJson.Options)
                ?? throw new JsonException("Meal plan payload was null.");
        }
        catch (JsonException exception)
        {
            throw AssistantErrors.ProviderUnavailable(exception);
        }

        if (dto.Plans is null)
            throw AssistantErrors.ProviderUnavailable();

        var proposals = new List<AssistantMealPlanProposal>();
        foreach (var plan in dto.Plans)
        {
            var items = new List<AssistantMealPlanProposalItem>();
            foreach (var item in plan.Items ?? [])
            {
                if (item.FoodItemId == Guid.Empty || !allowedIds.Contains(item.FoodItemId))
                    throw AssistantErrors.ProviderUnavailable();
                items.Add(new AssistantMealPlanProposalItem
                {
                    FoodItemId = item.FoodItemId,
                    Quantity = item.Quantity,
                    Course = string.IsNullOrWhiteSpace(item.Course) ? null : item.Course.Trim()
                });
            }

            proposals.Add(new AssistantMealPlanProposal
            {
                Title = NormalizeOptional(plan.Title),
                Strategy = NormalizeOptional(plan.Strategy),
                OverallReason = NormalizeOptional(plan.OverallReason),
                Items = items
            });
        }

        return proposals;
    }

    private async Task<string> CompleteAsync(
        string originalMessage,
        ParsedAssistantIntent intent,
        IReadOnlyList<AssistantScoredFood> ranked,
        CancellationToken cancellationToken)
    {
        var projection = _assistant.CompactCandidateProjection;
        var payload = JsonSerializer.Serialize(new
        {
            originalMessage,
            parsedIntent = intent,
            partySize = Math.Max(1, intent.PartySize ?? 1),
            budgetMax = intent.BudgetMax,
            maxMealPlanOptions = Math.Max(1, _assistant.MaxMealPlanOptions),
            candidates = ranked.Select(item =>
            {
                var compact = AssistantSemanticMatcher.ProjectFood(item.Eligible, projection);
                compact.SemanticCompatibility = item.SemanticScore;
                return compact;
            }).ToArray()
        }, AssistantJson.Options);

        try
        {
            return await languageModel.CompleteJsonAsync(
                AssistantPromptCatalog.MealPlanSystem + "\nJSON schema:\n" + AssistantPromptCatalog.MealPlanSchema,
                payload,
                _openAi.MaxOutputTokensMealPlan,
                cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw AssistantErrors.ProviderUnavailable(exception);
        }
        catch (Exception exception)
        {
            throw AssistantErrors.ProviderUnavailable(exception);
        }
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class MealPlanBatchDto
    {
        public List<MealPlanDto>? Plans { get; set; }
    }

    private sealed class MealPlanDto
    {
        public string? Title { get; set; }
        public string? Strategy { get; set; }
        public string? OverallReason { get; set; }
        public List<MealPlanItemDto>? Items { get; set; }
    }

    private sealed class MealPlanItemDto
    {
        public Guid FoodItemId { get; set; }
        public int Quantity { get; set; }
        public string? Course { get; set; }
    }
}

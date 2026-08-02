using ApplicationLayer.AI.V2.Models;

namespace ApplicationLayer.AI.V2.Services;

public sealed class DeterministicRecommendationReasonBuilder : IDeterministicRecommendationReasonBuilder
{
    public string Build(FoodRecommendationExplanationContext context)
    {
        var evidence = new List<string>();
        Add(evidence, context.MatchedIngredients.FirstOrDefault() is { } ingredient ? $"có {ingredient}" : null);
        Add(evidence, context.MatchedTasteAndSpice.FirstOrDefault() is { } taste ? $"vị {taste}" : null);
        Add(evidence, context.MatchedPreparationMethods.FirstOrDefault() is { } method ? $"được {method}" : null);
        Add(evidence, context.BudgetEvidence);
        Add(evidence, context.DistanceEvidence);
        Add(evidence, context.RatingEvidence);
        Add(evidence, context.MatchedCoursesAndPurposes.FirstOrDefault());
        var selected = evidence.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();
        var reason = selected.Length == 0
            ? $"{context.FoodName} phù hợp với yêu cầu về {context.Category}."
            : $"{context.FoodName} {string.Join(", ", selected)}.";
        return reason.Length <= 220 ? reason : reason[..217].TrimEnd(' ', ',') + "...";
    }

    private static void Add(ICollection<string> values, string? value) { if (!string.IsNullOrWhiteSpace(value)) values.Add(value.Trim().TrimEnd('.')); }
}

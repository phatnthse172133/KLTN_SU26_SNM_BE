using DomainLayer.Enums;
using System.Collections;
using System.Globalization;

namespace ApplicationLayer.AI.V2.Models;

public enum AiProviderFailureCategory
{
    NONE, DISABLED, TIMEOUT, RATE_LIMITED, TRANSIENT_ERROR,
    PERMANENT_ERROR, INVALID_RESPONSE, VALIDATION_FAILED, CANCELLED
}

public enum FoodRecommendationSortPreference
{
    BEST_MATCH, NEAREST_RELEVANT, HIGHEST_RATED_RELEVANT, LOWEST_PRICE_RELEVANT
}

public sealed class AiProviderRuntimeTrace
{
    public bool ProviderAttempted { get; set; }
    public string? ProviderUsed { get; set; }
    public bool FallbackUsed { get; set; }
    public string? FallbackReason { get; set; }
    public long ProviderLatencyMs { get; set; }
    public string FailureCategory { get; set; } = AiProviderFailureCategory.NONE.ToString();
    public string? ModelName { get; set; }
    public bool FeatureEnabled { get; set; }
    public bool ApiKeyLoaded { get; set; }
    public string CircuitBreakerState { get; set; } = "NOT_CONFIGURED";
}

public sealed class IntentSignalEvidence
{
    public string Field { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Source { get; set; } = "LLM_INFERENCE";
    public decimal Confidence { get; set; }
    public string? EvidenceSpan { get; set; }
}

public static class IntentSignalEvidenceFactory
{
    private static readonly HashSet<string> SupportedFields = new(StringComparer.Ordinal)
    {
        nameof(FoodRecommendationIntent.DesiredFoodTerms), nameof(FoodRecommendationIntent.PreferredIngredientCodes),
        nameof(FoodRecommendationIntent.ExcludedIngredientCodes), nameof(FoodRecommendationIntent.AllergenExclusionCodes),
        nameof(FoodRecommendationIntent.DietaryRequirementCodes), nameof(FoodRecommendationIntent.PreferredTasteCodes),
        nameof(FoodRecommendationIntent.AvoidedTasteCodes), nameof(FoodRecommendationIntent.PreferredSpiceLevel),
        nameof(FoodRecommendationIntent.PreparationMethodCodes), nameof(FoodRecommendationIntent.AvoidedPreparationMethodCodes),
        nameof(FoodRecommendationIntent.PreferredCourseCodes), nameof(FoodRecommendationIntent.ExcludedCourseCodes),
        nameof(FoodRecommendationIntent.MealPurposeCodes), nameof(FoodRecommendationIntent.PreferredServingTemperatures),
        nameof(FoodRecommendationIntent.SocialContext), nameof(FoodRecommendationIntent.DesiredFullness),
        nameof(FoodRecommendationIntent.MinimumPrice), nameof(FoodRecommendationIntent.MaximumPrice),
        nameof(FoodRecommendationIntent.PartySize), nameof(FoodRecommendationIntent.IsShareablePreferred),
        nameof(FoodRecommendationIntent.TakeawayPreferred), nameof(FoodRecommendationIntent.QuickServicePreferred),
        nameof(FoodRecommendationIntent.HealthyPreference), nameof(FoodRecommendationIntent.FreshPreference),
        nameof(FoodRecommendationIntent.PopularityPreference), nameof(FoodRecommendationIntent.PreferNearMe),
        nameof(FoodRecommendationIntent.MaximumDistanceMeters)
    };

    public static IReadOnlyCollection<IntentSignalEvidence> Create(object intent, string rawQuery, string source, decimal confidence)
    {
        var result = new List<IntentSignalEvidence>();
        foreach (var property in intent.GetType().GetProperties().Where(property => SupportedFields.Contains(property.Name)))
        {
            var raw = property.GetValue(intent);
            if (raw is null || raw is false) continue;
            string[] values = raw is string text ? [text] : raw is IEnumerable sequence
                ? sequence.Cast<object?>().Where(value => value is not null).Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)!).ToArray()
                : [Convert.ToString(raw, CultureInfo.InvariantCulture)!];
            foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal))
                result.Add(new() { Field = property.Name, Value = value, Source = source,
                    Confidence = Math.Clamp(confidence, 0m, 1m), EvidenceSpan = source == "SYSTEM_DEFAULT" || source == "CUSTOMER_PROFILE" ? null : rawQuery });
        }
        return result.OrderBy(value => value.Field, StringComparer.Ordinal).ThenBy(value => value.Value, StringComparer.Ordinal).Take(100).ToArray();
    }
}

public sealed class FoodRecommendationIntent
{
    public string InputLanguageHint { get; set; } = "auto";
    public string DetectedLanguage { get; set; } = "vi";
    public string ResponseLanguage { get; set; } = "vi";
    public decimal? LanguageConfidence { get; set; }
    public IReadOnlyCollection<string> LanguageWarnings { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public string OriginalNormalizedQuery { get; set; } = string.Empty;
    public IReadOnlyCollection<string> DesiredFoodTerms { get; set; } = [];
    public IReadOnlyCollection<string> ContextualTerms { get; set; } = [];
    public IReadOnlyCollection<string> UnmappedMeaningfulTerms { get; set; } = [];
    public IReadOnlyCollection<string> PreferredIngredientCodes { get; set; } = [];
    public IReadOnlyCollection<string> ExcludedIngredientCodes { get; set; } = [];
    public IReadOnlyCollection<string> AllergenExclusionCodes { get; set; } = [];
    public IReadOnlyCollection<string> DietaryRequirementCodes { get; set; } = [];
    public IReadOnlyCollection<string> PreferredTasteCodes { get; set; } = [];
    public IReadOnlyCollection<string> AvoidedTasteCodes { get; set; } = [];
    public FoodSpiceLevel? PreferredSpiceLevel { get; set; }
    public IReadOnlyCollection<string> PreparationMethodCodes { get; set; } = [];
    public IReadOnlyCollection<string> AvoidedPreparationMethodCodes { get; set; } = [];
    public IReadOnlyCollection<string> PreferredCourseCodes { get; set; } = [];
    public IReadOnlyCollection<string> ExcludedCourseCodes { get; set; } = [];
    public IReadOnlyCollection<string> MealPurposeCodes { get; set; } = [];
    public IReadOnlyCollection<ServingTemperature> PreferredServingTemperatures { get; set; } = [];
    public string? SocialContext { get; set; }
    public string? DesiredFullness { get; set; }
    public decimal? MinimumPrice { get; set; }
    public decimal? MaximumPrice { get; set; }
    public int? PartySize { get; set; }
    public bool? IsShareablePreferred { get; set; }
    public bool? TakeawayPreferred { get; set; }
    public bool? QuickServicePreferred { get; set; }
    public bool? HealthyPreference { get; set; }
    public bool? FreshPreference { get; set; }
    public bool? PopularityPreference { get; set; }
    public bool PreferNearMe { get; set; }
    public int? MaximumDistanceMeters { get; set; }
    public bool DistanceRankingEnabled { get; set; }
    public FoodRecommendationSortPreference SortPreference { get; set; } = FoodRecommendationSortPreference.BEST_MATCH;
    public decimal Confidence { get; set; }
    public IReadOnlyCollection<string> Ambiguities { get; set; } = [];
    public bool ClarificationNeeded { get; set; }
    public IReadOnlyCollection<string> Warnings { get; set; } = [];
    public IReadOnlyCollection<IntentSignalEvidence> SignalEvidence { get; set; } = [];
}

public sealed class MealPlanIntent
{
    public string InputLanguageHint { get; set; } = "auto";
    public string DetectedLanguage { get; set; } = "vi";
    public string ResponseLanguage { get; set; } = "vi";
    public decimal? LanguageConfidence { get; set; }
    public IReadOnlyCollection<string> LanguageWarnings { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyCollection<string> PreferredIngredientCodes { get; set; } = [];
    public IReadOnlyCollection<string> ExcludedIngredientCodes { get; set; } = [];
    public IReadOnlyCollection<string> AllergenExclusionCodes { get; set; } = [];
    public IReadOnlyCollection<string> DietaryRequirementCodes { get; set; } = [];
    public IReadOnlyCollection<string> PreferredTasteCodes { get; set; } = [];
    public IReadOnlyCollection<string> AvoidedTasteCodes { get; set; } = [];
    public FoodSpiceLevel? PreferredSpiceLevel { get; set; }
    public IReadOnlyCollection<string> PreparationMethodCodes { get; set; } = [];
    public IReadOnlyCollection<string> AvoidedPreparationMethodCodes { get; set; } = [];
    public IReadOnlyCollection<string> PreferredCourseCodes { get; set; } = [];
    public IReadOnlyCollection<string> MealPurposeCodes { get; set; } = [];
    public IReadOnlyCollection<string> RequestedCourseHints { get; set; } = [];
    public IReadOnlyCollection<ServingTemperature> PreferredServingTemperatures { get; set; } = [];
    public string? SocialContext { get; set; }
    public string? DesiredFullness { get; set; }
    public int? PartySize { get; set; }
    public bool? IsShareablePreferred { get; set; }
    public bool? TakeawayPreferred { get; set; }
    public bool? QuickServicePreferred { get; set; }
    public bool? HealthyPreference { get; set; }
    public bool? FreshPreference { get; set; }
    public bool? PopularityPreference { get; set; }
    public bool PreferNearMe { get; set; }
    public int? MaximumDistanceMeters { get; set; }
    public bool DistanceRankingEnabled { get; set; } = true;
    public decimal Confidence { get; set; }
    public IReadOnlyCollection<string> Warnings { get; set; } = [];
    // Runtime request context persisted with the structured intent so additive
    // response fields survive idempotent replays without a schema migration.
    public int RequestedPlanCount { get; set; } = 3;
    public Guid? MarketId { get; set; }
    public string? Scope { get; set; }
    public string Provider { get; set; } = "NEUTRAL";
    public AiProviderRuntimeTrace ProviderRuntime { get; set; } = new();
    public IReadOnlyCollection<IntentSignalEvidence> SignalEvidence { get; set; } = [];
}

public sealed record AiTaxonomyCodes(
    IReadOnlyCollection<string> Ingredients,
    IReadOnlyCollection<string> Allergens,
    IReadOnlyCollection<string> DietaryAttributes,
    IReadOnlyCollection<string> PreparationMethods,
    IReadOnlyCollection<string> TasteProfiles,
    IReadOnlyCollection<string> Courses,
    IReadOnlyCollection<string> DiningPurposes);

public sealed record FoodRecommendationIntentRequest(string Query, AiTaxonomyCodes AllowedTaxonomy,
    string InputLanguageHint = "auto", string ResponseLanguage = "vi");
public sealed record MealPlanIntentRequest(string Query, AiTaxonomyCodes AllowedTaxonomy, string DiningStyle = "FULL_MEAL",
    string InputLanguageHint = "auto", string ResponseLanguage = "vi");

public sealed class FoodRecommendationIntentExtractionResult
{
    public bool IsSuccess { get; init; }
    public bool UsedFallback { get; init; }
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public string? ProviderRequestId { get; init; }
    public AiProviderFailureCategory FailureCategory { get; init; }
    public IReadOnlyCollection<string> ValidationWarnings { get; init; } = [];
    public FoodRecommendationIntent? ParsedResult { get; init; }
    public AiProviderRuntimeTrace RuntimeTrace { get; init; } = new();
}

public sealed class MealPlanIntentExtractionResult
{
    public bool IsSuccess { get; init; }
    public bool UsedFallback { get; init; }
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public string? ProviderRequestId { get; init; }
    public AiProviderFailureCategory FailureCategory { get; init; }
    public IReadOnlyCollection<string> ValidationWarnings { get; init; } = [];
    public MealPlanIntent? ParsedResult { get; init; }
    public AiProviderRuntimeTrace RuntimeTrace { get; init; } = new();
}

public sealed record FoodRecommendationExplanationContext(
    string UserRequestSummary,
    string FoodName,
    string Category,
    decimal CurrentPrice,
    IReadOnlyCollection<string> MatchedIngredients,
    IReadOnlyCollection<string> MatchedTasteAndSpice,
    IReadOnlyCollection<string> MatchedPreparationMethods,
    IReadOnlyCollection<string> MatchedCoursesAndPurposes,
    string? BudgetEvidence,
    string? DistanceEvidence,
    string? RatingEvidence,
    IReadOnlyCollection<string> DietaryEvidence,
    IReadOnlyCollection<string> UnmatchedSoftPreferences,
    IReadOnlyCollection<string> Warnings,
    string ResponseLanguage = "vi");

public sealed class AiGeneratedTextResult
{
    public bool IsSuccess { get; init; }
    public bool UsedFallback { get; init; }
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public string? ProviderRequestId { get; init; }
    public AiProviderFailureCategory FailureCategory { get; init; }
    public IReadOnlyCollection<string> ValidationWarnings { get; init; } = [];
    public string? Text { get; init; }
}

public sealed record FoodRecommendationNormalizationContext(
    string Query,
    decimal? ExplicitMaximumPrice,
    int? ExplicitMaximumDistanceMeters,
    bool HasLocation,
    DomainLayer.Entities.CustomerFoodProfile? CustomerProfile,
    DomainLayer.InterfaceRepository.FoodSemanticCatalogSet Catalogs);

public sealed class FoodRecommendationIntentNormalizationResult
{
    public bool IsValid { get; init; }
    public FoodRecommendationIntent? Intent { get; init; }
    public IReadOnlyCollection<string> Warnings { get; init; } = [];
    public IReadOnlyCollection<string> Errors { get; init; } = [];
}

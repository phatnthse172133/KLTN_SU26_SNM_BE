using DomainLayer.Enums;

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
    public IReadOnlyCollection<string> MealPurposeCodes { get; set; } = [];
    public decimal? MinimumPrice { get; set; }
    public decimal? MaximumPrice { get; set; }
    public bool PreferNearMe { get; set; }
    public int? MaximumDistanceMeters { get; set; }
    public FoodRecommendationSortPreference SortPreference { get; set; } = FoodRecommendationSortPreference.BEST_MATCH;
    public decimal Confidence { get; set; }
    public IReadOnlyCollection<string> Warnings { get; set; } = [];
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
    public bool PreferNearMe { get; set; }
    public int? MaximumDistanceMeters { get; set; }
    public decimal Confidence { get; set; }
    public IReadOnlyCollection<string> Warnings { get; set; } = [];
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

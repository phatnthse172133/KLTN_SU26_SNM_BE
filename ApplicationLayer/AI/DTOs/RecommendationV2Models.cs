using ApplicationLayer.AI.V2.Models;
using DomainLayer.Enums;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.AI.V2.Recommendations;

public sealed class CreateFoodRecommendationV2Request
{
    public string Query { get; set; } = string.Empty;
    public string InputLanguage { get; set; } = "auto";
    public string ResponseLanguage { get; set; } = "vi";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? MaxDistanceMeters { get; set; }
    public decimal? MaximumPrice { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed class RecommendationFeedbackV2Request
{
    public Guid FoodId { get; set; }
    public string Action { get; set; } = string.Empty;
}

public sealed class FoodRecommendationV2Response
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = "SUCCESS";
    public bool UsedProviderFallback { get; set; }
    public UnderstoodFoodRequestResponse UnderstoodRequest { get; set; } = new();
    public IReadOnlyCollection<FoodRecommendationItemResponse> Items { get; set; } = [];
    public IReadOnlyCollection<FoodRecommendationItemResponse> NearMatches { get; set; } = [];
    public RecommendationPagingResponse Paging { get; set; } = new();
    public IReadOnlyCollection<string> Warnings { get; set; } = [];
}

public sealed class UnderstoodFoodRequestResponse
{
    public string InputLanguageHint { get; set; } = "auto";
    public string DetectedLanguage { get; set; } = "vi";
    public string ResponseLanguage { get; set; } = "vi";
    public decimal? LanguageConfidence { get; set; }
    public IReadOnlyCollection<string> LanguageWarnings { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public decimal? MinimumPrice { get; set; }
    public decimal? MaximumPrice { get; set; }
    public bool PreferNearMe { get; set; }
    public int? MaximumDistanceMeters { get; set; }
    public IReadOnlyCollection<string> PreferredIngredients { get; set; } = [];
    public IReadOnlyCollection<string> ExcludedIngredients { get; set; } = [];
    public IReadOnlyCollection<string> DietaryRequirements { get; set; } = [];
    public IReadOnlyCollection<string> AllergenExclusions { get; set; } = [];
    public IReadOnlyCollection<string> TastePreferences { get; set; } = [];
    public IReadOnlyCollection<string> PreparationPreferences { get; set; } = [];
    public IReadOnlyCollection<string> Warnings { get; set; } = [];
}

public sealed class FoodRecommendationItemResponse
{
    public Guid FoodId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal CompatibilityScore { get; set; }
    public string MatchTier { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal? Rating { get; set; }
    public int ReviewCount { get; set; }
    public string? Course { get; set; }
    public bool IsOrderable { get; set; }
    public RecommendationBoothResponse Booth { get; set; } = new();
    public RecommendationMarketResponse Market { get; set; } = new();
}
public sealed class RecommendationBoothResponse { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; }
public sealed class RecommendationMarketResponse { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; public int? DistanceMeters { get; set; } }
public sealed class RecommendationPagingResponse { public int Page { get; set; } public int PageSize { get; set; } public int TotalStrongMatches { get; set; } public int TotalNearMatches { get; set; } }
public sealed class RecommendationFeedbackV2Response { public Guid FeedbackId { get; set; } public Guid SessionId { get; set; } public Guid FoodId { get; set; } public string Action { get; set; } = string.Empty; }

public sealed class FoodRecommendationCandidate
{
    public Guid FoodId { get; init; }
    public string FoodName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public Guid CategoryId { get; init; }
    public string CategoryCode { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public decimal CurrentPrice { get; init; }
    public bool IsAvailable { get; init; }
    public bool IsDeleted { get; init; }
    public bool CategoryDeleted { get; init; }
    public bool CategoryIsActive { get; init; }
    public bool CategoryIsSelectable { get; init; }
    public Guid BoothId { get; init; }
    public string BoothName { get; init; } = string.Empty;
    public BoothStatus BoothStatus { get; init; }
    public TimeOnly? BoothOpenTime { get; init; }
    public TimeOnly? BoothCloseTime { get; init; }
    public Guid MarketId { get; init; }
    public string MarketName { get; init; } = string.Empty;
    public NightMarketStatus MarketStatus { get; init; }
    public ModerationStatus MarketModerationStatus { get; init; }
    public bool MarketDeleted { get; init; }
    public TimeOnly? MarketOpenTime { get; init; }
    public TimeOnly? MarketCloseTime { get; init; }
    public decimal? MarketLatitude { get; init; }
    public decimal? MarketLongitude { get; init; }
    public decimal? Rating { get; init; }
    public int ReviewCount { get; init; }
    public string? SearchText { get; init; }
    public FoodAiProfileStatus? SemanticProfileStatus { get; init; }
    public FoodSpiceLevel SpiceLevel { get; init; }
    public ServingTemperature? ServingTemperature { get; init; }
    public int? EstimatedServingCount { get; init; }
    public bool? IsShareable { get; init; }
    public IReadOnlyCollection<string> IngredientCodes { get; init; } = [];
    public IReadOnlyCollection<CandidateAllergen> Allergens { get; init; } = [];
    public IReadOnlyCollection<CandidateDietaryAttribute> DietaryAttributes { get; init; } = [];
    public IReadOnlyCollection<string> PreparationMethodCodes { get; init; } = [];
    public IReadOnlyCollection<string> TasteCodes { get; init; } = [];
    public IReadOnlyCollection<FoodCourse> Courses { get; init; } = [];
    public IReadOnlyCollection<DiningPurpose> DiningPurposes { get; init; } = [];
}

public sealed record CandidateAllergen(string Code, AllergenDeclarationType DeclarationType, bool IsConfirmed, MetadataSource Source);
public sealed record CandidateDietaryAttribute(string Code, DietarySuitabilityStatus Status, bool IsConfirmed, MetadataSource Source);
public sealed record SemanticMatchResult(decimal Score, IReadOnlyCollection<string> MatchedTerms);

public sealed class RecommendationScoreBreakdown
{
    public decimal SemanticTextScore { get; init; }
    public decimal FoodNameCategoryScore { get; init; }
    public decimal IngredientScore { get; init; }
    public decimal TasteSpiceScore { get; init; }
    public decimal PreparationScore { get; init; }
    public decimal BudgetScore { get; init; }
    public decimal DietaryScore { get; init; }
    public decimal DistanceScore { get; init; }
    public decimal RatingScore { get; init; }
    public decimal CustomerHistoryScore { get; init; }
    public decimal DiversityAdjustment { get; set; }
    public decimal FinalScore { get; init; }
}

public sealed class RecommendationReasonEvidence
{
    public IReadOnlyCollection<string> DesiredTerms { get; init; } = [];
    public IReadOnlyCollection<string> Ingredients { get; init; } = [];
    public IReadOnlyCollection<string> TastesAndSpice { get; init; } = [];
    public IReadOnlyCollection<string> Preparations { get; init; } = [];
    public IReadOnlyCollection<string> CoursesAndPurposes { get; init; } = [];
    public IReadOnlyCollection<string> Dietary { get; init; } = [];
    public string? Budget { get; init; }
    public string? Distance { get; init; }
    public string? Rating { get; init; }
}

public sealed class RankedRecommendationCandidate
{
    public FoodRecommendationCandidate Candidate { get; init; } = null!;
    public RecommendationScoreBreakdown Breakdown { get; init; } = new();
    public RecommendationReasonEvidence Evidence { get; init; } = new();
    public int? DistanceMeters { get; init; }
    public decimal BaseScore => Breakdown.FinalScore;
    public decimal OrderingScore => BaseScore + Breakdown.DiversityAdjustment;
    public RecommendationMatchTier Tier { get; init; }
}

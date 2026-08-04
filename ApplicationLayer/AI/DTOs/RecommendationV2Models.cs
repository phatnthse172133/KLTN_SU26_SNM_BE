using ApplicationLayer.AI.V2.Models;
using DomainLayer.Enums;
using System.Text.Json.Serialization;
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
    public int? MaximumDistanceMeters { get; set; }
    public decimal? LocationAccuracyMeters { get; set; }
    public DateTimeOffset? LocationCapturedAt { get; set; }
    public bool UseDistanceRanking { get; set; } = true;
    public string? SortPreference { get; set; }
    public decimal? MaximumPrice { get; set; }
    public Guid? PreviousSessionId { get; set; }
    public IReadOnlyCollection<string> RemovedIntentSignals { get; set; } = [];
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
    public AiProviderRuntimeTrace ProviderRuntime { get; set; } = new();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RecommendationDiagnosticsResponse? Diagnostics { get; set; }
}

public sealed class RecommendationDiagnosticsResponse
{
    public IReadOnlyCollection<string> DesiredFoodTerms { get; set; } = [];
    public string OriginalNormalizedQuery { get; set; } = string.Empty;
    public int TotalCandidates { get; set; }
    public int EligibleCandidates { get; set; }
    public int FilteredByFoodStatus { get; set; }
    public int FilteredByBoothStatus { get; set; }
    public int FilteredByMarketStatus { get; set; }
    public int FilteredByOpenHours { get; set; }
    public int FilteredByPrice { get; set; }
    public int FilteredByDistance { get; set; }
    public int FilteredByDietaryOrAllergen { get; set; }
    public int RemainingAfterFoodStatus { get; set; }
    public int RemainingAfterBoothStatus { get; set; }
    public int RemainingAfterMarketStatus { get; set; }
    public int RemainingAfterOpenHours { get; set; }
    public int RemainingAfterPrice { get; set; }
    public int RemainingAfterDistance { get; set; }
    public int RemainingAfterDietaryOrAllergen { get; set; }
    public decimal HighestScore { get; set; }
    public int StrongCount { get; set; }
    public int NearCount { get; set; }
    public IReadOnlyDictionary<string, int> TopRejectionReasons { get; set; } = new Dictionary<string, int>();
}

public sealed class UnderstoodFoodRequestResponse
{
    public string InputLanguageHint { get; set; } = "auto";
    public string DetectedLanguage { get; set; } = "vi";
    public string ResponseLanguage { get; set; } = "vi";
    public decimal Confidence { get; set; }
    public bool ClarificationNeeded { get; set; }
    public IReadOnlyCollection<string> Ambiguities { get; set; } = [];
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
    public IReadOnlyCollection<string> AvoidedTasteProfiles { get; set; } = [];
    public IReadOnlyCollection<string> AvoidedPreparationMethods { get; set; } = [];
    public IReadOnlyCollection<string> PreferredCourses { get; set; } = [];
    public IReadOnlyCollection<string> PreferredServingTemperatures { get; set; } = [];
    public IReadOnlyCollection<string> MealPurposes { get; set; } = [];
    public string? SocialContext { get; set; }
    public string? DesiredFullness { get; set; }
    public int? PartySize { get; set; }
    public bool? IsShareablePreferred { get; set; }
    public bool? TakeawayPreferred { get; set; }
    public bool? QuickServicePreferred { get; set; }
    public bool? HealthyPreference { get; set; }
    public bool? FreshPreference { get; set; }
    public bool? PopularityPreference { get; set; }
    public IReadOnlyCollection<string> FreeTextContext { get; set; } = [];
    public IReadOnlyCollection<string> UnmappedTerms { get; set; } = [];
    public IReadOnlyCollection<string> Warnings { get; set; } = [];
    public IReadOnlyCollection<IntentSignalEvidence> SignalEvidence { get; set; } = [];
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
    public int? MarketDistanceMeters { get; set; }
    public bool DistanceAvailable { get; set; }
    public IReadOnlyCollection<string> RankingReasons { get; set; } = [];
    public RecommendationBoothResponse Booth { get; set; } = new();
    public RecommendationMarketResponse Market { get; set; } = new();
}
public sealed class RecommendationBoothResponse { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; }
public sealed class RecommendationMarketResponse { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; public int? DistanceMeters { get; set; } public bool DistanceAvailable { get; set; } }
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
    public decimal FinalScore { get; set; }
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
    public RecommendationMatchTier Tier { get; set; }
}

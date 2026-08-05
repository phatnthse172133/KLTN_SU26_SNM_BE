using DomainLayer.Enums;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.AI.V2.Models;

namespace ApplicationLayer.AI.V2.MealPlans;

public sealed class CreateMealPlanV2Request
{
    public int PartySize { get; set; }
    public decimal Budget { get; set; }
    public string DiningStyle { get; set; } = string.Empty;
    // Request is retained for backwards compatibility. New clients should use
    // NaturalLanguageRequest; both are optional.
    public string? Request { get; set; }
    public string? NaturalLanguageRequest { get; set; }
    public Guid? MarketId { get; set; }
    public string? Scope { get; set; }
    public int RequestedPlanCount { get; set; } = 3;
    public Guid? PreviousSessionId { get; set; }
    public string InputLanguage { get; set; } = "auto";
    public string ResponseLanguage { get; set; } = "vi";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? MaxDistanceMeters { get; set; }
    public int? MaximumDistanceMeters { get; set; }
    public decimal? LocationAccuracyMeters { get; set; }
    public DateTimeOffset? LocationCapturedAt { get; set; }
    public bool UseDistanceRanking { get; set; } = true;
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class ReplaceMealPlanItemRequest { public Guid ReplacementFoodId { get; set; } public int ExpectedPlanVersion { get; set; } }
public sealed class RegenerateMealPlanCourseRequest { public int ExpectedPlanVersion { get; set; } }
public sealed class AddMealPlanToCartRequest { public int ExpectedPlanVersion { get; set; } public string IdempotencyKey { get; set; } = string.Empty; }
public sealed class RefreshMealPlanPricesRequest { public int ExpectedPlanVersion { get; set; } }

public sealed class MealPlanAddToCartResponse
{
    public Guid PlanId { get; set; }
    public int PlanVersion { get; set; }
    public CartResponse Cart { get; set; } = new();
    public IReadOnlyCollection<Guid> AddedFoodItemIds { get; set; } = [];
    public IReadOnlyCollection<Guid> MergedFoodItemIds { get; set; } = [];
    public string IdempotencyResult { get; set; } = "CREATED";
    public string NavigationRoute { get; set; } = "Cart";
}

public sealed class MealPlanPriceChangeDetails
{
    public Guid PlanId { get; set; }
    public int ExpectedPlanVersion { get; set; }
    public decimal OldTotal { get; set; }
    public decimal NewTotal { get; set; }
    public IReadOnlyCollection<MealPlanChangedPriceItem> ChangedItems { get; set; } = [];
}
public sealed class MealPlanChangedPriceItem
{
    public Guid PlanItemId { get; set; }
    public Guid FoodId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public decimal OldUnitPrice { get; set; }
    public decimal NewUnitPrice { get; set; }
    public int Quantity { get; set; }
}
public sealed class MealPlanUnavailableDetails
{
    public IReadOnlyCollection<MealPlanUnavailableItem> Items { get; set; } = [];
}
public sealed class MealPlanUnavailableItem
{
    public Guid PlanItemId { get; set; }
    public Guid? FoodId { get; set; }
    public string Course { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool CanRequestAlternatives { get; set; } = true;
}

public sealed class MealPlanV2Response
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool UsedProviderFallback { get; set; }
    public string Provider { get; set; } = "NEUTRAL";
    public int RequestedPlanCount { get; set; } = 3;
    public int GeneratedPlanCount { get; set; }
    public IReadOnlyCollection<string> Limitations { get; set; } = [];
    public AiProviderRuntimeTrace ProviderRuntime { get; set; } = new();
    public UnderstoodMealPlanRequest UnderstoodRequest { get; set; } = new();
    public IReadOnlyCollection<MealPlanSummaryResponse> Plans { get; set; } = [];
    public IReadOnlyCollection<string> Warnings { get; set; } = [];
}

public sealed class UnderstoodMealPlanRequest
{
    public string InputLanguageHint { get; set; } = "auto";
    public string DetectedLanguage { get; set; } = "vi";
    public string ResponseLanguage { get; set; } = "vi";
    public decimal? LanguageConfidence { get; set; }
    public IReadOnlyCollection<string> LanguageWarnings { get; set; } = [];
    public int PartySize { get; set; }
    public decimal Budget { get; set; }
    public string DiningStyle { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Preferences { get; set; } = [];
    public IReadOnlyCollection<string> Exclusions { get; set; } = [];
    public IReadOnlyCollection<string> Warnings { get; set; } = [];
    public IReadOnlyCollection<IntentSignalEvidence> SignalEvidence { get; set; } = [];
}

public class MealPlanSummaryResponse
{
    public Guid PlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public MealPlanMarketResponse Market { get; set; } = new();
    public int PartySize { get; set; }
    public decimal Budget { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal RemainingBudget { get; set; }
    public decimal BudgetUtilizationPercent { get; set; }
    public decimal ServingCoverage { get; set; }
    public decimal CourseCoverage { get; set; }
    public int FoodCount { get; set; }
    public int BoothCount { get; set; }
    public int? EstimatedServingCount { get; set; }
    public decimal CompatibilityScore { get; set; }
    public decimal DistanceContribution { get; set; }
    public string CompatibilityLabel { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public int Version { get; set; }
}

public sealed class MealPlanDetailResponse : MealPlanSummaryResponse
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Warnings { get; set; } = [];
    public IReadOnlyCollection<MealPlanCourseGroupResponse> CourseGroups { get; set; } = [];
}

public sealed class MealPlanMarketResponse { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; public string? ImageUrl { get; set; } public int? DistanceMeters { get; set; } public bool DistanceAvailable { get; set; } }
public sealed class MealPlanBoothResponse { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; }
public sealed class MealPlanCourseGroupResponse { public string Course { get; set; } = string.Empty; public bool IsRequired { get; set; } public bool IsComplete { get; set; } public IReadOnlyCollection<MealPlanItemResponse> Items { get; set; } = []; }
public sealed class MealPlanItemResponse
{
    public Guid PlanItemId { get; set; }
    public Guid? FoodId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Course { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public decimal TotalPriceSnapshot { get; set; }
    public int? ServingCountSnapshot { get; set; }
    public MealPlanBoothResponse Booth { get; set; } = new();
    public decimal? Rating { get; set; }
    public int ReviewCount { get; set; }
    public decimal CompatibilityScore { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool CanReplace { get; set; }
    public bool CanRemove { get; set; }
    public bool IsCurrentlyOrderable { get; set; }
    public bool HasPriceChanged { get; set; }
    public decimal? CurrentPrice { get; set; }
}

public sealed class MealPlanAlternativePageResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int CurrentPlanVersion { get; set; }
    public IReadOnlyCollection<MealPlanAlternativeResponse> Items { get; set; } = [];
}

public sealed class MealPlanAlternativeResponse
{
    public Guid FoodId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public MealPlanBoothResponse Booth { get; set; } = new();
    public string Course { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal CompatibilityScore { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal ProjectedTotalPrice { get; set; }
    public decimal ProjectedRemainingBudget { get; set; }
    public int? ProjectedServingCount { get; set; }
    public bool WillKeepPlanComplete { get; set; }
    public int CurrentPlanVersion { get; set; }
}

public sealed record MealPlanStylePolicy(MealPlanDiningStyle Style, IReadOnlyCollection<FoodCourse> RequiredCourses,
    IReadOnlyCollection<FoodCourse> OptionalCourses, int MinimumFoods, int MaximumFoods, int MinimumBooths,
    decimal ServingMultiplier, decimal BudgetTarget, bool RequireServingData);

public sealed class MealPlanScoreBreakdown
{
    public decimal FoodCompatibility { get; init; }
    public decimal Completeness { get; init; }
    public decimal ServingAdequacy { get; init; }
    public decimal BudgetUtilization { get; init; }
    public decimal Distance { get; init; }
    public decimal RatingQuality { get; init; }
    public decimal BoothComposition { get; init; }
    public decimal Total { get; init; }
}

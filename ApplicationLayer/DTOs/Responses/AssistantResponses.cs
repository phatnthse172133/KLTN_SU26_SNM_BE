using System.Text.Json.Serialization;
using ApplicationLayer.DTOs;
using DomainLayer.Enums;

namespace ApplicationLayer.DTOs.Responses;

public sealed class CreateAssistantConversationResponse
{
    public Guid Id { get; set; }
    [JsonConverter(typeof(StrictEnumJsonConverter<AssistantConversationStatus>))]
    public AssistantConversationStatus Status { get; set; }
}

public sealed class AssistantTurnResponse
{
    public Guid ConversationId { get; set; }
    [JsonConverter(typeof(StrictEnumJsonConverter<AssistantConversationStatus>))]
    public AssistantConversationStatus Status { get; set; }
    [JsonConverter(typeof(NullableEnumJsonConverter<AssistantIntentKind>))]
    public AssistantIntentKind? Intent { get; set; }
    public string Reply { get; set; } = string.Empty;
    public bool LocationRequired { get; set; }
    public IReadOnlyList<AssistantRecommendationResponse> Recommendations { get; set; } = [];
    public AssistantMealPlanResponse? MealPlan { get; set; }
    public IReadOnlyList<AssistantMealPlanResponse> MealPlans { get; set; } = [];
    public AssistantPreferenceSummaryResponse PreferenceSummary { get; set; } = new();
    public AssistantTurnDiagnostics? Diagnostics { get; set; }
}

public sealed class AssistantTurnDiagnostics
{
    public int EligibleCount { get; set; }
    public int TotalFoodCount { get; set; }
    public int BatchCount { get; set; }
    public IReadOnlyList<Guid> IdsSent { get; set; } = [];
    public IReadOnlyList<Guid> IdsEvaluated { get; set; } = [];
    public long LatencyMs { get; set; }
    public AssistantParsedIntentDiagnostics? ParsedIntent { get; set; }
}

public sealed class AssistantParsedIntentDiagnostics
{
    [JsonConverter(typeof(NullableEnumJsonConverter<AssistantIntentKind>))]
    public AssistantIntentKind? Intent { get; set; }
    public bool NeedsLocation { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public int? PartySize { get; set; }
    public IReadOnlyList<string> HardConstraints { get; set; } = [];
    public IReadOnlyList<string> StructuredPreferences { get; set; } = [];
    public IReadOnlyList<string> SemanticPreferences { get; set; } = [];
    public IReadOnlyList<string> SemanticAvoidances { get; set; } = [];
    public string? DiningContext { get; set; }
    public string? UserGoal { get; set; }
    public string? AdditionalMeaning { get; set; }
}

public sealed class AssistantPreferenceSummaryResponse
{
    public IReadOnlyList<string> HardConstraints { get; set; } = [];
    public IReadOnlyList<string> StructuredPreferences { get; set; } = [];
    public IReadOnlyList<string> SemanticPreferences { get; set; } = [];
    public IReadOnlyList<string> SemanticAvoidances { get; set; } = [];
    public string? DiningContext { get; set; }
    public string? UserGoal { get; set; }
    public string? AdditionalMeaning { get; set; }
    public IReadOnlyList<string> UnknownDataFacets { get; set; } = [];
}

public sealed class AssistantRecommendationResponse
{
    public Guid FoodItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public Guid NightMarketId { get; set; }
    public string NightMarketName { get; set; } = string.Empty;
    public decimal EffectivePrice { get; set; }
    public double? DistanceMeters { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public double FinalScore { get; set; }
    public double SemanticScore { get; set; }
    public IReadOnlyList<string> Reasons { get; set; } = [];
    public IReadOnlyList<string> UnknownDataFacets { get; set; } = [];
    public bool IsFeatured { get; set; }
    public bool HasPromotion { get; set; }
    public bool IsOpenNow { get; set; }
}

public sealed class AssistantMealPlanResponse
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public Guid NightMarketId { get; set; }
    public string NightMarketName { get; set; } = string.Empty;
    public int PartySize { get; set; }
    public decimal? BudgetMax { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal EstimatedTotal { get; set; }
    public decimal? RemainingBudget { get; set; }
    public string? OverallPlanReason { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = [];
    public IReadOnlyList<string> UnknownData { get; set; } = [];
    public IReadOnlyList<AssistantMealPlanSectionResponse> Sections { get; set; } = [];
    public IReadOnlyList<AssistantMealPlanItemResponse> Items { get; set; } = [];
}

public sealed class AssistantMealPlanSectionResponse
{
    public string Course { get; set; } = string.Empty;
    public IReadOnlyList<AssistantMealPlanItemResponse> Items { get; set; } = [];
}

public sealed class AssistantMealPlanItemResponse
{
    public Guid FoodItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Course { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

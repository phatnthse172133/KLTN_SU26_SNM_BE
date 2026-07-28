namespace ApplicationLayer.AI.DTOs;

public class FoodTagResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TagGroup { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsSelectable { get; set; }
    public bool IsPreferenceSelectable { get; set; }
    public bool IsAutoAssigned { get; set; }
}

public class CustomerPreferenceTagResponse
{
    public Guid FoodTagId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class CustomerPreferenceResponse
{
    public IReadOnlyCollection<CustomerPreferenceTagResponse> LikedTags { get; set; } = [];
    public IReadOnlyCollection<CustomerPreferenceTagResponse> AvoidTags { get; set; } = [];
}

public class ParsedFoodIntentResponse
{
    public IReadOnlyCollection<string> MatchedTags { get; set; } = [];
    public IReadOnlyCollection<string> AvoidTags { get; set; } = [];
    public decimal? BudgetMax { get; set; }
    public string? DiningStyle { get; set; }
}

public class FoodDiscoveryItemResponse
{
    public Guid FoodItemId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal BasePrice { get; set; }
    public decimal EffectivePrice { get; set; }
    // Backward-compatible alias for EffectivePrice. New consumers should use
    // BasePrice and EffectivePrice so discount presentation is unambiguous.
    public decimal Price { get; set; }
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public Guid NightMarketId { get; set; }
    public string NightMarketName { get; set; } = string.Empty;
    public string? ZoneName { get; set; }
    public string? BoothSlotCode { get; set; }
    public decimal BoothRating { get; set; }
    public bool CanOrder { get; set; }
    public double? DistanceMeters { get; set; }
    public int MatchScore { get; set; }
    public IReadOnlyCollection<string> MatchedPreferences { get; set; } = [];
    public IReadOnlyCollection<string> MatchedCurrentPreferences { get; set; } = [];
    public IReadOnlyCollection<string> MatchedSavedPreferences { get; set; } = [];
    public bool IsFallback { get; set; }
    public string Reason { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Tags { get; set; } = [];
}

public class FoodDiscoveryResponse
{
    public Guid LogId { get; set; }
    public ParsedFoodIntentResponse ParsedIntent { get; set; } = new();
    public IReadOnlyCollection<FoodDiscoveryItemResponse> Results { get; set; } = [];
}

public class AIHomePromptResponse
{
    public string Label { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string Intent { get; set; } = "FoodDiscovery";
    public int? GroupSize { get; set; }
    public decimal? Budget { get; set; }
}

public class AIHomeTagResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
}

public class AIHomeResponse
{
    public IReadOnlyCollection<AIHomePromptResponse> QuickPrompts { get; set; } = [];
    public IReadOnlyCollection<AIHomeTagResponse> PopularTags { get; set; } = [];
    public IReadOnlyCollection<string> DiningStyles { get; set; } = [];
}

public class DiningPlanItemResponse
{
    public Guid FoodItemId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public string? BoothSlotCode { get; set; }
    public string? ZoneName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class DiningPlanOptionResponse
{
    public string OptionId { get; set; } = string.Empty;
    public string OptionType { get; set; } = string.Empty;
    public string FeasibilityStatus { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public Guid NightMarketId { get; set; }
    public string NightMarketName { get; set; } = string.Empty;
    public int GroupSize { get; set; }
    public int MatchScore { get; set; }
    public decimal EstimatedTotal { get; set; }
    public decimal Budget { get; set; }
    public decimal RemainingBudget { get; set; }
    public double? DistanceMeters { get; set; }
    public IReadOnlyCollection<DiningPlanItemResponse> PlanPreview { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}

public class DiningPlanAssistantResponse
{
    public string Step { get; set; } = "PLAN_OPTIONS";
    public string Message { get; set; } = string.Empty;
    public Guid LogId { get; set; }
    public IReadOnlyCollection<DiningPlanOptionResponse> Options { get; set; } = [];
}

public class DiningPlanReadyResponse
{
    public string Step { get; set; } = "PLAN_READY";
    public Guid NightMarketId { get; set; }
    public string NightMarketName { get; set; } = string.Empty;
    public int GroupSize { get; set; }
    public decimal Budget { get; set; }
    public decimal EstimatedTotal { get; set; }
    public decimal RemainingBudget { get; set; }
    public IReadOnlyCollection<DiningPlanItemResponse> PlanItems { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}

public class AIRecommendationLogResponse
{
    public Guid Id { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? NightMarketId { get; set; }
    public string RecommendationType { get; set; } = string.Empty;
    public string InputJson { get; set; } = string.Empty;
    public string? ParsedIntentJson { get; set; }
    public string ResultJson { get; set; } = string.Empty;
    public string? SelectedOptionId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AIFeedbackResponse
{
    public Guid FeedbackLogId { get; set; }
    public string Message { get; set; } = string.Empty;
}

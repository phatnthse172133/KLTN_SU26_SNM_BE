using ApplicationLayer.Helppers;

namespace ApplicationLayer.DTOs.Responses;

public class PromotionResponse
{
    public Guid Id { get; set; }
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public string? PromotionCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DiscountType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public decimal? MinimumOrderAmount { get; set; }
    public decimal? MaximumDiscountAmount { get; set; }
    public int? TotalUsageLimit { get; set; }
    public int? UsageLimitPerCustomer { get; set; }
    public bool IsPublic { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int UsedCount { get; set; }
    public IReadOnlyCollection<string> ConfigurationWarnings { get; set; } = [];
    public IReadOnlyCollection<PromotionFoodItemResponse> FoodItems { get; set; } = [];
    public IReadOnlyCollection<PromotionCategoryResponse> Categories { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PromotionFoodItemResponse
{
    public Guid FoodItemId { get; set; }
    public string FoodName { get; set; } = string.Empty;
}

public class PromotionCategoryResponse
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

public class PromotionValidationResponse
{
    public Guid PromotionId { get; set; }
    public Guid BoothId { get; set; }
    public string? PromotionCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal EligibleAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal OrderSubtotal { get; set; }
    public decimal EligibleSubtotal { get; set; }
    public decimal CalculatedDiscount { get; set; }
    public decimal ActualDiscount { get; set; }
    public decimal FinalAmount { get; set; }
}

public class AvailablePromotionResponse : PromotionValidationResponse
{
    public string BoothName { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public decimal? MinimumOrderAmount { get; set; }
    public decimal? MaximumDiscountAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class PromotionUsageStatisticsResponse
{
    public Guid PromotionId { get; set; }
    public int ReservedCount { get; set; }
    public int ConsumedCount { get; set; }
    public int ReleasedCount { get; set; }
    public int ActiveUsageCount => ReservedCount + ConsumedCount;
    public decimal TotalDiscountAmount { get; set; }
    public int? TotalUsageLimit { get; set; }
    public int? RemainingUsage { get; set; }
}

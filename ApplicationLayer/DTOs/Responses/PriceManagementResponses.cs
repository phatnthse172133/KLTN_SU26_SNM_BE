namespace ApplicationLayer.DTOs.Responses;

public class FoodPriceResponse
{
    public Guid Id { get; set; }
    public Guid FoodItemId { get; set; }
    public decimal Price { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PackagePriceResponse
{
    public Guid Id { get; set; }
    public Guid PackageId { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PublicPackagePriceResponse
{
    public int DurationDays { get; set; }
    public decimal BasePrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public bool HasPromotion { get; set; }
    public DateTime? PromotionEndDate { get; set; }
}

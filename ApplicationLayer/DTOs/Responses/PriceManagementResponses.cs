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
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

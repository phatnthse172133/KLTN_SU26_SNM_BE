namespace ApplicationLayer.DTOs.Responses;

public class FoodItemResponse
{
    public Guid Id { get; set; }
    public Guid BoothId { get; set; }
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ThumbnailUrl { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsFeatured { get; set; }
    public IReadOnlyCollection<Guid> TagIds { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

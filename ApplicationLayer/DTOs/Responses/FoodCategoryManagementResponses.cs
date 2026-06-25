namespace ApplicationLayer.DTOs.Responses;

public class FoodCategoryResponse
{
    public Guid Id { get; set; }
    public Guid BoothId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

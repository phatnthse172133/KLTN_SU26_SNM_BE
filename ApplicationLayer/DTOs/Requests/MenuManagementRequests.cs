using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Requests;

public class CreateFoodItemRequest
{
    public Guid CategoryId { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(0.01, 999999999)]
    public decimal Price { get; set; }

    [StringLength(500)]
    public string? ThumbnailUrl { get; set; }

    public bool IsAvailable { get; set; } = true;

    public bool IsFeatured { get; set; }
}

public class UpdateFoodItemRequest : CreateFoodItemRequest { }

public class UpdateFoodAvailabilityRequest
{
    public bool IsAvailable { get; set; }
}

public class UpdateFoodFeaturedRequest
{
    public bool IsFeatured { get; set; }
}

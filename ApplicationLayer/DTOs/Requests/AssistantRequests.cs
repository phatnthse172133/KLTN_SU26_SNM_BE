using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Requests;

public sealed class CreateAssistantConversationRequest
{
}

public sealed class SendAssistantMessageRequest
{
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    [Range(1, 100_000)]
    public int? MaxDistanceMeters { get; set; }

    [Range(1, 100)]
    public int? PartySize { get; set; }

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal? Budget { get; set; }
}

public sealed class UpdateAssistantMealPlanItemQuantityRequest
{
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public sealed class ReplaceAssistantMealPlanItemRequest
{
    public Guid ReplacementFoodItemId { get; set; }
}

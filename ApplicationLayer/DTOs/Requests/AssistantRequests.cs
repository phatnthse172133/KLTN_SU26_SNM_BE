using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Requests;

public sealed class CreateAssistantConversationRequest
{
    public Guid? MarketId { get; set; }
}

public sealed class SendAssistantMessageRequest
{
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    public Guid? MarketId { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    [Range(1, 100_000)]
    public int? MaxDistanceMeters { get; set; }
}

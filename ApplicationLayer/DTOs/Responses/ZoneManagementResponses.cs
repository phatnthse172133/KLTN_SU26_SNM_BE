namespace ApplicationLayer.DTOs.Responses;

public class ZoneResponse
{
    public Guid Id { get; set; }
    public Guid NightMarketId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

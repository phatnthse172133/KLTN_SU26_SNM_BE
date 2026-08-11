namespace ApplicationLayer.DTOs.Responses;

public class ZoneResponse
{
    public Guid Id { get; set; }
    public Guid NightMarketId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? ZoneCode { get; set; }
    public int Capacity { get; set; }
    public double DefaultBoothWidth { get; set; }
    public double DefaultBoothHeight { get; set; }
    public double DefaultGap { get; set; }
    public double? WidthMeters { get; set; }
    public double? LengthMeters { get; set; }
    public double? BoothWidthMeters { get; set; }
    public double? BoothLengthMeters { get; set; }
    public double? HorizontalGapMeters { get; set; }
    public double? VerticalGapMeters { get; set; }
    public int AssignedSlotCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

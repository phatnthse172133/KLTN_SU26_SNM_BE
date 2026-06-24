namespace ApplicationLayer.DTOs.Responses;

public class BoothResponse
{
    public Guid Id { get; set; }
    public Guid NightMarketId { get; set; }
    public Guid BoothOwnerId { get; set; }
    public Guid? ZoneId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public string? BoothCode { get; set; }
    public string? Description { get; set; }
    public string? PhoneNumber { get; set; }
    public string? SlotNumber { get; set; }
    public string? ThumbnailUrl { get; set; }
    public decimal? MapPositionX { get; set; }
    public decimal? MapPositionY { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
    public decimal? AverageRating { get; set; }
    public bool IsFeatured { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class NightMarketResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public TimeOnly? OpeningHours { get; set; }
    public TimeOnly? ClosingHours { get; set; }
    public int TotalBooth { get; set; }
    public int? MapWidth { get; set; }
    public int? MapHeight { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Status { get; set; } = string.Empty;
}

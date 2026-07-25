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
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
    public decimal? AverageRating { get; set; }
    public bool IsFeatured { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class BoothNavigationInfoResponse
{
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public Guid NightMarketId { get; set; }
    public string NightMarketName { get; set; } = string.Empty;
    public string NightMarketAddress { get; set; } = string.Empty;
    public GeographicCoordinateResponse? BoothCoordinate { get; set; }
    public GeographicCoordinateResponse? NightMarketCenter { get; set; }
    public GeographicBoundaryResponse? NightMarketBoundary { get; set; }
}

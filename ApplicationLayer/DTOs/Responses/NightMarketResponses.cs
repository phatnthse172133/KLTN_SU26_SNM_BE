namespace ApplicationLayer.DTOs.Responses;

public class NightMarketResponse
{
    public Guid Id { get; set; }
    public Guid? MarketOwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? BoundaryWidthMeters { get; set; }
    public int? BoundaryHeightMeters { get; set; }
    public TimeOnly? OpeningHours { get; set; }
    public TimeOnly? ClosingHours { get; set; }
    public int TotalBooth { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class NightMarketOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class NightMarketNavigationInfoResponse
{
    public Guid NightMarketId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public GeographicCoordinateResponse Destination { get; set; } = new();
    public GeographicBoundaryResponse Boundary { get; set; } = new();
}

public class GeographicCoordinateResponse
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}

public class GeographicBoundaryResponse
{
    public int WidthMeters { get; set; }
    public int HeightMeters { get; set; }
}

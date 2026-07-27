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

public class NightMarketListItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? ThumbnailUrl { get; set; }
    public TimeOnly? OpeningHours { get; set; }
    public TimeOnly? ClosingHours { get; set; }
    public bool IsOpenNow { get; set; }
    public string OpeningStatusText { get; set; } = string.Empty;
    public int ActiveBoothCount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class NightMarketDetailResponse : NightMarketListItemResponse
{
    public string? Description { get; set; }
    public IReadOnlyCollection<string> ImageUrls { get; set; } = [];
    public bool HasLayout { get; set; }
}

public sealed class NightMarketBoothListItemResponse
{
    public Guid Id { get; set; }
    public Guid NightMarketId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? SlotNumber { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
    public bool IsOpenNow { get; set; }
    public decimal? AverageRating { get; set; }
    public bool IsFeatured { get; set; }
}

public sealed class NightMarketFoodListItemResponse
{
    public Guid Id { get; set; }
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ThumbnailUrl { get; set; }
    public bool IsAvailable { get; set; }
    public bool CanOrder { get; set; }
    public bool IsFeatured { get; set; }
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

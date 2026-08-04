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
    public string? LogoUrl { get; set; }
    public decimal? MapPositionX { get; set; }
    public decimal? MapPositionY { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
    public decimal? AverageRating { get; set; }
    public bool IsFeatured { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? NightMarketName { get; set; }
    public string? ZoneName { get; set; }
    public string? BanReason { get; set; }
}

public class BoothImageResponse
{
    public Guid Id { get; set; }
    public Guid BoothId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsCover { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BoothLogoResponse
{
    public Guid BoothId { get; set; }
    public string? LogoUrl { get; set; }
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

public class MarketOwnerBoothResponse
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
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public string? OwnerPhone { get; set; }
    public string? NightMarketName { get; set; }
    public string? ZoneName { get; set; }
    public decimal? AverageRating { get; set; }
    public bool IsFeatured { get; set; }
}

public class BoothOwnerSearchResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool HasBooth { get; set; }
}

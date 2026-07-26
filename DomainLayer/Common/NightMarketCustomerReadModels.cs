using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Common;

public sealed record NightMarketCustomerReadModel(
    Guid Id,
    string Name,
    string? Description,
    string Address,
    decimal? Latitude,
    decimal? Longitude,
    TimeOnly? OpeningHours,
    TimeOnly? ClosingHours,
    string? ThumbnailUrl,
    NightMarketStatus Status,
    int ActiveBoothCount,
    bool HasLayout,
    int? BoundaryWidthMeters,
    int? BoundaryHeightMeters);

public sealed record NightMarketBoothCustomerReadModel(
    Guid Id,
    Guid NightMarketId,
    string Name,
    string? Description,
    string? ThumbnailUrl,
    string? SlotNumber,
    decimal? Latitude,
    decimal? Longitude,
    TimeOnly? OpenTime,
    TimeOnly? CloseTime,
    decimal? AverageRating,
    bool IsFeatured);

public sealed record NightMarketFoodCustomerReadModel(
    Guid Id,
    Guid BoothId,
    string BoothName,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    decimal Price,
    string? ThumbnailUrl,
    bool IsFeatured);

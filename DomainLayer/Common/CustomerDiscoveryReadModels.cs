using DomainLayer.Enums;

namespace DomainLayer.Common;

public sealed record CustomerBoothReadModel(
    Guid Id,
    Guid MarketId,
    string MarketName,
    string MarketAddress,
    string Name,
    string? Description,
    string? PublicPhoneNumber,
    string? ThumbnailUrl,
    string? SlotNumber,
    Guid? ZoneId,
    string? ZoneName,
    TimeOnly? OpenTime,
    TimeOnly? CloseTime,
    TimeOnly? MarketOpenTime,
    TimeOnly? MarketCloseTime,
    bool MarketIsOperational,
    decimal AverageRating,
    int ReviewCount,
    int FoodCount,
    bool IsFeatured,
    IReadOnlyCollection<string> ImageUrls,
    Guid? LayoutId,
    Guid? LayoutNodeId,
    string? LocationSlotNumber,
    Guid? LocationZoneId,
    string? LocationZoneName);

public sealed record CustomerFoodReadModel(
    Guid Id,
    Guid BoothId,
    string BoothName,
    string? BoothThumbnailUrl,
    Guid MarketId,
    string MarketName,
    string MarketAddress,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    decimal BasePrice,
    decimal EffectivePrice,
    string? ThumbnailUrl,
    bool IsAvailable,
    bool IsFeatured,
    TimeOnly? BoothOpenTime,
    TimeOnly? BoothCloseTime,
    TimeOnly? MarketOpenTime,
    TimeOnly? MarketCloseTime,
    bool MarketIsOperational,
    IReadOnlyCollection<string> ImageUrls)
{
    public FoodCourse? PrimaryCourse { get; init; }
    public FoodSpiceLevel SpiceLevel { get; init; }
    public ServingTemperature? ServingTemperature { get; init; }
    public int? EstimatedServingCount { get; init; }
    public string? ServingSizeDescription { get; init; }
    public bool? IsShareable { get; init; }
    public decimal AverageRating { get; init; }
    public int ReviewCount { get; init; }
    public CustomerFoodSemanticReadModel SemanticMetadata { get; init; } = new();
    // Catalog-derived chips for Customer UI. Remaining `Tags` is NOT the removed FoodTag entity.
    public IReadOnlyCollection<CustomerCatalogChipReadModel> Tags { get; init; } = [];
}

public sealed class CustomerFoodSemanticReadModel
{
    public FoodCourse? PrimaryCourse { get; init; }
    public IReadOnlyCollection<FoodCourse> SupportedCourses { get; init; } = [];
    public IReadOnlyCollection<CustomerSemanticCatalogReadModel> Ingredients { get; init; } = [];
    public IReadOnlyCollection<CustomerAllergenReadModel> Allergens { get; init; } = [];
    public IReadOnlyCollection<CustomerDietaryReadModel> Dietary { get; init; } = [];
    public IReadOnlyCollection<CustomerSemanticCatalogReadModel> Preparations { get; init; } = [];
    public IReadOnlyCollection<CustomerSemanticCatalogReadModel> Tastes { get; init; } = [];
}

public sealed record CustomerSemanticCatalogReadModel(Guid Id, string Code, string Name);
public sealed record CustomerAllergenReadModel(Guid Id, string Code, string Name, AllergenDeclarationType DeclarationType, bool IsConfirmed, MetadataSource Source);
public sealed record CustomerDietaryReadModel(Guid Id, string Code, string Name, DietarySuitabilityStatus Status, bool IsConfirmed, MetadataSource Source);

public sealed record CustomerCatalogChipReadModel(
    Guid Id,
    string Code,
    string Name,
    string TagGroup);

namespace ApplicationLayer.DTOs.Responses;
using DomainLayer.Enums;
using ApplicationLayer.DTOs;
using System.Text.Json.Serialization;

public sealed class CustomerMarketSummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

public class CustomerBoothListItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public Guid MarketId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public string? SlotNumber { get; set; }
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public bool IsOpenNow { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int FoodCount { get; set; }
    public bool IsFeatured { get; set; }
}

public sealed class CustomerBoothDetailResponse : CustomerBoothListItemResponse
{
    public string? Description { get; set; }
    public string? PublicPhoneNumber { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
    public IReadOnlyCollection<string> ImageUrls { get; set; } = [];
    public CustomerMarketSummaryResponse Market { get; set; } = new();
    public CustomerBoothLocationResponse? Location { get; set; }
}

public sealed class CustomerBoothLocationResponse
{
    public Guid LayoutId { get; set; }
    public Guid LayoutNodeId { get; set; }
    public string? SlotNumber { get; set; }
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
}

public sealed class CustomerBoothSummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public bool IsOpenNow { get; set; }
}

public class CustomerFoodListItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public bool IsAvailable { get; set; }
    public bool CanOrder { get; set; }
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public Guid MarketId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    [JsonConverter(typeof(NullableEnumJsonConverter<FoodCourse>))] public FoodCourse? PrimaryCourse { get; set; }
    public int? EstimatedServingCount { get; set; }
    public bool? IsShareable { get; set; }
}

public sealed class CustomerFoodDetailResponse : CustomerFoodListItemResponse
{
    public string? Description { get; set; }
    public IReadOnlyCollection<string> ImageUrls { get; set; } = [];
    /// <summary>Deprecated compatibility field derived from normalized metadata.</summary>
    public IReadOnlyCollection<CustomerFoodTagResponse> Tags { get; set; } = [];
    public FoodSemanticMetadataResponse SemanticMetadata { get; set; } = new();
    public CustomerBoothSummaryResponse Booth { get; set; } = new();
    public CustomerMarketSummaryResponse Market { get; set; } = new();
}

public sealed class CustomerFoodTagResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TagGroup { get; set; } = string.Empty;
}

public sealed class CustomerReviewResponse
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public short Rating { get; set; }
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public CustomerReviewReplyResponse? Reply { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CustomerReviewReplyResponse
{
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

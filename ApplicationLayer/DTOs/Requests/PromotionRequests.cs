using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class PromotionListRequest : PaginationReq
{
    [StringLength(200)]
    public string? Keyword { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PromotionStatus? Status { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PromotionScope? Scope { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }
}

public class CreatePromotionRequest
{
    [StringLength(50)]
    public string? PromotionCode { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiscountType DiscountType { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PromotionScope Scope { get; set; }

    [Range(0.01, 999999999)]
    public decimal DiscountValue { get; set; }

    [Range(0, 999999999)]
    public decimal? MinimumOrderAmount { get; set; }

    [Range(0.01, 999999999)]
    public decimal? MaximumDiscountAmount { get; set; }

    [Range(1, int.MaxValue)]
    public int? TotalUsageLimit { get; set; }

    [Range(1, int.MaxValue)]
    public int? UsageLimitPerCustomer { get; set; }

    public bool IsPublic { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public IReadOnlyCollection<Guid> FoodItemIds { get; set; } = [];

    public IReadOnlyCollection<Guid> CategoryIds { get; set; } = [];
}

public class UpdatePromotionRequest : CreatePromotionRequest
{
}

public class ValidateCartPromotionRequest
{
    public Guid BoothId { get; set; }

    [Required, StringLength(50)]
    public string PromotionCode { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class CreateComplaintRequest
{
    // Kept for backward compatibility; the service derives the authoritative booth from OrderId.
    public Guid BoothId { get; set; }
    public Guid OrderId { get; set; }

    // Wire: PascalCase enum name string (matches ComplaintResponse.Category). Integers still accepted.
    // Domain members: FoodQuality, WrongItem, MissingItem, DelayedOrder, BoothBehavior, PaymentIssue, Other
    // (PromotionIssue is legacy-only and rejected in ComplaintService).
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ComplaintCategory Category { get; set; } = ComplaintCategory.Other;

    [StringLength(200)]
    public string? Title { get; set; }

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(5)]
    public List<ComplaintImageRequest> Images { get; set; } = new();
}

public class ComplaintImageRequest
{
    [Required, StringLength(500)]
    public string ImageUrl { get; set; } = string.Empty;
}

public class AddComplaintEvidenceRequest
{
    [MaxLength(5)]
    public List<ComplaintImageRequest> Images { get; set; } = new();
}

public class BoothComplaintResponseRequest
{
    [Required, StringLength(2000, MinimumLength = 10)]
    public string Explanation { get; set; } = string.Empty;

    [MaxLength(5)]
    public List<ComplaintImageRequest> Images { get; set; } = new();
}

public class UpdateComplaintStatusRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ComplaintStatus Status { get; set; }

    [StringLength(2000)]
    public string? AdminResponse { get; set; }

    [StringLength(2000)]
    public string? EvidenceRequestNote { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ComplaintResolutionAction? ResolutionAction { get; set; }

    [StringLength(500)]
    public string? PolicyViolation { get; set; }
}

public class AdminComplaintQueryRequest : PaginationReq
{
    public ComplaintStatus? Status { get; set; }
    public string? Keyword { get; set; }
    public Guid? BoothId { get; set; }
}

public class MarketOwnerComplaintQueryRequest : PaginationReq
{
    public ComplaintStatus? Status { get; set; }
    public Guid? MarketId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

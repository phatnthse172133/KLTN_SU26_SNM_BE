using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class CreateComplaintRequest
{
    // Kept for backward compatibility; the service derives the authoritative booth from OrderId.
    public Guid BoothId { get; set; }
    public Guid OrderId { get; set; }

    [Required, StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(5)]
    public List<ComplaintImageRequest> Images { get; set; } = new();
}

public class ComplaintImageRequest
{
    [Required, Url, StringLength(500)]
    public string ImageUrl { get; set; } = string.Empty;
}

public class UpdateComplaintStatusRequest
{
    public ComplaintStatus Status { get; set; }

    [StringLength(2000, MinimumLength = 10)]
    public string? AdminResponse { get; set; }

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

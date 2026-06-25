using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class CreateComplaintRequest
{
    public Guid BoothId { get; set; }
    public Guid OrderId { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

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

    [StringLength(2000)]
    public string? AdminResponse { get; set; }

    public ComplaintResolutionAction? ResolutionAction { get; set; }

    [StringLength(500)]
    public string? PolicyViolation { get; set; }
}

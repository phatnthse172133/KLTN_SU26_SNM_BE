using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class CreateBoothRegistrationRequest
{
    [Required] public Guid RequestedNightMarketId { get; set; }
    public Guid? PreferredZoneId { get; set; }
    [Required, StringLength(200)] public string BoothName { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [Phone, StringLength(20)] public string? Phone { get; set; }
    [MinLength(1)] public List<BoothDocumentRequest> Documents { get; set; } = [];
}

public class BoothDocumentRequest
{
    [Required] public BoothDocumentType? DocumentType { get; set; }
    [Required, Url, StringLength(500)] public string FileUrl { get; set; } = string.Empty;
}

public class ReviewBoothRegistrationRequest
{
    [Required] public bool Approved { get; set; }
    [StringLength(1000)] public string? RejectReason { get; set; }
}

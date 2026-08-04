using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class ZoneListRequest : PaginationReq
{
    [StringLength(200)]
    public string? Keyword { get; set; }

    public ZoneStatus? Status { get; set; }

    [RegularExpression("(?i)^(name|status|createdAt|updatedAt)$")]
    public string SortBy { get; set; } = "createdAt";

    [RegularExpression("(?i)^(asc|desc)$")]
    public string SortDirection { get; set; } = "desc";
}

public class CreateZoneRequest
{
    [Required, StringLength(100)]
    public string ZoneName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(50)]
    public string? Color { get; set; }

    public ZoneStatus Status { get; set; } = ZoneStatus.Active;
}

public class UpdateZoneRequest : CreateZoneRequest { }

public class UpdateZoneStatusRequest
{
    public ZoneStatus Status { get; set; }
}

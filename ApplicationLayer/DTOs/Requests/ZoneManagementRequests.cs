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

    [StringLength(20)]
    public string? ZoneCode { get; set; }

    [Range(0, 10000)]
    public int Capacity { get; set; } = 0;

    [Range(10, 500)]
    public double DefaultBoothWidth { get; set; } = 80;

    [Range(10, 500)]
    public double DefaultBoothHeight { get; set; } = 60;

    [Range(5, 200)]
    public double DefaultGap { get; set; } = 20;

    public ZoneStatus Status { get; set; } = ZoneStatus.Active;
}

public class UpdateZoneRequest : CreateZoneRequest { }

public class UpdateZoneStatusRequest
{
    public ZoneStatus Status { get; set; }
}

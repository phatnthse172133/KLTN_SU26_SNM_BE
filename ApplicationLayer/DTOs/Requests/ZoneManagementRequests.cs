using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class CreateZoneRequest
{
    public Guid NightMarketId { get; set; }

    [Required, StringLength(100)]
    public string ZoneName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(50)]
    public string? Color { get; set; }

    public ZoneStatus Status { get; set; } = ZoneStatus.Active;
}

public class UpdateZoneRequest : CreateZoneRequest { }

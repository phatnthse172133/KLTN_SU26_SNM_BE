using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class ChangeBoothStatusRequest
{
    [Required]
    [EnumDataType(typeof(BoothStatus))]
    public BoothStatus Status { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 10)]
    public string Reason { get; set; } = string.Empty;

    public DateTime? ExpectedUpdatedAt { get; set; }
}

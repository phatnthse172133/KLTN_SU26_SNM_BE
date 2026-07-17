using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class ChangeUserStatusRequest
{
    [Required]
    [EnumDataType(typeof(UserStatus))]
    public UserStatus Status { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 10)]
    public string Reason { get; set; } = string.Empty;
}

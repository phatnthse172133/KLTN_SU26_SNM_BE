using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class ChangeUserStatusRequest
{
    [Required]
    [EnumDataType(typeof(UserStatus))]
    public UserStatus Status { get; set; }
}

using DomainLayer.Common;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

public class UserDeviceToken : ISoftDelete
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = null!;
    public DevicePlatform Platform { get; set; }
    public string? DeviceId { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public virtual User User { get; set; } = null!;
}

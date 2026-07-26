using System;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

public class UserStatusHistory
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid ChangedByAdminId { get; set; }

    public UserStatus PreviousStatus { get; set; }

    public UserStatus NewStatus { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual User ChangedByAdmin { get; set; } = null!;
}

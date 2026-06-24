using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

// Thông báo đẩy (push notification qua FCM) cho người dùng
public partial class Notification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    // NULL khi thông báo không gắn với gian hàng cụ thể (VD: thông báo hệ thống)
    public Guid? BoothId { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth? Booth { get; set; }

    public virtual User User { get; set; } = null!;
}

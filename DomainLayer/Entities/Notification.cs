using System;
using System.Collections.Generic;
using DomainLayer.Enums;

namespace DomainLayer.Entities;

/// <summary>
/// Thông báo đẩy (push notification qua FCM) cho người dùng
/// </summary>
public partial class Notification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// NULL khi thông báo không gắn với gian hàng cụ thể (VD: thông báo hệ thống)
    /// </summary>
    public Guid? BoothId { get; set; }

    /// <summary>
    /// NewOrder | OrderStatusChanged | NewMessage | Promotion | System
    /// </summary>
    public NotificationType Type { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth? Booth { get; set; }

    public virtual User User { get; set; } = null!;
}

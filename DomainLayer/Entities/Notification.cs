using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DomainLayer.Common;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

// ThÃƒÆ’Ã‚Â´ng bÃƒÆ’Ã‚Â¡o Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚ÂºÃ‚Â©y (push notification qua FCM) cho ngÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã‚Âi dÃƒÆ’Ã‚Â¹ng
public partial class Notification : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? BatchId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public NotificationTarget? Target { get; set; }

    [MaxLength(50)]
    public string? TargetRole { get; set; }

    // NULL khi thÃƒÆ’Ã‚Â´ng bÃƒÆ’Ã‚Â¡o khÃƒÆ’Ã‚Â´ng gÃƒÂ¡Ã‚ÂºÃ‚Â¯n vÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºi gian hÃƒÆ’Ã‚Â ng cÃƒÂ¡Ã‚Â»Ã‚Â¥ thÃƒÂ¡Ã‚Â»Ã†â€™ (VD: thÃƒÆ’Ã‚Â´ng bÃƒÆ’Ã‚Â¡o hÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡ thÃƒÂ¡Ã‚Â»Ã¢â‚¬Ëœng)
    public Guid? BoothId { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public string? DataJson { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth? Booth { get; set; }

    public virtual User User { get; set; } = null!;
}

using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Cuộc trò chuyện giữa 1 khách hàng và 1 chủ gian hàng - dùng SignalR để realtime
/// </summary>
public partial class Conversation
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>
    /// FK tới User – chủ gian hàng (BoothOwner)
    /// </summary>
    public Guid BoothOwnerId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User BoothOwner { get; set; } = null!;

    public virtual User Customer { get; set; } = null!;

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}

using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Cuộc trò chuyện giữa 1 khách hàng và 1 gian hàng - dùng SignalR để realtime
/// </summary>
public partial class Conversation
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Guid BoothId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual User Customer { get; set; } = null!;

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}

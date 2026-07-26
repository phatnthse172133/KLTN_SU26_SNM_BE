using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Cuộc trò chuyện giữa 1 khách hàng và 1 chủ gian hàng - dùng SignalR để realtime
public partial class Conversation
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Guid BoothOwnerId { get; set; }

    public Guid? LastMessageId { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public DateTime? CustomerLastReadAt { get; set; }

    public DateTime? BoothOwnerLastReadAt { get; set; }

    public ConversationStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User BoothOwner { get; set; } = null!;

    public virtual User Customer { get; set; } = null!;

    public virtual Message? LastMessage { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}

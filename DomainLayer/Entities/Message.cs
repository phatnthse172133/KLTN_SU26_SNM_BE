using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Tin nhắn trong cuộc trò chuyện - truyền tải qua SignalR Hub
/// </summary>
public partial class Message
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid SenderId { get; set; }

    /// <summary>
    /// Snapshot vai trò người gửi: Customer | BoothOwner
    /// </summary>
    public string SenderRole { get; set; } = null!;

    /// <summary>
    /// Text | Image | System
    /// </summary>
    public string MessageType { get; set; } = null!;

    public string Content { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}

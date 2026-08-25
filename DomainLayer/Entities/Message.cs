using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Tin nhắn trong cuộc trò chuyện - truyền tải qua SignalR Hub
public partial class Message
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid SenderId { get; set; }

    public Guid? ClientMessageId { get; set; }

    // Snapshot vai trò người gửi: Customer | BoothOwner
    public ConversationParticipantRole SenderRole { get; set; }

    // Text | Image | System | File
    public MessageType Type { get; set; }

    public string Content { get; set; } = null!;

    /// <summary>Public URL for the single optional attachment (Image or File).</summary>
    public string? AttachmentUrl { get; set; }

    /// <summary>Original client file name (sanitized for display only).</summary>
    public string? AttachmentName { get; set; }

    public string? AttachmentMimeType { get; set; }

    public long? AttachmentSize { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}

using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Responses;

public class ConversationUserResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class ConversationBoothResponse
{
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
}

public class MessageResponse
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public Guid? ClientMessageId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatarUrl { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ConversationResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid BoothOwnerId { get; set; }
    public Guid BoothId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastMessageAt { get; set; }
    public MessageResponse? LastMessage { get; set; }
    public ConversationUserResponse Customer { get; set; } = new();
    public ConversationUserResponse BoothOwner { get; set; } = new();
    public ConversationBoothResponse Booth { get; set; } = new();
    public int UnreadCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

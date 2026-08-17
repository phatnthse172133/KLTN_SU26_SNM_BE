using DomainLayer.Enums;

namespace DomainLayer.Entities;

public sealed class AssistantMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public AssistantMessageRole Role { get; set; }
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public AssistantConversation Conversation { get; set; } = null!;
}

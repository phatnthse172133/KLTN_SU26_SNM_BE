using ApplicationLayer.DTOs.Responses;

namespace ApplicationLayer.Services.Chats;

public interface IRealtimeChatPublisher
{
    /// <summary>
    /// Delivers MessageCreated only to the recipient's personal chat inbox
    /// (<c>chat-user:{recipientUserId}</c>). Does not target conversation groups.
    /// </summary>
    Task PublishMessageCreatedAsync(
        Guid conversationId,
        Guid recipientUserId,
        MessageResponse message,
        CancellationToken cancellationToken = default);

    Task PublishMessageDeletedAsync(
        Guid conversationId,
        Guid messageId,
        Guid deletedByUserId,
        DateTime deletedAt,
        CancellationToken cancellationToken = default);

    Task PublishConversationReadAsync(
        Guid conversationId,
        Guid readerId,
        DateTime readAt,
        CancellationToken cancellationToken = default);
}

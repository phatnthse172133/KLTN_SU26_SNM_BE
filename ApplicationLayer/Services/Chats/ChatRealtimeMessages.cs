using ApplicationLayer.DTOs.Responses;

namespace ApplicationLayer.Services.Chats;

public interface IRealtimeChatPublisher
{
    Task PublishMessageCreatedAsync(
        Guid conversationId,
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

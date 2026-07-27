using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IMessageRepository : IGenericRepository<Message>
{
    Task<Message?> GetByClientMessageIdAsync(
        Guid senderId,
        Guid clientMessageId,
        CancellationToken cancellationToken = default);

    Task<Message?> GetOwnedAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Message>> GetPagedByConversationAsync(
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> CountUnreadByConversationIdsAsync(
        IReadOnlyCollection<Guid> conversationIds,
        Guid readerId,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(
        Guid conversationId,
        Guid readerId,
        CancellationToken cancellationToken = default);

    Task<Message?> GetLatestVisibleByConversationAsync(
        Guid conversationId,
        Guid? excludingMessageId = null,
        CancellationToken cancellationToken = default);

    Task<int> MarkConversationMessagesReadAsync(
        Guid conversationId,
        Guid readerId,
        DateTime readAt,
        CancellationToken cancellationToken = default);
}

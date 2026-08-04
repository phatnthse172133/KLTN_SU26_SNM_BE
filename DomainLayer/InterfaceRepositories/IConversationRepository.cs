using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IConversationRepository : IGenericRepository<Conversation>
{
    Task<Conversation?> GetByCustomerAndBoothAsync(
        Guid customerId,
        Guid boothId,
        CancellationToken cancellationToken = default);

    Task<(Conversation Conversation, bool Created)> GetOrCreateCustomerBoothAsync(
        Guid customerId,
        Guid boothId,
        DateTime now,
        CancellationToken cancellationToken = default);

    Task<Conversation?> GetOwnedAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Conversation?> GetOwnedWithUsersAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Conversation>> GetPagedByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

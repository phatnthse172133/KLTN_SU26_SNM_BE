using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IConversationRepository : IGenericRepository<Conversation>
{
    Task<Conversation?> GetByParticipantsAsync(
        Guid customerId,
        Guid boothOwnerId,
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

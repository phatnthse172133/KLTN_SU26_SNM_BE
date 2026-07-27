using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class MessageRepository : GenericRepository<Message>, IMessageRepository
{
    public MessageRepository(SNMDbContext context) : base(context)
    {
    }

    public Task<Message?> GetByClientMessageIdAsync(
        Guid senderId,
        Guid clientMessageId,
        CancellationToken cancellationToken = default)
        => _dbSet
            .Include(message => message.Sender)
            .FirstOrDefaultAsync(
                message => message.SenderId == senderId
                    && message.ClientMessageId == clientMessageId,
                cancellationToken);

    public Task<Message?> GetOwnedAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _dbSet
            .Include(message => message.Conversation)
            .Include(message => message.Sender)
            .FirstOrDefaultAsync(
                message => message.Id == messageId
                    && message.DeletedAt == null
                    && (message.Conversation.CustomerId == userId
                        || message.Conversation.Booth.BoothOwnerId == userId),
                cancellationToken);

    public async Task<PagedResult<Message>> GetPagedByConversationAsync(
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(message => message.Sender)
            .Where(message => message.ConversationId == conversationId
                && message.DeletedAt == null);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Message>(items, total);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountUnreadByConversationIdsAsync(
        IReadOnlyCollection<Guid> conversationIds,
        Guid readerId,
        CancellationToken cancellationToken = default)
    {
        if (conversationIds.Count == 0)
            return new Dictionary<Guid, int>();

        return await _dbSet
            .AsNoTracking()
            .Where(message => conversationIds.Contains(message.ConversationId)
                && message.SenderId != readerId
                && !message.IsRead
                && message.DeletedAt == null)
            .GroupBy(message => message.ConversationId)
            .Select(group => new { ConversationId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ConversationId, item => item.Count, cancellationToken);
    }

    public Task<int> CountUnreadAsync(
        Guid conversationId,
        Guid readerId,
        CancellationToken cancellationToken = default)
        => _dbSet.CountAsync(message => message.ConversationId == conversationId
            && message.SenderId != readerId
            && message.DeletedAt == null
            && !message.IsRead,
            cancellationToken);

    public Task<Message?> GetLatestVisibleByConversationAsync(
        Guid conversationId,
        Guid? excludingMessageId = null,
        CancellationToken cancellationToken = default)
        => _dbSet
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId
                && (!excludingMessageId.HasValue || message.Id != excludingMessageId.Value)
                && message.DeletedAt == null)
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> MarkConversationMessagesReadAsync(
        Guid conversationId,
        Guid readerId,
        DateTime readAt,
        CancellationToken cancellationToken = default)
        => _dbSet
            .Where(message => message.ConversationId == conversationId
                && message.SenderId != readerId
                && message.DeletedAt == null
                && !message.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.IsRead, true)
                .SetProperty(message => message.ReadAt, readAt)
                .SetProperty(message => message.UpdatedAt, readAt),
                cancellationToken);
}

using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class ConversationRepository : GenericRepository<Conversation>, IConversationRepository
{
    public ConversationRepository(SNMDbContext context) : base(context)
    {
    }

    public Task<Conversation?> GetByParticipantsAsync(
        Guid customerId,
        Guid boothOwnerId,
        CancellationToken cancellationToken = default)
        => _dbSet
            .Include(conversation => conversation.Customer)
            .Include(conversation => conversation.BoothOwner)
            .Include(conversation => conversation.LastMessage)
            .FirstOrDefaultAsync(
                conversation => conversation.CustomerId == customerId
                    && conversation.BoothOwnerId == boothOwnerId,
                cancellationToken);

    public Task<Conversation?> GetOwnedAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(
            conversation => conversation.Id == conversationId
                && (conversation.CustomerId == userId || conversation.BoothOwnerId == userId),
            cancellationToken);

    public Task<Conversation?> GetOwnedWithUsersAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _dbSet
            .Include(conversation => conversation.Customer)
            .Include(conversation => conversation.BoothOwner)
            .Include(conversation => conversation.LastMessage)
            .FirstOrDefaultAsync(
                conversation => conversation.Id == conversationId
                    && (conversation.CustomerId == userId || conversation.BoothOwnerId == userId),
                cancellationToken);

    public async Task<PagedResult<Conversation>> GetPagedByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(conversation => conversation.Customer)
            .Include(conversation => conversation.BoothOwner)
            .Include(conversation => conversation.LastMessage)
            .Where(conversation => conversation.CustomerId == userId
                || conversation.BoothOwnerId == userId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(conversation => conversation.LastMessageAt ?? conversation.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Conversation>(items, total);
    }
}

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

    public Task<Conversation?> GetByCustomerAndBoothAsync(
        Guid customerId,
        Guid boothId,
        CancellationToken cancellationToken = default)
        => _dbSet
            .Include(conversation => conversation.Customer)
            .Include(conversation => conversation.Booth)
                .ThenInclude(booth => booth.BoothOwner)
            .Include(conversation => conversation.LastMessage)
            .FirstOrDefaultAsync(
                conversation => conversation.CustomerId == customerId
                    && conversation.BoothId == boothId,
                cancellationToken);

    public async Task<(Conversation Conversation, bool Created)> GetOrCreateCustomerBoothAsync(
        Guid customerId,
        Guid boothId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var conversationId = Guid.NewGuid();
        const string activeStatus = "Active";
        var affected = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Conversations"
                ("Id", "CustomerId", "BoothId", "Status", "CreatedAt", "UpdatedAt")
            VALUES
                ({conversationId}, {customerId}, {boothId}, {activeStatus}, {now}, {now})
            ON CONFLICT ("CustomerId", "BoothId") DO NOTHING
            """,
            cancellationToken);

        var conversation = await GetByCustomerAndBoothAsync(
            customerId,
            boothId,
            cancellationToken) ?? throw new InvalidOperationException(
                "Conversation insert completed but the row could not be loaded.");

        return (conversation, affected == 1);
    }

    public Task<Conversation?> GetOwnedAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(
            conversation => conversation.Id == conversationId
                && (conversation.CustomerId == userId || conversation.Booth.BoothOwnerId == userId),
            cancellationToken);

    public Task<Conversation?> GetOwnedWithUsersAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _dbSet
            .Include(conversation => conversation.Customer)
            .Include(conversation => conversation.Booth)
                .ThenInclude(booth => booth.BoothOwner)
            .Include(conversation => conversation.LastMessage)
            .FirstOrDefaultAsync(
                conversation => conversation.Id == conversationId
                    && (conversation.CustomerId == userId || conversation.Booth.BoothOwnerId == userId),
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
            .Include(conversation => conversation.Booth)
                .ThenInclude(booth => booth.BoothOwner)
            .Include(conversation => conversation.LastMessage)
            .Where(conversation => conversation.CustomerId == userId
                || conversation.Booth.BoothOwnerId == userId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(conversation => conversation.LastMessageAt ?? conversation.CreatedAt)
            .ThenByDescending(conversation => conversation.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Conversation>(items, total);
    }
}

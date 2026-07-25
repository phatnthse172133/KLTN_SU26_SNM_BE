using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
{
    public NotificationRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<Notification>> GetPagedByUserAsync(
        Guid userId,
        NotificationType? type,
        bool? isRead,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = ActiveQuery()
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);

        if (type.HasValue)
            query = query.Where(notification => notification.Type == type.Value);

        if (isRead.HasValue)
            query = query.Where(notification => notification.IsRead == isRead.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Notification>(items, total);
    }

    public Task<Notification?> GetByUserAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => ActiveQuery().FirstOrDefaultAsync(
            notification => notification.Id == notificationId
                && notification.UserId == userId,
            cancellationToken);

    public Task<int> CountUnreadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => ActiveQuery().CountAsync(
            notification => notification.UserId == userId && !notification.IsRead,
            cancellationToken);

    public Task<int> MarkAllAsReadAsync(
        Guid userId,
        DateTime readAt,
        CancellationToken cancellationToken = default)
        => ActiveQuery()
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(notification => notification.IsRead, true)
                    .SetProperty(notification => notification.ReadAt, readAt)
                    .SetProperty(notification => notification.UpdatedAt, readAt),
                cancellationToken);

    public async Task<PagedResult<Notification>> GetAdminPagedBatchesAsync(
        string? keyword,
        NotificationTarget? target,
        string? role,
        NotificationType? type,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = ActiveQuery()
            .AsNoTracking()
            .Where(x => x.BatchId != null);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(k) || x.Content.ToLower().Contains(k));
        }

        if (target.HasValue)
            query = query.Where(x => x.Target == target.Value);

        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.Trim();
            query = query.Where(x => x.TargetRole == r);
        }

        if (type.HasValue)
            query = query.Where(x => x.Type == type.Value);

        if (fromDate.HasValue)
            query = query.Where(x => x.CreatedAt >= fromDate.Value.ToUniversalTime());

        if (toDate.HasValue)
        {
            var endOfDay = toDate.Value.Date.AddDays(1).ToUniversalTime();
            query = query.Where(x => x.CreatedAt < endOfDay);
        }

        var batchIdsQuery = query
            .Where(x => x.BatchId != null)
            .Select(x => x.BatchId)
            .Distinct();

        var total = await batchIdsQuery.CountAsync(cancellationToken);

        var batchStats = await query
            .Where(x => x.BatchId != null)
            .Select(x => new { x.BatchId, x.CreatedAt })
            .Distinct()
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var pagedBatchIds = batchStats.Select(x => x.BatchId).ToList();

        var representativeData = await ActiveQuery()
            .AsNoTracking()
            .Where(x => pagedBatchIds.Contains(x.BatchId))
            .Select(x => new
            {
                x.BatchId,
                x.Title,
                x.Content,
                x.Type,
                x.Target,
                x.TargetRole,
                x.CreatedAt,
                x.CreatedByUserId,
                UserId = x.Target == NotificationTarget.SpecificUser ? x.UserId : Guid.Empty
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        var groupedItems = representativeData.Select(x => new Notification
        {
            BatchId = x.BatchId,
            Title = x.Title,
            Content = x.Content,
            Type = x.Type,
            Target = x.Target,
            TargetRole = x.TargetRole,
            CreatedAt = x.CreatedAt,
            CreatedByUserId = x.CreatedByUserId,
            UserId = x.UserId
        }).ToList();

        var sortedItems = pagedBatchIds
            .Select(bid => groupedItems.First(x => x.BatchId == bid))
            .ToList();

        return new PagedResult<Notification>(sortedItems, total);
    }

    public async Task<Notification?> GetAdminBatchDetailAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        return await ActiveQuery()
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.BatchId == batchId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, int>> GetBatchRecipientCountsAsync(
        List<Guid> batchIds,
        CancellationToken cancellationToken = default)
    {
        var counts = await ActiveQuery()
            .Where(x => x.BatchId.HasValue && batchIds.Contains(x.BatchId.Value))
            .GroupBy(x => x.BatchId)
            .Select(g => new { BatchId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, int>();
        foreach (var c in counts)
        {
            if (c.BatchId.HasValue)
                result[c.BatchId.Value] = c.Count;
        }
        return result;
    }

    public async Task<int> CountByBatchIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        return await ActiveQuery()
            .CountAsync(x => x.BatchId == batchId, cancellationToken);
    }
}

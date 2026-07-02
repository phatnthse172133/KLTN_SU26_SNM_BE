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
}

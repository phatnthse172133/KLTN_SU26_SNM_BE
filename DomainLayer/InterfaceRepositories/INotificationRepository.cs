using DomainLayer.Common;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface INotificationRepository : IGenericRepository<Notification>
{
    Task<PagedResult<Notification>> GetPagedByUserAsync(
        Guid userId,
        NotificationType? type,
        bool? isRead,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Notification?> GetByUserAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> MarkAllAsReadAsync(
        Guid userId,
        DateTime readAt,
        CancellationToken cancellationToken = default);
}

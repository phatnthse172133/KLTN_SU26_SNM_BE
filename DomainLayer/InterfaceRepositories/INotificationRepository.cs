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

    Task<int> MarkReferenceAsReadAsync(
        Guid userId,
        string referenceType,
        Guid referenceId,
        DateTime readAt,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Notification>> GetAdminPagedBatchesAsync(
        string? keyword,
        NotificationTarget? target,
        string? role,
        NotificationType? type,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Notification?> GetAdminBatchDetailAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, int>> GetBatchRecipientCountsAsync(
        List<Guid> batchIds,
        CancellationToken cancellationToken = default);

    Task<int> CountByBatchIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);
}

using ApplicationLayer.DTOs.Responses;

namespace ApplicationLayer.Services.Notifications;

public interface IRealtimeNotificationPublisher
{
    Task PublishAsync(Guid userId, NotificationListItemResponse notification, int unreadCount, CancellationToken cancellationToken = default);

    Task PublishUnreadCountAsync(Guid userId, int unreadCount, CancellationToken cancellationToken = default);
}

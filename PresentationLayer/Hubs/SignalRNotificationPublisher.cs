using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace PresentationLayer.Hubs;

public class SignalRNotificationPublisher : IRealtimeNotificationPublisher
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotificationPublisher(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public async Task PublishAsync(
        Guid userId,
        NotificationListItemResponse notification,
        int unreadCount,
        CancellationToken cancellationToken = default)
    {
        var clients = _hub.Clients.Group(NotificationHub.GroupName(userId));
        await clients.SendAsync(
            "ReceiveNotification",
            notification,
            cancellationToken);
        await clients.SendAsync(
            "NotificationUnreadCountUpdated",
            new { UnreadCount = unreadCount },
            cancellationToken);
    }

    public Task PublishUnreadCountAsync(
        Guid userId,
        int unreadCount,
        CancellationToken cancellationToken = default)
        => _hub.Clients
            .Group(NotificationHub.GroupName(userId))
            .SendAsync(
                "NotificationUnreadCountUpdated",
                new { UnreadCount = unreadCount },
                cancellationToken);
}

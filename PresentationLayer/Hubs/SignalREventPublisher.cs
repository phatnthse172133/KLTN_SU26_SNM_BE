using ApplicationLayer.Services.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace PresentationLayer.Hubs;

/// <summary>
/// Unified SignalR event publisher.  Routes events to the correct hub
/// and group based on the <see cref="RealtimeEvent"/> metadata.
///
/// All events are sent through a single generic hub method ("RealtimeEvent")
/// so that every frontend client can listen on one method regardless
/// of the domain (Notification, Chat, Order, Support, …).
/// </summary>
public class SignalREventPublisher : IRealtimeEventPublisher
{
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly IHubContext<ChatHub> _chatHub;
    private readonly ILogger<SignalREventPublisher> _logger;

    public SignalREventPublisher(
        IHubContext<NotificationHub> notificationHub,
        IHubContext<ChatHub> chatHub,
        ILogger<SignalREventPublisher> logger)
    {
        _notificationHub = notificationHub;
        _chatHub = chatHub;
        _logger = logger;
    }

    public async Task PublishAsync(RealtimeEvent evt, CancellationToken cancellationToken = default)
    {
        // Send to ALL applicable targets independently.
        // GroupName, Role, and RecipientId are NOT mutually exclusive —
        // an event may need to reach both a group (e.g. support-ticket:{id})
        // and a direct user (e.g. the requester) at the same time.
        //
        // Each target is wrapped in its own try/catch so that a failure
        // in one delivery path does NOT block the others.
        var sent = false;

        // 1. Group-targeted (conversation goes through ChatHub, others through NotificationHub)
        if (!string.IsNullOrWhiteSpace(evt.GroupName))
        {
            try
            {
                if (evt.GroupName.StartsWith("conversation:", StringComparison.Ordinal))
                    await _chatHub.Clients.Group(evt.GroupName).SendAsync("RealtimeEvent", evt, cancellationToken);
                else
                    await _notificationHub.Clients.Group(evt.GroupName).SendAsync("RealtimeEvent", evt, cancellationToken);
                sent = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send realtime event {EventType} {EventId} to group {GroupName}.",
                    evt.EventType, evt.EventId, evt.GroupName);
            }
        }

        // 2. Role-based broadcast (always through NotificationHub)
        if (!string.IsNullOrWhiteSpace(evt.Role))
        {
            try
            {
                await _notificationHub.Clients
                    .Group(RealtimeGroups.Role(evt.Role))
                    .SendAsync("RealtimeEvent", evt, cancellationToken);
                sent = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send realtime event {EventType} {EventId} to role {Role}.",
                    evt.EventType, evt.EventId, evt.Role);
            }
        }

        // 3. Direct user — NotificationHub personal inbox.
        // Chat MessageCreated is owned by SignalRChatPublisher → chat-user:{recipient}
        // and must not be fan-out again from this publisher.
        if (evt.RecipientId.HasValue)
        {
            try
            {
                await _notificationHub.Clients
                    .Group(RealtimeGroups.User(evt.RecipientId.Value))
                    .SendAsync("RealtimeEvent", evt, cancellationToken);
                sent = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send realtime event {EventType} {EventId} to user {UserId}.",
                    evt.EventType, evt.EventId, evt.RecipientId.Value);
            }
        }

        if (!sent)
        {
            _logger.LogWarning(
                "Realtime event {EventType} {EventId} has no target (no GroupName, Role, or RecipientId).",
                evt.EventType, evt.EventId);
        }
    }

    public async Task PublishToManyAsync(
        IEnumerable<Guid> recipientIds,
        string eventType,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var recipientId in recipientIds)
        {
            await PublishAsync(new RealtimeEvent
            {
                EventId = Guid.NewGuid(),
                EventType = eventType,
                OccurredAt = now,
                RecipientId = recipientId,
                Payload = payload
            }, cancellationToken);
        }
    }

}

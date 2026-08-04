namespace ApplicationLayer.Services.Realtime;

/// <summary>
/// Unified realtime event publisher.  All SignalR pushes go through this
/// interface so that event shape, deduplication, and group routing are
/// consistent across Notification, Chat, Order, Support, Layout, and
/// Subscription domains.
/// </summary>
public interface IRealtimeEventPublisher
{
    /// <summary>
    /// Publish a single event to a user, group, or role as specified by
    /// <paramref name="evt"/>.
    /// </summary>
    Task PublishAsync(RealtimeEvent evt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish the same event to multiple recipients (convenience overload).
    /// Each recipient gets its own eventId copy.
    /// </summary>
    Task PublishToManyAsync(
        IEnumerable<Guid> recipientIds,
        string eventType,
        object? payload = null,
        CancellationToken cancellationToken = default);
}

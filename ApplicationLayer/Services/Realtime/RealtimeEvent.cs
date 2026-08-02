using System.Text.Json.Serialization;

namespace ApplicationLayer.Services.Realtime;

/// <summary>
/// Unified realtime event contract used across all SignalR hubs.
/// Every event carries a unique eventId for client-side deduplication.
/// </summary>
public sealed class RealtimeEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public string EventType { get; init; } = string.Empty;

    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    public Guid? RecipientId { get; init; }

    /// <summary>
    /// Target group name. When set, the event is sent to this SignalR group
    /// instead of a single user.  e.g. "market:{marketId}", "conversation:{conversationId}".
    /// </summary>
    public string? GroupName { get; init; }

    /// <summary>
    /// Optional role filter. When set, the event is sent to all users in that role.
    /// </summary>
    public string? Role { get; init; }

    public object? Payload { get; init; }
}

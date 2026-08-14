namespace ApplicationLayer.Services.Realtime;

/// <summary>
/// Centralised SignalR group naming conventions.
/// All realtime publishers must use these helpers to guarantee
/// consistent group names across hubs.
/// </summary>
public static class RealtimeGroups
{
    // Keep the established notification group name so upgraded publishers are
    // compatible with clients connected through the existing hub.
    public static string User(Guid userId) => $"notification:{userId}";
    /// <summary>Personal ChatHub inbox so participants receive messages without joining each conversation group.</summary>
    public static string ChatUser(Guid userId) => $"chat-user:{userId}";
    public static string Conversation(Guid conversationId) => $"conversation:{conversationId}";
    public static string Market(Guid marketId) => $"market:{marketId}";
    public static string Layout(Guid layoutId) => $"layout:{layoutId}";
    public static string Booth(Guid boothId) => $"booth:{boothId}";
    public static string SupportTicket(Guid ticketId) => $"support-ticket:{ticketId}";
    public static string Role(string role) => $"role:{role}";
}

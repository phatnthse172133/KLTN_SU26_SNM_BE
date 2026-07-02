using System.Collections.Concurrent;

namespace ApplicationLayer.Services.Notifications;

public class OnlinePresenceService : IOnlinePresenceService
{
    private readonly ConcurrentDictionary<Guid, int> _connectionCounts = new();

    public void Connected(Guid userId)
        => _connectionCounts.AddOrUpdate(userId, 1, (_, count) => count + 1);

    public void Disconnected(Guid userId)
    {
        while (_connectionCounts.TryGetValue(userId, out var count))
        {
            if (count <= 1)
            {
                if (_connectionCounts.TryRemove(
                    new KeyValuePair<Guid, int>(userId, count)))
                    return;
            }
            else if (_connectionCounts.TryUpdate(userId, count - 1, count))
            {
                return;
            }
        }
    }

    public bool IsOnline(Guid userId)
        => _connectionCounts.TryGetValue(userId, out var count) && count > 0;
}

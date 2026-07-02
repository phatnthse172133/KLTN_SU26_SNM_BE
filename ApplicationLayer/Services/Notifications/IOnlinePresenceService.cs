namespace ApplicationLayer.Services.Notifications;

public interface IOnlinePresenceService
{
    void Connected(Guid userId);
    void Disconnected(Guid userId);
    bool IsOnline(Guid userId);
}

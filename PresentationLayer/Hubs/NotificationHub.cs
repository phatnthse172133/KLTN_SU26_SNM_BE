using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ApplicationLayer.Services.Notifications;

namespace PresentationLayer.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly IOnlinePresenceService _presence;

    public NotificationHub(IOnlinePresenceService presence)
    {
        _presence = presence;
    }

    public override async Task OnConnectedAsync()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GroupName(userId));
        _presence.Connected(userId);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var userId))
            _presence.Disconnected(userId);

        return base.OnDisconnectedAsync(exception);
    }

    internal static string GroupName(Guid userId) => $"notification:{userId}";
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Realtime;
using DomainLayer.InterfaceRepository;
using DomainLayer.InterfaceRepositories;

namespace PresentationLayer.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly IOnlinePresenceService _presence;
    private readonly INightMarketRepository _nightMarketRepo;
    private readonly IBoothRepository _boothRepo;
    private readonly IMarketLayoutRepository _layoutRepo;
    private readonly ISupportTicketRepository _supportTicketRepo;

    public NotificationHub(
        IOnlinePresenceService presence,
        INightMarketRepository nightMarketRepo,
        IBoothRepository boothRepo,
        IMarketLayoutRepository layoutRepo,
        ISupportTicketRepository supportTicketRepo)
    {
        _presence = presence;
        _nightMarketRepo = nightMarketRepo;
        _boothRepo = boothRepo;
        _layoutRepo = layoutRepo;
        _supportTicketRepo = supportTicketRepo;
    }

    public override async Task OnConnectedAsync()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
        {
            Context.Abort();
            return;
        }

        // Join the unified user group so all domain events reach this connection
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.User(userId));

        // Also join role-based group so role broadcasts work
        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        if (!string.IsNullOrWhiteSpace(role))
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                RealtimeGroups.Role(role));

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

    // ─── Server-side join with authorization ───

    /// <summary>
    /// Join the market group.  Market Owner can only join their own markets;
    /// Admin can join any market.
    /// </summary>
    public async Task JoinMarket(Guid marketId)
    {
        var userId = CurrentUserId();
        if (IsAdmin())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Market(marketId), Context.ConnectionAborted);
            return;
        }

        var market = await _nightMarketRepo.GetActiveByIdAsync(marketId, Context.ConnectionAborted);
        if (market is null || market.MarketOwnerId != userId)
            throw new HubException("You are not authorized to join this market group.");

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Market(marketId), Context.ConnectionAborted);
    }

    public Task LeaveMarket(Guid marketId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Market(marketId), Context.ConnectionAborted);

    /// <summary>
    /// Join the layout group.  Market Owner can only join layouts belonging to their markets;
    /// Admin can join any layout.
    /// </summary>
    public async Task JoinLayout(Guid layoutId)
    {
        var userId = CurrentUserId();
        if (IsAdmin())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Layout(layoutId), Context.ConnectionAborted);
            return;
        }

        var layout = await _layoutRepo.GetActiveByIdAsync(layoutId, Context.ConnectionAborted);
        if (layout is null)
            throw new HubException("Layout was not found.");

        var market = await _nightMarketRepo.GetActiveByIdAsync(layout.NightMarketId, Context.ConnectionAborted);
        if (market is null || market.MarketOwnerId != userId)
            throw new HubException("You are not authorized to join this layout group.");

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Layout(layoutId), Context.ConnectionAborted);
    }

    public Task LeaveLayout(Guid layoutId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Layout(layoutId), Context.ConnectionAborted);

    /// <summary>
    /// Join the booth group. Booth owners join their own booth; admins can join any;
    /// authenticated customers may join customer-visible booths for menu/order realtime.
    /// </summary>
    public async Task JoinBooth(Guid boothId)
    {
        var userId = CurrentUserId();
        if (IsAdmin())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Booth(boothId), Context.ConnectionAborted);
            return;
        }

        var owned = await _boothRepo.GetOwnedBoothAsync(userId, boothId);
        if (owned is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Booth(boothId), Context.ConnectionAborted);
            return;
        }

        if (await _boothRepo.CustomerVisibleExistsAsync(boothId, Context.ConnectionAborted))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Booth(boothId), Context.ConnectionAborted);
            return;
        }

        throw new HubException("You are not authorized to join this booth group.");
    }

    public Task LeaveBooth(Guid boothId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Booth(boothId), Context.ConnectionAborted);

    /// <summary>
    /// Join the support-ticket group.  The requester can join their own tickets;
    /// Admin can join any ticket.
    /// </summary>
    public async Task JoinSupportTicket(Guid ticketId)
    {
        var userId = CurrentUserId();
        if (IsAdmin())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.SupportTicket(ticketId), Context.ConnectionAborted);
            return;
        }

        var ticket = await _supportTicketRepo.GetDetailAsync(ticketId, Context.ConnectionAborted);
        if (ticket is null || ticket.RequesterId != userId)
            throw new HubException("You are not authorized to join this support ticket group.");

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.SupportTicket(ticketId), Context.ConnectionAborted);
    }

    public Task LeaveSupportTicket(Guid ticketId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.SupportTicket(ticketId), Context.ConnectionAborted);

    // ─── Helpers ───

    private Guid CurrentUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            throw new HubException("Invalid user.");
        return userId;
    }

    private bool IsAdmin()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Legacy group name kept for backward compatibility with existing
    /// <see cref="SignalRNotificationPublisher"/>.  New code should use
    /// <see cref="RealtimeGroups.User"/>.
    /// </summary>
    internal static string GroupName(Guid userId) => RealtimeGroups.User(userId);
}

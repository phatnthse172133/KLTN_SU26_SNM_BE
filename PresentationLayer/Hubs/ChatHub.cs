using System.Security.Claims;
using ApplicationLayer.Services.Chats;
using ApplicationLayer.Services.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PresentationLayer.Hubs;

[Authorize(Roles = "Customer,BoothOwner")]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId();
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.ChatUser(userId),
            Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = CurrentUserId();
        if (!await _chatService.IsParticipantAsync(userId, conversationId, Context.ConnectionAborted))
            throw new HubException("You are not allowed to join this conversation.");

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            ConversationGroupName(conversationId),
            Context.ConnectionAborted);
    }

    public Task LeaveConversation(Guid conversationId)
        => Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            ConversationGroupName(conversationId),
            Context.ConnectionAborted);

    public async Task MarkConversationRead(Guid conversationId)
    {
        await _chatService.MarkReadAsync(
            CurrentUserId(),
            conversationId,
            Context.ConnectionAborted);
    }

    public async Task StartTyping(Guid conversationId)
    {
        var userId = CurrentUserId();
        if (!await _chatService.IsParticipantAsync(userId, conversationId, Context.ConnectionAborted))
            throw new HubException("You are not allowed to type in this conversation.");

        await Clients
            .OthersInGroup(ConversationGroupName(conversationId))
            .SendAsync("UserTyping", new
            {
                ConversationId = conversationId,
                UserId = userId
            }, Context.ConnectionAborted);
    }

    public async Task StopTyping(Guid conversationId)
    {
        var userId = CurrentUserId();
        if (!await _chatService.IsParticipantAsync(userId, conversationId, Context.ConnectionAborted))
            throw new HubException("You are not allowed to type in this conversation.");

        await Clients
            .OthersInGroup(ConversationGroupName(conversationId))
            .SendAsync("UserStoppedTyping", new
            {
                ConversationId = conversationId,
                UserId = userId
            }, Context.ConnectionAborted);
    }

    internal static string ConversationGroupName(Guid conversationId)
        => RealtimeGroups.Conversation(conversationId);

    private Guid CurrentUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            throw new HubException("Invalid user.");

        return userId;
    }
}

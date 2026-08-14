using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.Chats;
using ApplicationLayer.Services.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace PresentationLayer.Hubs;

public class SignalRChatPublisher : IRealtimeChatPublisher
{
    private readonly IHubContext<ChatHub> _hubContext;

    public SignalRChatPublisher(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishMessageCreatedAsync(
        Guid conversationId,
        Guid recipientUserId,
        MessageResponse message,
        CancellationToken cancellationToken = default)
    {
        // conversationId remains on the message payload for client routing;
        // delivery target is chat-user:{recipient} only.
        if (recipientUserId == Guid.Empty)
            return Task.CompletedTask;

        return _hubContext.Clients
            .Group(RealtimeGroups.ChatUser(recipientUserId))
            .SendAsync("MessageCreated", message, cancellationToken);
    }

    public Task PublishMessageDeletedAsync(
        Guid conversationId,
        Guid messageId,
        Guid deletedByUserId,
        DateTime deletedAt,
        CancellationToken cancellationToken = default)
        => _hubContext.Clients
            .Group(ChatHub.ConversationGroupName(conversationId))
            .SendAsync("MessageDeleted", new
            {
                ConversationId = conversationId,
                MessageId = messageId,
                DeletedByUserId = deletedByUserId,
                DeletedAt = deletedAt
            }, cancellationToken);

    public Task PublishConversationReadAsync(
        Guid conversationId,
        Guid readerId,
        DateTime readAt,
        CancellationToken cancellationToken = default)
        => _hubContext.Clients
            .Group(ChatHub.ConversationGroupName(conversationId))
            .SendAsync("ConversationRead", new
            {
                ConversationId = conversationId,
                ReaderId = readerId,
                ReadAt = readAt
            }, cancellationToken);
}

using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Chats;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Realtime;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Chats;

public class ChatService : IChatService
{
    private const int MaxMessageLength = 2000;
    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;
    private readonly IBoothRepository _booths;
    private readonly IMapper _mapper;
    private readonly IRealtimeChatPublisher _realtime;
    private readonly IRealtimeEventPublisher _eventPublisher;
    private readonly INotificationService _notifications;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IConversationRepository conversations,
        IMessageRepository messages,
        IBoothRepository booths,
        IMapper mapper,
        IRealtimeChatPublisher realtime,
        IRealtimeEventPublisher eventPublisher,
        INotificationService notifications,
        ILogger<ChatService> logger)
    {
        _conversations = conversations;
        _messages = messages;
        _booths = booths;
        _mapper = mapper;
        _realtime = realtime;
        _eventPublisher = eventPublisher;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ApiResponse<ConversationResponse>> CreateCustomerBoothConversationAsync(
        Guid userId,
        CreateCustomerBoothConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await _booths.CustomerVisibleExistsAsync(request.BoothId, cancellationToken))
            throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");

        var booth = await _booths.GetByIdAsync(request.BoothId)
            ?? throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");

        if (booth.BoothOwnerId == userId)
        {
            throw AppException.BadRequest(
                "Booth owner cannot start a customer conversation with their own booth.",
                "CHAT_SELF_CONVERSATION");
        }

        var (conversation, created) = await _conversations.GetOrCreateCustomerBoothAsync(
            userId,
            booth.Id,
            DateTime.UtcNow,
            cancellationToken);
        var unreadCount = await GetUnreadCountAsync(conversation, userId, cancellationToken);

        return ApiResponse<ConversationResponse>.SuccessResponse(
            ToConversationResponse(conversation, unreadCount),
            created ? "Conversation created successfully." : "Conversation already exists.");
    }

    public async Task<ApiResponse<PaginationResp<ConversationResponse>>> GetConversationsAsync(
        Guid userId,
        ChatListRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await _conversations.GetPagedByUserAsync(
            userId,
            request.Keyword?.Trim(),
            request.Page,
            request.PageSize,
            cancellationToken);
        var unreadCounts = await _messages.CountUnreadByConversationIdsAsync(
            page.Items.Select(conversation => conversation.Id).ToList(),
            userId,
            cancellationToken);

        var response = PaginationResp<ConversationResponse>.Create(
            page.Items
                .Select(conversation => ToConversationResponse(
                    conversation,
                    unreadCounts.GetValueOrDefault(conversation.Id)))
                .ToList(),
            page.TotalCount,
            request);

        return ApiResponse<PaginationResp<ConversationResponse>>.SuccessResponse(response);
    }

    public async Task<ApiResponse<ConversationResponse>> GetConversationAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await GetOwnedConversationAsync(userId, conversationId, cancellationToken);
        var unreadCount = await GetUnreadCountAsync(conversation, userId, cancellationToken);
        return ApiResponse<ConversationResponse>.SuccessResponse(
            ToConversationResponse(conversation, unreadCount));
    }

    public async Task<ApiResponse<PaginationResp<MessageResponse>>> GetMessagesAsync(
        Guid userId,
        Guid conversationId,
        MessageListRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await GetOwnedConversationAsync(userId, conversationId, cancellationToken);
        var page = await _messages.GetPagedByConversationAsync(
            conversationId,
            request.Page,
            request.PageSize,
            cancellationToken);

        return ApiResponse<PaginationResp<MessageResponse>>.SuccessResponse(
            _mapper.MapPage<Message, MessageResponse>(page, request));
    }

    public async Task<ApiResponse<MessageResponse>> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Type != MessageType.Text)
        {
            throw AppException.BadRequest(
                "Only text messages are supported.",
                "CHAT_MESSAGE_TYPE_UNSUPPORTED");
        }

        var content = NormalizeContent(request.Content);
        var conversation = await GetOwnedConversationAsync(userId, conversationId, cancellationToken);

        if (request.ClientMessageId.HasValue)
        {
            var duplicated = await _messages.GetByClientMessageIdAsync(
                userId,
                request.ClientMessageId.Value,
                cancellationToken);
            if (duplicated is not null)
            {
                if (duplicated.ConversationId != conversationId)
                {
                    throw AppException.Conflict(
                        "ClientMessageId was already used in another conversation.",
                        "CHAT_CLIENT_MESSAGE_ID_CONFLICT");
                }

                if (duplicated.Type != request.Type
                    || !string.Equals(duplicated.Content, content, StringComparison.Ordinal))
                {
                    throw AppException.Conflict(
                        "ClientMessageId was reused with different message content.",
                        "CHAT_CLIENT_MESSAGE_ID_PAYLOAD_CONFLICT");
                }

                return ApiResponse<MessageResponse>.SuccessResponse(
                    _mapper.Map<MessageResponse>(duplicated),
                    "Message already exists.");
            }
        }

        if (conversation.Status != ConversationStatus.Active)
        {
            throw AppException.BadRequest(
                "Conversation is not active.",
                "CHAT_CONVERSATION_CLOSED");
        }

        if (!await _booths.CustomerVisibleExistsAsync(conversation.BoothId, cancellationToken))
        {
            throw AppException.BadRequest(
                "Messages cannot be sent while the booth is unavailable.",
                "CHAT_BOOTH_UNAVAILABLE");
        }

        var now = DateTime.UtcNow;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = userId,
            ClientMessageId = request.ClientMessageId,
            SenderRole = GetSenderRole(conversation, userId),
            Type = request.Type,
            Content = content,
            IsRead = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _messages.AddAsync(message);
        conversation.LastMessageId = message.Id;
        conversation.LastMessageAt = now;
        conversation.UpdatedAt = now;
        _conversations.Update(conversation);
        await _messages.SaveChangesAsync();

        var created = await _messages.GetOwnedAsync(message.Id, userId, cancellationToken) ?? message;
        var response = _mapper.Map<MessageResponse>(created);
        await RunPostCommitSafelyAsync(
            () => _realtime.PublishMessageCreatedAsync(
                conversationId,
                response,
                CancellationToken.None),
            "realtime message delivery",
            message.Id);
        await RunPostCommitSafelyAsync(
            () => _eventPublisher.PublishAsync(new RealtimeEvent
            {
                EventType = "MessageCreated",
                GroupName = RealtimeGroups.Conversation(conversationId),
                Payload = response
            }),
            "unified message event delivery",
            message.Id);

        var receiverId = conversation.CustomerId == userId
            ? conversation.Booth.BoothOwnerId
            : conversation.CustomerId;
        await RunPostCommitSafelyAsync(
            () => _notifications.NotifyAsync(
                new NotificationMessage(
                    receiverId,
                    NotificationType.NewMessage,
                    "New chat message",
                    $"You have a new message from {response.SenderName}.",
                    conversation.BoothId,
                    "Conversation",
                    conversation.Id),
                CancellationToken.None),
            "chat notification creation",
            message.Id);

        return ApiResponse<MessageResponse>.SuccessResponse(
            response,
            "Message sent successfully.");
    }

    public async Task<ApiResponse<object>> MarkReadAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await GetOwnedConversationAsync(userId, conversationId, cancellationToken);
        var now = DateTime.UtcNow;

        if (conversation.CustomerId == userId)
            conversation.CustomerLastReadAt = now;
        else
            conversation.BoothOwnerLastReadAt = now;

        conversation.UpdatedAt = now;
        _conversations.Update(conversation);
        var updatedCount = await _messages.MarkConversationMessagesReadAsync(
            conversationId,
            userId,
            now,
            cancellationToken);
        await _conversations.SaveChangesAsync();
        await RunPostCommitSafelyAsync(
            () => _notifications.MarkReferenceReadAsync(
                userId,
                "Conversation",
                conversationId,
                CancellationToken.None),
            "chat notification read synchronization",
            conversationId);
        await RunPostCommitSafelyAsync(
            () => _realtime.PublishConversationReadAsync(
                conversationId,
                userId,
                now,
                CancellationToken.None),
            "read receipt delivery",
            conversationId);
        await RunPostCommitSafelyAsync(
            () => _eventPublisher.PublishAsync(new RealtimeEvent
            {
                EventType = "ConversationRead",
                GroupName = RealtimeGroups.Conversation(conversationId),
                Payload = new { conversationId, readerId = userId, readAt = now }
            }),
            "unified conversation read event",
            conversationId);

        return ApiResponse<object>.SuccessResponse(
            new { UpdatedCount = updatedCount },
            "Conversation marked as read.");
    }

    public async Task DeleteMessageAsync(
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var message = await _messages.GetOwnedAsync(messageId, userId, cancellationToken)
            ?? throw AppException.NotFound("Message was not found.", "CHAT_MESSAGE_NOT_FOUND");

        if (message.SenderId != userId)
        {
            throw AppException.Forbidden(
                "Only the sender can delete this message.",
                "CHAT_MESSAGE_DELETE_FORBIDDEN");
        }

        var now = DateTime.UtcNow;
        message.DeletedAt = now;
        message.Content = string.Empty;
        message.UpdatedAt = now;
        _messages.Update(message);

        if (message.Conversation.LastMessageId == message.Id)
        {
            var previousMessage = await _messages.GetLatestVisibleByConversationAsync(
                message.ConversationId,
                message.Id,
                cancellationToken);
            message.Conversation.LastMessageId = previousMessage?.Id;
            message.Conversation.LastMessageAt = previousMessage?.CreatedAt;
            message.Conversation.UpdatedAt = now;
            _conversations.Update(message.Conversation);
        }

        await _messages.SaveChangesAsync();
        await RunPostCommitSafelyAsync(
            () => _realtime.PublishMessageDeletedAsync(
                message.ConversationId,
                message.Id,
                userId,
                now,
                CancellationToken.None),
            "message deletion delivery",
            message.Id);
        await RunPostCommitSafelyAsync(
            () => _eventPublisher.PublishAsync(new RealtimeEvent
            {
                EventType = "MessageDeleted",
                GroupName = RealtimeGroups.Conversation(message.ConversationId),
                Payload = new { conversationId = message.ConversationId, messageId = message.Id, deletedByUserId = userId, deletedAt = now }
            }),
            "unified message deletion event",
            message.Id);
    }

    public async Task<bool> IsParticipantAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
        => await _conversations.GetOwnedAsync(conversationId, userId, cancellationToken) is not null;

    private async Task<Conversation> GetOwnedConversationAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken)
        => await _conversations.GetOwnedWithUsersAsync(conversationId, userId, cancellationToken)
            ?? throw AppException.NotFound(
                "Conversation was not found.",
                "CHAT_CONVERSATION_NOT_FOUND");

    private async Task<int> GetUnreadCountAsync(
        Conversation conversation,
        Guid userId,
        CancellationToken cancellationToken)
        => await _messages.CountUnreadAsync(
            conversation.Id,
            userId,
            cancellationToken);

    private ConversationResponse ToConversationResponse(Conversation conversation, int unreadCount)
    {
        var response = _mapper.Map<ConversationResponse>(conversation);
        response.UnreadCount = unreadCount;
        return response;
    }

    private static string NormalizeContent(string content)
    {
        var normalized = content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw AppException.BadRequest(
                "Message content is required.",
                "CHAT_MESSAGE_CONTENT_REQUIRED");
        }

        if (normalized.Length > MaxMessageLength)
        {
            throw AppException.BadRequest(
                $"Message content cannot exceed {MaxMessageLength} characters.",
                "CHAT_MESSAGE_TOO_LONG");
        }

        return normalized;
    }

    private static ConversationParticipantRole GetSenderRole(Conversation conversation, Guid userId)
        => conversation.CustomerId == userId
            ? ConversationParticipantRole.Customer
            : ConversationParticipantRole.BoothOwner;

    private async Task RunPostCommitSafelyAsync(
        Func<Task> action,
        string operation,
        Guid resourceId)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Chat {Operation} failed for resource {ResourceId}; persisted chat data was retained.",
                operation,
                resourceId);
        }
    }
}

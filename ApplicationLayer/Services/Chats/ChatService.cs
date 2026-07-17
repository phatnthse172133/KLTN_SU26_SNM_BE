using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
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

    public ChatService(
        IConversationRepository conversations,
        IMessageRepository messages,
        IBoothRepository booths,
        IMapper mapper,
        IRealtimeChatPublisher realtime)
    {
        _conversations = conversations;
        _messages = messages;
        _booths = booths;
        _mapper = mapper;
        _realtime = realtime;
    }

    public async Task<ApiResponse<ConversationResponse>> CreateCustomerBoothConversationAsync(
        Guid userId,
        CreateCustomerBoothConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var booth = await _booths.GetByIdAsync(request.BoothId)
            ?? throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");

        if (booth.Status != BoothStatus.Active)
        {
            throw AppException.BadRequest(
                "Cannot start a conversation with an inactive booth.",
                "CHAT_BOOTH_INACTIVE");
        }

        if (booth.BoothOwnerId == userId)
        {
            throw AppException.BadRequest(
                "Booth owner cannot start a customer conversation with their own booth.",
                "CHAT_SELF_CONVERSATION");
        }

        var existing = await _conversations.GetByParticipantsAsync(
            userId,
            booth.BoothOwnerId,
            cancellationToken);
        if (existing is not null)
        {
            var unreadCount = await GetUnreadCountAsync(existing, userId, cancellationToken);
            return ApiResponse<ConversationResponse>.SuccessResponse(
                ToConversationResponse(existing, unreadCount));
        }

        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            CustomerId = userId,
            BoothOwnerId = booth.BoothOwnerId,
            Status = ConversationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _conversations.AddAsync(conversation);
        await _conversations.SaveChangesAsync();

        var created = await _conversations.GetOwnedWithUsersAsync(
            conversation.Id,
            userId,
            cancellationToken) ?? conversation;

        return ApiResponse<ConversationResponse>.SuccessResponse(
            ToConversationResponse(created, 0),
            "Conversation created successfully.");
    }

    public async Task<ApiResponse<PaginationResp<ConversationResponse>>> GetConversationsAsync(
        Guid userId,
        ChatListRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await _conversations.GetPagedByUserAsync(
            userId,
            request.Page,
            request.PageSize,
            cancellationToken);
        var readTimes = page.Items.ToDictionary(
            conversation => conversation.Id,
            conversation => GetLastReadAt(conversation, userId));
        var unreadCounts = await _messages.CountUnreadByConversationIdsAsync(
            readTimes,
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
        var content = NormalizeContent(request.Content);
        var conversation = await GetOwnedConversationAsync(userId, conversationId, cancellationToken);
        if (conversation.Status != ConversationStatus.Active)
        {
            throw AppException.BadRequest(
                "Conversation is not active.",
                "CHAT_CONVERSATION_CLOSED");
        }

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

                return ApiResponse<MessageResponse>.SuccessResponse(
                    _mapper.Map<MessageResponse>(duplicated),
                    "Message already exists.");
            }
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
        await _realtime.PublishMessageCreatedAsync(conversationId, response, cancellationToken);

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
        await _realtime.PublishConversationReadAsync(conversationId, userId, now, cancellationToken);

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
        await _realtime.PublishMessageDeletedAsync(
            message.ConversationId,
            message.Id,
            userId,
            now,
            cancellationToken);
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
            GetLastReadAt(conversation, userId),
            cancellationToken);

    private static DateTime? GetLastReadAt(Conversation conversation, Guid userId)
        => conversation.CustomerId == userId
            ? conversation.CustomerLastReadAt
            : conversation.BoothOwnerLastReadAt;

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
}

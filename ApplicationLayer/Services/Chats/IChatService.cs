using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Chats;

public interface IChatService
{
    Task<ApiResponse<ConversationResponse>> CreateCustomerBoothConversationAsync(
        Guid userId,
        CreateCustomerBoothConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PaginationResp<ConversationResponse>>> GetConversationsAsync(
        Guid userId,
        ChatListRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ConversationResponse>> GetConversationAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PaginationResp<MessageResponse>>> GetMessagesAsync(
        Guid userId,
        Guid conversationId,
        MessageListRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MessageResponse>> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> MarkReadAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task DeleteMessageAsync(
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task<bool> IsParticipantAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);
}

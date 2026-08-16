using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Assistant;

public interface IAssistantService
{
    Task<ApiResponse<CreateAssistantConversationResponse>> CreateConversationAsync(
        Guid customerId,
        CreateAssistantConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AssistantTurnResponse>> SendMessageAsync(
        Guid customerId,
        Guid conversationId,
        SendAssistantMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CartBatchAddResponse>> AddMealPlanToCartAsync(
        Guid customerId,
        Guid conversationId,
        Guid mealPlanId,
        CancellationToken cancellationToken = default);
}

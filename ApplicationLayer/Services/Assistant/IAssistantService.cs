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

    Task<ApiResponse<AssistantMealPlanResponse>> UpdateMealPlanItemQuantityAsync(
        Guid customerId,
        Guid conversationId,
        Guid mealPlanId,
        Guid itemId,
        UpdateAssistantMealPlanItemQuantityRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AssistantMealPlanResponse>> RemoveMealPlanItemAsync(
        Guid customerId,
        Guid conversationId,
        Guid mealPlanId,
        Guid itemId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AssistantMealPlanReplacementsResponse>> GetMealPlanItemReplacementsAsync(
        Guid customerId,
        Guid conversationId,
        Guid mealPlanId,
        Guid itemId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AssistantMealPlanResponse>> ReplaceMealPlanItemAsync(
        Guid customerId,
        Guid conversationId,
        Guid mealPlanId,
        Guid itemId,
        ReplaceAssistantMealPlanItemRequest request,
        CancellationToken cancellationToken = default);
}

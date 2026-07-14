using ApplicationLayer.AI.DTOs;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.AI.Services;

public interface IAIRecommendationService
{
    Task<ApiResponse<AIHomeResponse>> GetHomeAsync(
        Guid? customerId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<FoodDiscoveryResponse>> GetPersonalizedRecommendationsAsync(
        Guid? customerId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<FoodDiscoveryResponse>> FoodDiscoveryAsync(
        Guid? customerId,
        FoodDiscoveryRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<DiningPlanAssistantResponse>> DiningPlanAssistantAsync(
        Guid? customerId,
        DiningPlanAssistantRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<DiningPlanReadyResponse>> ConfirmDiningPlanAsync(
        Guid? customerId,
        ConfirmDiningPlanRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<DiningPlanAssistantResponse>> RegenerateDiningPlanAsync(
        Guid? customerId,
        RegenerateDiningPlanRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AIFeedbackResponse>> SubmitFeedbackAsync(
        Guid? customerId,
        AIFeedbackRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AIRecommendationLogResponse>> GetLogAsync(
        Guid logId,
        CancellationToken cancellationToken = default);
}

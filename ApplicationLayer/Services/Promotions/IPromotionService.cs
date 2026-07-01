using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Promotions;

public interface IPromotionService
{
    Task<ApiResponse<PaginationResp<PromotionResponse>>> GetByBoothAsync(Guid ownerId,Guid boothId, PromotionListRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<PaginationResp<PromotionResponse>>> GetAllAsync(PromotionListRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<PromotionResponse>> GetAsync(Guid userId, string role, Guid promotionId, CancellationToken cancellationToken = default);

    Task<ApiResponse<PromotionResponse>> CreateAsync(Guid ownerId, Guid boothId, CreatePromotionRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<PromotionResponse>> UpdateAsync(Guid ownerId, Guid promotionId, UpdatePromotionRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<PromotionResponse>> ActivateAsync(Guid ownerId, Guid promotionId, CancellationToken cancellationToken = default);

    Task<ApiResponse<PromotionResponse>> DeactivateAsync(Guid ownerId, Guid promotionId, CancellationToken cancellationToken = default);

    Task<ApiResponse<PromotionResponse>> SuspendAsync(Guid promotionId, CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> DeleteAsync(Guid ownerId, Guid promotionId, CancellationToken cancellationToken = default);

    Task<ApiResponse<PromotionUsageStatisticsResponse>> GetUsageStatisticsAsync(Guid userId, string role, Guid promotionId, CancellationToken cancellationToken = default);

    Task<ApiResponse<PaginationResp<AvailablePromotionResponse>>> GetAvailableForCartAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default);

    Task<ApiResponse<PromotionValidationResponse>> ValidateForCartAsync(Guid customerId,ValidateCartPromotionRequest request, CancellationToken cancellationToken = default);
}

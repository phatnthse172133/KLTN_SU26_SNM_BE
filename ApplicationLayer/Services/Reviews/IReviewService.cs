using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Reviews;

public interface IReviewService
{
    Task<ApiResponse<CustomerReviewHistoryResponse>> CreateAsync(Guid customerId, CreateReviewRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<CustomerReviewHistoryResponse>> UpdateAsync(Guid customerId, Guid reviewId, UpdateReviewRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<CustomerReviewHistoryResponse>> HideAsync(Guid customerId, Guid reviewId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<CustomerReviewResponse>>> GetByBoothAsync(Guid boothId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<CustomerReviewHistoryResponse>>> GetMineAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ReviewResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ReviewResponse>>> GetAllFilteredAsync(AdminReviewQueryRequest query, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReviewResponse>> UpdateVisibilityAsync(Guid reviewId, UpdateReviewVisibilityRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReviewResponse>> UpsertReplyAsync(Guid ownerId, Guid reviewId, UpsertReviewReplyRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ReviewResponse>>> GetByMarketOwnerAsync(Guid marketOwnerId, MarketOwnerReviewQueryRequest query, CancellationToken cancellationToken = default);

    Task<ApiResponse<CustomerFoodReviewHistoryResponse>> CreateFoodReviewAsync(Guid customerId, CreateFoodReviewRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<CustomerFoodReviewHistoryResponse>> UpdateFoodReviewAsync(Guid customerId, Guid foodReviewId, UpdateFoodReviewRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<CustomerFoodReviewHistoryResponse>> HideFoodReviewAsync(Guid customerId, Guid foodReviewId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<FoodReviewResponse>>> GetByFoodItemAsync(Guid foodItemId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<CustomerFoodReviewHistoryResponse>>> GetMineFoodAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReviewImageUploadResponse>> UploadImageAsync(Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default);
}

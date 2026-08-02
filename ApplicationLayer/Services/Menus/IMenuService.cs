using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Menus;

public interface IMenuService
{
    Task<ApiResponse<PaginationResp<FoodItemResponse>>> GetMyBoothMenuAsync(Guid ownerId, Guid boothId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<FoodItemResponse>> CreateFoodItemAsync(Guid ownerId, Guid boothId, CreateFoodItemRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<FoodItemResponse>> UpdateFoodItemAsync(Guid ownerId, Guid boothId, Guid foodItemId, UpdateFoodItemRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<FoodItemV2Response>> CreateFoodItemV2Async(Guid ownerId, Guid boothId, CreateFoodItemV2Request request, CancellationToken cancellationToken = default);
    Task<ApiResponse<FoodItemV2Response>> UpdateFoodItemV2Async(Guid ownerId, Guid boothId, Guid foodItemId, UpdateFoodItemV2Request request, CancellationToken cancellationToken = default);
    Task<ApiResponse<FoodItemResponse>> UpdateAvailabilityAsync(Guid ownerId, Guid boothId, Guid foodItemId, UpdateFoodAvailabilityRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<FoodItemResponse>> UpdateFeaturedAsync(Guid ownerId, Guid boothId, Guid foodItemId, UpdateFoodFeaturedRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteFoodItemAsync(Guid ownerId, Guid boothId, Guid foodItemId, CancellationToken cancellationToken = default);
}

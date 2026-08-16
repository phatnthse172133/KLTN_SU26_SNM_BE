using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.FoodTags;

public interface IFoodTagService
{
    Task<ApiResponse<PaginationResp<FoodTagResponse>>> GetAsync(
        FoodTagQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<FoodTagResponse>> CreateAsync(
        CreateFoodTagRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<FoodTagResponse>> UpdateAsync(
        Guid tagId,
        UpdateFoodTagRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> DeleteAsync(
        Guid tagId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyCollection<FoodTagResponse>>> UpdateFoodItemTagsAsync(
        Guid ownerId,
        Guid foodItemId,
        UpdateFoodItemTagsRequest request,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}

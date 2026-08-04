using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.FoodCategories;

public interface IFoodCategoryService
{
    Task<ApiResponse<PaginationResp<FoodCategoryResponse>>> GetSelectableAsync(PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<FoodCategoryResponse>>> GetMyBoothCategoriesAsync(Guid ownerId, Guid boothId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<FoodCategoryResponse>> GetMyBoothCategoryAsync(Guid ownerId, Guid boothId, Guid categoryId, CancellationToken cancellationToken = default);
    Task<ApiResponse<FoodCategoryResponse>> CreateAsync(Guid ownerId, Guid boothId, CreateFoodCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<FoodCategoryResponse>> UpdateAsync(Guid ownerId, Guid boothId, Guid categoryId, UpdateFoodCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteAsync(Guid ownerId, Guid boothId, Guid categoryId, CancellationToken cancellationToken = default);
}

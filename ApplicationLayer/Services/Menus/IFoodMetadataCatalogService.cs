using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Menus;

public interface IFoodMetadataCatalogService
{
    Task<ApiResponse<FoodMetadataCatalogResponse>> GetActiveAsync(CancellationToken cancellationToken = default);
}

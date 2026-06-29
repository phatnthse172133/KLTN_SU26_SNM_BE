using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Prices;

public interface IPriceService
{
    Task<ApiResponse<PaginationResp<FoodPriceResponse>>> GetFoodPricesAsync(
        Guid ownerId, Guid boothId, Guid foodItemId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<FoodPriceResponse>> CreateFoodPriceAsync(Guid ownerId, Guid boothId, Guid foodItemId, CreatePriceRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<FoodPriceResponse>> UpdateFoodPriceAsync(Guid ownerId, Guid boothId, Guid foodItemId, Guid priceId, UpdatePriceRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteFoodPriceAsync(Guid ownerId, Guid boothId, Guid foodItemId, Guid priceId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<PackagePriceResponse>>> GetPackagePricesAsync(
        Guid packageId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PackagePriceResponse>> CreatePackagePriceAsync(Guid packageId, CreatePriceRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PackagePriceResponse>> UpdatePackagePriceAsync(Guid packageId, Guid priceId, UpdatePriceRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeletePackagePriceAsync(Guid packageId, Guid priceId, CancellationToken cancellationToken = default);
}

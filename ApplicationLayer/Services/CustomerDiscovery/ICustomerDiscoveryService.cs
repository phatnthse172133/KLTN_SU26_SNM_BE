using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.CustomerDiscovery;

public interface ICustomerDiscoveryService
{
    Task<ApiResponse<PaginationResp<CustomerBoothListItemResponse>>> GetBoothsAsync(CustomerBoothQueryRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<CustomerBoothDetailResponse>> GetBoothAsync(Guid boothId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<CustomerFoodListItemResponse>>> GetFoodsAsync(CustomerFoodQueryRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<CustomerFoodDetailResponse>> GetFoodAsync(Guid foodItemId, CancellationToken cancellationToken = default);
}

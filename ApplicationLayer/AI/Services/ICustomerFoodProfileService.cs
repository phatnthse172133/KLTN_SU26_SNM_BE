using ApplicationLayer.AI.DTOs;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.AI.Services;

public interface ICustomerFoodProfileService
{
    Task<ApiResponse<CustomerFoodProfileResponse>> GetMineAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<ApiResponse<CustomerFoodProfileResponse>> UpdateMineAsync(Guid customerId, UpdateCustomerFoodProfileRequest request, CancellationToken cancellationToken = default);
}

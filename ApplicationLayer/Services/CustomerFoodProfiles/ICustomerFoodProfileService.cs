using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.CustomerFoodProfiles;

public interface ICustomerFoodProfileService
{
    Task<ApiResponse<CustomerFoodProfileResponse>> GetMineAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<ApiResponse<CustomerFoodProfileResponse>> UpdateMineAsync(Guid customerId, UpdateCustomerFoodProfileRequest request, CancellationToken cancellationToken = default);
}

using ApplicationLayer.AI.DTOs;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.AI.Services;

public interface ICustomerPreferenceService
{
    Task<ApiResponse<CustomerPreferenceResponse>> GetMineAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CustomerPreferenceResponse>> UpdateMineAsync(
        Guid customerId,
        UpdateCustomerPreferenceRequest request,
        CancellationToken cancellationToken = default);
}

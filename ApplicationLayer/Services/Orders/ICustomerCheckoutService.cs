using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Orders;

public interface ICustomerCheckoutService
{
    Task<ApiResponse<OrderResponseDto>> CheckoutBoothAsync(
        Guid customerId,
        CheckoutCartBoothRequest request,
        CancellationToken cancellationToken = default);
}

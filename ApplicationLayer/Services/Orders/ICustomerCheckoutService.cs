using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Orders;

public interface ICustomerCheckoutService
{
    Task<CheckoutPreviewResponse> GetPreviewAsync(
        Guid customerId,
        Guid boothId,
        Guid? promotionId = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponseDto>> CreateOrderAsync(
        Guid customerId,
        CreateCustomerOrderRequest request,
        string? headerIdempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponseDto>> CheckoutBoothAsync(
        Guid customerId,
        CheckoutCartBoothRequest request,
        CancellationToken cancellationToken = default);
}

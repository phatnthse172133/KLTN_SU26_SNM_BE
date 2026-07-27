using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.PayOS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.Orders
{
    public interface IOrderService
    {
        Task<ApiResponse<OrderResponseDto>> CreateOrderAsync(CreateOrderDto dto);
        Task<ApiResponse<PaginationResp<CustomerOrderHistoryResponse>>> GetCustomerHistoryAsync(Guid customerId, CustomerOrderHistoryRequest request, CancellationToken cancellationToken = default);
        Task<ApiResponse<CustomerOrderDetailResponse>> GetCustomerDetailAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken = default);
        Task<ApiResponse<SupplementalPaymentResponseDto>> PayRemainingAmountAsync(Guid actorId, long orderCode);
        Task<WebhookDispatchResult> ProcessPaymentWebhookAsync(PayOSWebhookData verifiedData);
        Task<ApiResponse<bool>> UpdateOrderStatusByBoothOwnerAsync(Guid boothOwnerId, UpdateOrderStatusDto dto);
        Task<ApiResponse<bool>> CancelOrderByCustomer(Guid customerId, long orderCode);
        Task<ApiResponse<bool>> CancelOrderByBoothOwnerAsync(Guid boothOwnerId, long orderCode, RefundQRRequest request);
        Task<ApiResponse<bool>> ReconcileRefundAsync(Guid boothOwnerId, long orderCode);
        Task<ApiResponse<bool>> ActiveCheckPaymentStatus(long orderCode);
        Task<bool> HasOrderWithCodeAsync(long orderCode);
        Task<ApiResponse<PaginationResp<BoothOwnerOrderListItemResponse>>> GetBoothOwnerOrdersAsync(
            Guid boothOwnerId, BoothOwnerOrderQuery query, CancellationToken cancellationToken = default);
        Task<ApiResponse<BoothOwnerOrderDetailResponse>> GetBoothOwnerOrderAsync(
            Guid boothOwnerId, long orderCode, CancellationToken cancellationToken = default);
        Task<ApiResponse<OrderResponseDto>> CreateWalkInOrderAsync(
            Guid boothOwnerId, CreateWalkInOrderRequest request, CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> UpdateBoothOwnerOrderStatusAsync(
            Guid boothOwnerId, long orderCode, UpdateBoothOwnerOrderStatusRequest request,
            CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> ConfirmCashPaymentAsync(
            Guid boothOwnerId, long orderCode, CancellationToken cancellationToken = default);
    }
}

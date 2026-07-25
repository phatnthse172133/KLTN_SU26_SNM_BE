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
        Task<ApiResponse<SupplementalPaymentResponseDto>> PayRemainingAmountAsync(Guid actorId, long orderCode);
        Task<WebhookDispatchResult> ProcessPaymentWebhookAsync(PayOSWebhookData verifiedData);
        Task<ApiResponse<bool>> UpdateOrderStatusByBoothOwnerAsync(Guid boothOwnerId, UpdateOrderStatusDto dto);
        Task<ApiResponse<bool>> CancelOrder(long orderCode);
        Task<ApiResponse<bool>> ActiveCheckPaymentStatus(long orderCode);
        Task<bool> HasOrderWithCodeAsync(long orderCode);
    }
}

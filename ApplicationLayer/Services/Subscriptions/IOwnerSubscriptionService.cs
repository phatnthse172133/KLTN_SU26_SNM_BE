using ApplicationLayer.DTOs.Subscriptions;
using ApplicationLayer.Helppers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.Subscriptions
{
    public interface IOwnerSubscriptionService
    {
        Task<ApiResponse<CurrentSubscriptionResponse>> GetBoothCurrentAsync(Guid ownerId, Guid boothId, CancellationToken ct = default);
        Task<ApiResponse<List<SubscriptionHistoryItem>>> GetBoothHistoryAsync(Guid ownerId, Guid boothId, CancellationToken ct = default);
        Task<ApiResponse<SubscriptionQuoteResponse>> QuoteBoothAsync(Guid ownerId, Guid boothId, SubscriptionQuoteRequest request, CancellationToken ct = default);
        Task<ApiResponse<PayOSPaymentResponseDto>> PurchaseBoothAsync(Guid ownerId, Guid boothId, PurchaseSubscriptionRequest request, CancellationToken ct = default);
        Task<ApiResponse<PayOSPaymentResponseDto>> RenewBoothAsync(Guid ownerId, Guid boothId, RenewSubscriptionRequest request, CancellationToken ct = default);

        Task<ApiResponse<CurrentSubscriptionResponse>> GetMarketCurrentAsync(Guid ownerId, CancellationToken ct = default);
        Task<ApiResponse<List<SubscriptionHistoryItem>>> GetMarketHistoryAsync(Guid ownerId, CancellationToken ct = default);
        Task<ApiResponse<SubscriptionQuoteResponse>> QuoteMarketAsync(Guid ownerId, SubscriptionQuoteRequest request, CancellationToken ct = default);
        Task<ApiResponse<PayOSPaymentResponseDto>> PurchaseMarketAsync(Guid ownerId, PurchaseSubscriptionRequest request, CancellationToken ct = default);
        Task<ApiResponse<PayOSPaymentResponseDto>> RenewMarketAsync(Guid ownerId, RenewSubscriptionRequest request, CancellationToken ct = default);

        Task<ApiResponse<PaymentStatusResponse>> GetPaymentStatusAsync(Guid userId, Guid subscriptionId, CancellationToken ct = default);
        Task<ApiResponse<CancelPaymentResponse>> CancelPaymentAsync(Guid userId, Guid subscriptionId, CancellationToken ct = default);
    }
}

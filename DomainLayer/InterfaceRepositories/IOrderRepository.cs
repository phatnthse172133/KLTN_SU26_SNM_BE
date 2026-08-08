using DomainLayer.Entities;
using DomainLayer.Common;
using DomainLayer.Enums;
using System;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface IOrderRepository : IGenericRepository<Order>
{
    Task<Order?> GetByCustomerAsync(Guid customerId, Guid orderId);
    Task<Guid?> GetBoothIdForCustomerOrderAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken = default);
    Task<PagedResult<CustomerOrderHistoryReadModel>> GetCustomerHistoryAsync(Guid customerId, OrderStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<CustomerOrderDetailReadModel?> GetCustomerDetailAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken = default);
    Task<OrderDetail?> GetCustomerOrderDetailLineAsync(Guid customerId, Guid orderDetailId, CancellationToken cancellationToken = default);
    Task<bool> ContainsBoothItemsAsync(Guid orderId, Guid boothId);
    Task<Order?> GetOrderByCodeAsync(long orderCode);
    Task<Order?> GetOrderByCodeForUpdateAsync(long orderCode);
    Task<Order?> GetByCheckoutRequestAsync(Guid customerId, Guid checkoutRequestId);
    Task BeginTransactionAsync();
    Task AcquireCheckoutLockAsync(Guid customerId, Guid checkoutRequestId);
    Task AcquireSupplementalPaymentLockAsync(Guid orderId);
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
    Task<int> UpdateOrderStatusIfPlacedAsync(long orderCode, OrderStatus newStatus, DateTime updatedAt);
    Task<int> UpdateOrderToUnderpaidAsync(long orderCode, DateTime updatedAt);
    Task<int> UpdatePaymentToPaidAsync(long orderCode, string? paymentLinkId, string? gatewayRef, DateTime paidAt, DateTime updatedAt);
    Task<int> UpdatePaymentToPaidWithAmountAsync(long orderCode, decimal amount, string paymentLinkId, string? gatewayRef, DateTime paidAt, DateTime updatedAt);
    Task<int> UpdatePendingPaymentStatusByIdAsync(Guid paymentId, PaymentStatus status, string? gatewayRef, DateTime paidAt, DateTime updatedAt);
    Task<int> MarkPendingPaymentForRefundAsync(Guid paymentId, decimal refundAmount, string? gatewayRef, DateTime updatedAt);
    Task<int> TryClaimPayoutCreationAsync(Guid paymentId, DateTime claimedAt, DateTime staleBefore);
    Task<int> UpdateOrderFromUnderpaidToPreparingAsync(long orderCode, DateTime updatedAt);
    Task<decimal> GetTotalPaidAmountAsync(long orderCode);
    Task<Payment?> GetPaymentByPayOSOrderCodeAsync(long payOSOrderCode);
    Task<long?> GetOrderCodeByPayOSOrderCodeAsync(long payOSOrderCode);
    Task<Payment?> GetPendingPayOSPaymentByOrderIdAsync(Guid orderId);
    Task AddPaymentAsync(Payment payment);
    Task AddPaymentAttemptAsync(PaymentAttempt attempt);
    Task<int> ClearCheckedOutCartItemsAsync(Order order, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task<Guid?> TryRecordWebhookEventAsync(PaymentWebhookEvent webhookEvent, CancellationToken cancellationToken = default);
    Task CompleteWebhookEventAsync(Guid eventId, WebhookProcessingStatus status, string? error, DateTime processedAt, CancellationToken cancellationToken = default);
    Task<PagedResult<Order>> GetByBoothOwnerPagedAsync(
        Guid boothOwnerId,
        string? keyword,
        OrderStatus? status,
        PaymentStatus? paymentStatus,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<Order?> GetByBoothOwnerAndCodeAsync(
        Guid boothOwnerId,
        long orderCode,
        CancellationToken cancellationToken = default);
    Task<int> UpdateBoothOwnerOrderStatusAsync(
        Guid boothOwnerId,
        long orderCode,
        OrderStatus expectedStatus,
        OrderStatus newStatus,
        DateTime updatedAt,
        CancellationToken cancellationToken = default);
}

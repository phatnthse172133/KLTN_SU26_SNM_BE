using DomainLayer.Entities;
using DomainLayer.Enums;
using System;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface IOrderRepository : IGenericRepository<Order>
{
    Task<Order?> GetByCustomerAsync(Guid customerId, Guid orderId);
    Task<bool> ContainsBoothItemsAsync(Guid orderId, Guid boothId);
    Task<Order?> GetOrderByCodeAsync(long orderCode);
    Task BeginTransactionAsync();
    Task AcquireSupplementalPaymentLockAsync(Guid orderId);
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
    Task<int> UpdateOrderStatusIfPlacedAsync(long orderCode, OrderStatus newStatus, DateTime updatedAt);
    Task<int> UpdateOrderToUnderpaidAsync(long orderCode, DateTime updatedAt);
    Task<int> UpdatePaymentToPaidAsync(long orderCode, string? paymentLinkId, string? gatewayRef, DateTime paidAt, DateTime updatedAt);
    Task<int> UpdatePaymentToPaidWithAmountAsync(long orderCode, decimal amount, string paymentLinkId, string? gatewayRef, DateTime paidAt, DateTime updatedAt);
    Task<int> UpdateOrderFromUnderpaidToPreparingAsync(long orderCode, DateTime updatedAt);
    Task<decimal> GetTotalPaidAmountAsync(long orderCode);
    Task<Payment?> GetPaymentByPayOSOrderCodeAsync(long payOSOrderCode);
    Task<Payment?> GetPendingPayOSPaymentByOrderIdAsync(Guid orderId);
    Task AddPaymentAsync(Payment payment);
}

using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class OrderRepository : GenericRepository<Order>, IOrderRepository
{
    public OrderRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<Order?> GetByCustomerAsync(Guid customerId, Guid orderId)
        => await _dbSet.FirstOrDefaultAsync(order => order.Id == orderId && order.CustomerId == customerId);

    public async Task<bool> ContainsBoothItemsAsync(Guid orderId, Guid boothId)
        => await _context.OrderDetails
            .Include(detail => detail.FoodItem)
            .AnyAsync(detail => detail.OrderId == orderId && detail.FoodItem.BoothId == boothId);

    public async Task<Order?> GetOrderByCodeAsync(long orderCode)
        => await _dbSet.Include(order => order.Payments).FirstOrDefaultAsync(order => order.OrderCode == orderCode);

    public async Task BeginTransactionAsync()
        => await _context.Database.BeginTransactionAsync();

    public async Task AcquireSupplementalPaymentLockAsync(Guid orderId)
        => await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({orderId.ToString()}, 0))");

    public async Task CommitTransactionAsync()
        => await _context.Database.CommitTransactionAsync();

    public async Task RollbackTransactionAsync()
        => await _context.Database.RollbackTransactionAsync();

    public async Task<int> UpdateOrderStatusIfPlacedAsync(long orderCode, OrderStatus newStatus, DateTime updatedAt)
    {
        return await _dbSet
            .Where(o => o.OrderCode == orderCode && o.Status == OrderStatus.Placed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, newStatus)
                .SetProperty(o => o.UpdatedAt, updatedAt));
    }

    public async Task<int> UpdateOrderToUnderpaidAsync(long orderCode, DateTime updatedAt)
    {
        return await _dbSet
            .Where(o => o.OrderCode == orderCode && o.Status == OrderStatus.Placed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Underpaid)
                .SetProperty(o => o.UpdatedAt, updatedAt));
    }

    public async Task<int> UpdatePaymentToPaidAsync(long orderCode, string? paymentLinkId, string? gatewayRef, DateTime paidAt, DateTime updatedAt)
    {
        var query = _context.Payments
            .Where(p => p.Order != null && p.Order.OrderCode == orderCode && p.Status == PaymentStatus.Pending);

        if (!string.IsNullOrEmpty(paymentLinkId))
            query = query.Where(p => p.PaymentLinkId == paymentLinkId);

        return await query
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PaymentStatus.Paid)
                .SetProperty(p => p.GatewayRef, gatewayRef)
                .SetProperty(p => p.PaidAt, paidAt)
                .SetProperty(p => p.UpdatedAt, updatedAt));
    }

    public async Task<int> UpdatePaymentToPaidWithAmountAsync(long orderCode, decimal amount, string paymentLinkId, string? gatewayRef, DateTime paidAt, DateTime updatedAt)
    {
        if (string.IsNullOrEmpty(paymentLinkId))
            throw new ArgumentException("paymentLinkId is required to identify a specific payment transaction.", nameof(paymentLinkId));

        return await _context.Payments
            .Where(p => p.Order != null && p.Order.OrderCode == orderCode && p.Status == PaymentStatus.Pending && p.PaymentLinkId == paymentLinkId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PaymentStatus.Paid)
                .SetProperty(p => p.Amount, amount)
                .SetProperty(p => p.GatewayRef, gatewayRef)
                .SetProperty(p => p.PaidAt, paidAt)
                .SetProperty(p => p.UpdatedAt, updatedAt));
    }

    public async Task<int> UpdateOrderFromUnderpaidToPreparingAsync(long orderCode, DateTime updatedAt)
    {
        return await _dbSet
            .Where(o => o.OrderCode == orderCode && o.Status == OrderStatus.Underpaid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Preparing)
                .SetProperty(o => o.UpdatedAt, updatedAt));
    }

    public async Task<decimal> GetTotalPaidAmountAsync(long orderCode)
    {
        return await _context.Payments
            .Where(p => p.Order != null && p.Order.OrderCode == orderCode && p.Status == PaymentStatus.Paid)
            .SumAsync(p => p.Amount);
    }

    public async Task<Payment?> GetPaymentByPayOSOrderCodeAsync(long payOSOrderCode)
    {
        return await _context.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.PayOSOrderCode == payOSOrderCode);
    }

    public async Task<Payment?> GetPendingPayOSPaymentByOrderIdAsync(Guid orderId)
    {
        return await _context.Payments
            .Where(p => p.OrderId == orderId
                && p.Status == PaymentStatus.Pending
                && p.Gateway == PaymentGateway.Payos)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task AddPaymentAsync(Payment payment)
    {
        await _context.Payments.AddAsync(payment);
    }
}

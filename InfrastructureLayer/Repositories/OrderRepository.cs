using DomainLayer.Entities;
using DomainLayer.Common;
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

    public Task<Guid?> GetBoothIdForCustomerOrderAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken = default)
        => _context.OrderDetails.AsNoTracking()
            .Where(detail => detail.OrderId == orderId && detail.Order.CustomerId == customerId)
            .Select(detail => (Guid?)detail.FoodItem.BoothId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<CustomerOrderHistoryReadModel>> GetCustomerHistoryAsync(Guid customerId, OrderStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(order => order.CustomerId == customerId);
        if (status.HasValue)
            query = query.Where(order => order.Status == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(order => new CustomerOrderHistoryReadModel
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                BoothId = order.OrderDetails.Select(detail => detail.FoodItem.BoothId).First(),
                BoothName = order.OrderDetails.Select(detail => detail.FoodItem.Booth.BoothName).First(),
                OrderStatus = order.Status,
                PaymentStatus = order.Payments.OrderByDescending(payment => payment.CreatedAt).ThenByDescending(payment => payment.Id)
                    .Select(payment => (PaymentStatus?)payment.Status).FirstOrDefault(),
                FinalAmount = order.FinalAmount,
                CreatedAt = order.CreatedAt,
                ItemCount = order.OrderDetails.Sum(detail => detail.Quantity)
            }).ToListAsync(cancellationToken);

        return new PagedResult<CustomerOrderHistoryReadModel>(items, totalCount);
    }

    public Task<CustomerOrderDetailReadModel?> GetCustomerDetailAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .Where(order => order.Id == orderId && order.CustomerId == customerId)
            .Select(order => new CustomerOrderDetailReadModel
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                BoothId = order.OrderDetails.Select(detail => detail.FoodItem.BoothId).First(),
                BoothName = order.OrderDetails.Select(detail => detail.FoodItem.Booth.BoothName).First(),
                Subtotal = order.TotalAmount,
                DiscountAmount = order.DiscountAmount,
                FinalAmount = order.FinalAmount,
                OrderStatus = order.Status,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                Items = order.OrderDetails.OrderBy(detail => detail.CreatedAt)
                    .Select(detail => new CustomerOrderItemReadModel
                    {
                        FoodItemId = detail.FoodItemId,
                        FoodName = detail.FoodNameSnapshot,
                        Quantity = detail.Quantity,
                        UnitPrice = detail.UnitPrice,
                        LineTotal = detail.TotalPrice
                    }).ToList(),
                Payments = order.Payments.OrderByDescending(payment => payment.CreatedAt).ThenByDescending(payment => payment.Id)
                    .Select(payment => new CustomerPaymentReadModel
                    {
                        PaymentId = payment.Id,
                        Type = payment.Type,
                        Gateway = payment.Gateway,
                        Status = payment.Status,
                        Amount = payment.Amount,
                        RefundAmount = payment.RefundAmount,
                        PaidAt = payment.PaidAt,
                        RefundRequestedAt = payment.RefundRequestedAt,
                        RefundedAt = payment.RefundedAt,
                        CreatedAt = payment.CreatedAt
                    }).ToList(),
                Promotion = order.PromotionUsages.Where(usage => usage.Status != PromotionUsageStatus.Released)
                    .OrderByDescending(usage => usage.AppliedAt)
                    .Select(usage => new CustomerPromotionReadModel
                    {
                        PromotionId = usage.PromotionId,
                        PromotionCode = usage.PromotionCodeSnapshot,
                        PromotionTitle = usage.PromotionTitleSnapshot,
                        DiscountAmount = usage.DiscountAmount
                    }).FirstOrDefault()
            }).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> ContainsBoothItemsAsync(Guid orderId, Guid boothId)
        => await _context.OrderDetails
            .Include(detail => detail.FoodItem)
            .AnyAsync(detail => detail.OrderId == orderId && detail.FoodItem.BoothId == boothId);

    public async Task<Order?> GetOrderByCodeAsync(long orderCode)
        => await _dbSet
            .Include(order => order.Payments)
            .Include(order => order.PromotionUsages)
            .FirstOrDefaultAsync(order => order.OrderCode == orderCode);

    public async Task<Order?> GetOrderByCodeForUpdateAsync(long orderCode)
    {
        // Lock in its own statement. PostgreSQL takes a statement snapshot before
        // waiting for FOR UPDATE; loading collection joins in that same statement can
        // therefore materialize pre-wait Payment/Promotion rows. A second statement
        // after the lock observes the winner's committed state.
        var found = await _context.Database
            .SqlQuery<int>($"SELECT 1 AS \"Value\" FROM \"Order\" WHERE \"OrderCode\" = {orderCode} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (found == 0)
            return null;

        return await _dbSet
            .Include(order => order.Payments)
            .Include(order => order.PromotionUsages)
            .FirstOrDefaultAsync(order => order.OrderCode == orderCode);
    }

    public async Task<Order?> GetByCheckoutRequestAsync(Guid customerId, Guid checkoutRequestId)
        => await _dbSet
            .Include(order => order.Payments)
            .FirstOrDefaultAsync(order => order.CustomerId == customerId
                && order.CheckoutRequestId == checkoutRequestId);

    public async Task BeginTransactionAsync()
        => await _context.Database.BeginTransactionAsync();

    public Task AcquireCheckoutLockAsync(Guid customerId, Guid checkoutRequestId)
        => _context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({customerId.ToString() + ":" + checkoutRequestId}, 0))")
            : Task.CompletedTask;

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

    public Task<int> UpdatePendingPaymentStatusByIdAsync(
        Guid paymentId,
        PaymentStatus status,
        string? gatewayRef,
        DateTime paidAt,
        DateTime updatedAt)
        => _context.Payments
            .Where(payment => payment.Id == paymentId
                && payment.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(payment => payment.Status, status)
                .SetProperty(payment => payment.GatewayRef, gatewayRef)
                .SetProperty(payment => payment.PaidAt, paidAt)
                .SetProperty(payment => payment.UpdatedAt, updatedAt));

    public Task<int> MarkPendingPaymentForRefundAsync(
        Guid paymentId,
        decimal refundAmount,
        string? gatewayRef,
        DateTime updatedAt)
        => _context.Payments
            .Where(payment => payment.Id == paymentId
                && payment.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(payment => payment.Status, PaymentStatus.RefundProcessing)
                .SetProperty(payment => payment.GatewayRef, gatewayRef)
                .SetProperty(payment => payment.RefundAmount, refundAmount)
                .SetProperty(payment => payment.RefundReference, $"refund-{paymentId:N}")
                .SetProperty(payment => payment.RefundReason, "PayOS amount did not match the payment intent.")
                .SetProperty(payment => payment.RefundRequestedAt, updatedAt)
                .SetProperty(payment => payment.UpdatedAt, updatedAt));

    public Task<int> TryClaimPayoutCreationAsync(
        Guid paymentId,
        DateTime claimedAt,
        DateTime staleBefore)
        => _context.Payments
            .Where(payment => payment.Id == paymentId
                && payment.Status == PaymentStatus.RefundProcessing
                && payment.PayoutId == null
                && (payment.PayoutCreateClaimedAt == null
                    || payment.PayoutCreateClaimedAt < staleBefore))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(payment => payment.PayoutCreateClaimedAt, claimedAt)
                .SetProperty(payment => payment.UpdatedAt, claimedAt));

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
            .Include(payment => payment.Order)
                .ThenInclude(order => order.Payments)
            .Include(payment => payment.Order)
                .ThenInclude(order => order.PromotionUsages)
            .FirstOrDefaultAsync(p => p.PayOSOrderCode == payOSOrderCode);
    }

    public Task<long?> GetOrderCodeByPayOSOrderCodeAsync(long payOSOrderCode)
        => _context.Payments
            .AsNoTracking()
            .Where(payment => payment.PayOSOrderCode == payOSOrderCode)
            .Select(payment => (long?)payment.Order.OrderCode)
            .FirstOrDefaultAsync();

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

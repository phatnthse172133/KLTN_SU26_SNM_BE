using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.PayOS;
using DomainLayer.Common;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Orders;

public partial class OrderService
{
    private static readonly TimeSpan CustomerReconciliationCooldown = TimeSpan.FromSeconds(15);

    public async Task<ApiResponse<CustomerPaymentStatusResponse>> GetPaymentStatusAsync(
        Guid customerId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepo.GetByCustomerAsync(customerId, orderId)
            ?? throw AppException.NotFound("Order was not found.", "ORDER_NOT_FOUND");
        var payment = order.Payments.OrderByDescending(item => item.CreatedAt).FirstOrDefault()
            ?? throw AppException.NotFound("Payment was not found.", "PAYMENT_NOT_FOUND");
        return ApiResponse<CustomerPaymentStatusResponse>.SuccessResponse(new CustomerPaymentStatusResponse
        {
            OrderId = order.Id, OrderCode = order.OrderCode, OrderStatus = order.Status,
            PaymentStatus = payment.Status, Amount = payment.Amount, PaidAt = payment.PaidAt,
            CheckoutUrl = payment.CheckoutUrl, ExpiresAt = payment.ExpiresAt
        });
    }

    public async Task<ApiResponse<CustomerPaymentStatusResponse>> ReconcileCustomerPaymentAsync(
        Guid customerId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepo.GetByCustomerAsync(customerId, orderId)
            ?? throw AppException.NotFound("Order was not found.", "ORDER_NOT_FOUND");
        var payment = order.Payments.OrderByDescending(item => item.CreatedAt).FirstOrDefault()
            ?? throw AppException.NotFound("Payment was not found.", "PAYMENT_NOT_FOUND");

        if (order.PaymentMethod != PaymentType.PayOS
            || order.Status != OrderStatus.PendingPayment
            || payment.Status != PaymentStatus.Pending
            || !payment.PayOSOrderCode.HasValue)
            return await GetPaymentStatusAsync(customerId, orderId, cancellationToken);

        var now = DateTime.UtcNow;
        if (now - payment.UpdatedAt < CustomerReconciliationCooldown)
            return await GetPaymentStatusAsync(customerId, orderId, cancellationToken);

        var provider = await _payos.GetPaymentStatusAsync(payment.PayOSOrderCode.Value);
        if (provider is not null
            && string.Equals(provider.Status, "PAID", StringComparison.OrdinalIgnoreCase)
            && provider.AmountPaid == decimal.ToInt64(payment.Amount)
            && (string.IsNullOrWhiteSpace(payment.PaymentLinkId)
                || string.IsNullOrWhiteSpace(provider.PaymentLinkId)
                || string.Equals(payment.PaymentLinkId, provider.PaymentLinkId, StringComparison.Ordinal)))
        {
            await ProcessPaymentWebhookAsync(new PayOSWebhookData
            {
                OrderCode = provider.OrderCode,
                Amount = provider.AmountPaid,
                Code = "00",
                IsSuccessful = true,
                PaymentLinkId = provider.PaymentLinkId,
                Reference = provider.FirstTransactionReference,
                Description = "Bounded provider reconciliation"
            });
        }
        else
        {
            payment.UpdatedAt = now;
            await _orderRepo.SaveChangesAsync();
        }

        return await GetPaymentStatusAsync(customerId, orderId, cancellationToken);
    }

    public async Task<ApiResponse<OrderResponseDto>> RetryPaymentAsync(
        Guid customerId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepo.GetByCustomerAsync(customerId, orderId)
            ?? throw AppException.NotFound("Order was not found.", "ORDER_NOT_FOUND");
        var payment = order.Payments.SingleOrDefault(item => item.Type == PaymentType.PayOS)
            ?? throw AppException.Conflict("This order does not use PayOS.", "PAYMENT_METHOD_INVALID");
        if (payment.Status == PaymentStatus.Paid)
            throw AppException.Conflict("Payment is already complete.", "PAYMENT_ALREADY_PAID");
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Preparing or OrderStatus.ReadyForPickup or OrderStatus.Completed)
            throw AppException.Conflict("Order can no longer be paid.", "ORDER_CANNOT_RETRY_PAYMENT");

        var now = DateTime.UtcNow;
        await EnsureCustomerOrderStillOrderableAsync(order, now);
        var active = payment.Attempts.OrderByDescending(item => item.AttemptNumber)
            .FirstOrDefault(item => item.Status is PaymentAttemptStatus.Creating or PaymentAttemptStatus.Pending);
        if (active is not null && (!active.ExpiresAt.HasValue || active.ExpiresAt > now))
            throw AppException.Conflict("An active payment attempt already exists.", "PAYMENT_ATTEMPT_ACTIVE");
        if (active is not null) { active.Status = PaymentAttemptStatus.Expired; active.UpdatedAt = now; }

        var providerOrderCode = await _orderCodeGenerator.GenerateAsync(PayOSOrderSource.Order);
        var attempt = new PaymentAttempt
        {
            Id = Guid.NewGuid(), PaymentId = payment.Id,
            AttemptNumber = payment.Attempts.Select(item => item.AttemptNumber).DefaultIfEmpty().Max() + 1,
            ProviderOrderCode = providerOrderCode, Status = PaymentAttemptStatus.Creating,
            CreatedAt = now, UpdatedAt = now
        };
        await _orderRepo.AddPaymentAttemptAsync(attempt);
        payment.PayOSOrderCode = providerOrderCode;
        payment.MarkPending(now);
        if (order.Status == OrderStatus.PaymentFailed) order.MarkPaymentPending(now);
        await _orderRepo.SaveChangesAsync();

        try
        {
            var link = await _payos.CreatePaymentLinkAsync(new PayOSPaymentRequest
            {
                OrderCode = providerOrderCode, Amount = payment.Amount,
                Description = $"SNM{order.OrderCode % 1_000_000}"
            });
            attempt.Status = PaymentAttemptStatus.Pending;
            attempt.ProviderPaymentLinkId = link.PaymentLinkId;
            attempt.CheckoutUrl = link.CheckoutUrl;
            attempt.QrCode = link.QrCode;
            attempt.ExpiresAt = link.ExpiresAt?.UtcDateTime;
            attempt.UpdatedAt = DateTime.UtcNow;
            payment.PaymentLinkId = link.PaymentLinkId;
            payment.CheckoutUrl = link.CheckoutUrl;
            payment.QrCode = link.QrCode;
            payment.ExpiresAt = link.ExpiresAt?.UtcDateTime;
            payment.UpdatedAt = attempt.UpdatedAt;
            await _orderRepo.SaveChangesAsync();
            return ApiResponse<OrderResponseDto>.SuccessResponse(ToCustomerOrderResponse(order, payment), "Payment link recreated.");
        }
        catch
        {
            var failedAt = DateTime.UtcNow;
            attempt.Status = PaymentAttemptStatus.Failed;
            attempt.FailureCode = "PAYOS_CREATE_LINK_FAILED";
            attempt.UpdatedAt = failedAt;
            payment.MarkFailed("PAYOS_CREATE_LINK_FAILED", "PayOS did not create a checkout link.", failedAt);
            if (order.Status == OrderStatus.PendingPayment) order.MarkPaymentFailed(failedAt);
            await _orderRepo.SaveChangesAsync();
            throw;
        }
    }

    public async Task<ApiResponse<bool>> CancelCustomerOrderAsync(
        Guid customerId, Guid orderId, string? reason, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepo.GetByCustomerAsync(customerId, orderId)
            ?? throw AppException.NotFound("Order was not found.", "ORDER_NOT_FOUND");
        var payment = order.Payments.OrderByDescending(item => item.CreatedAt).FirstOrDefault()
            ?? throw AppException.NotFound("Payment was not found.", "PAYMENT_NOT_FOUND");
        if (payment.Status == PaymentStatus.Paid)
            throw AppException.Conflict("Paid orders require the refund flow.", "ORDER_CANNOT_CANCEL");
        if (order.PaymentMethod == PaymentType.Cash && order.Status != OrderStatus.Placed)
            throw AppException.Conflict("Cash order can only be cancelled while placed.", "ORDER_CANNOT_CANCEL");
        if (order.Status is OrderStatus.Preparing or OrderStatus.ReadyForPickup or OrderStatus.Completed or OrderStatus.Cancelled)
            throw AppException.Conflict("Order can no longer be cancelled.", "ORDER_CANNOT_CANCEL");

        if (payment.Type == PaymentType.PayOS && payment.PayOSOrderCode.HasValue)
            await TryCancelPayOSLinkAsync(payment.PayOSOrderCode.Value);
        var now = DateTime.UtcNow;
        payment.MarkCancelled(now);
        foreach (var attempt in payment.Attempts.Where(item => item.Status is PaymentAttemptStatus.Creating or PaymentAttemptStatus.Pending))
        {
            attempt.Status = PaymentAttemptStatus.Cancelled;
            attempt.UpdatedAt = now;
        }
        order.Cancel(reason, now);
        PromotionUsageLifecycle.ReleaseReserved(order.PromotionUsages, now);
        await _orderRepo.SaveChangesAsync();
        return ApiResponse<bool>.SuccessResponse(true, "Order cancelled.");
    }

    private static OrderResponseDto ToCustomerOrderResponse(Order order, Payment payment) => new()
    {
        OrderId = order.Id, OrderCode = order.OrderCode, Status = order.Status,
        PaymentId = payment.Id, PaymentMethod = payment.Type, PaymentStatus = payment.Status,
        TotalAmount = order.FinalAmount, PaymentUrl = payment.CheckoutUrl, CheckoutUrl = payment.CheckoutUrl,
        QrCode = payment.QrCode, ExpiresAt = payment.ExpiresAt
    };

    private async Task EnsureCustomerOrderStillOrderableAsync(Order order, DateTime utcNow)
    {
        var detail = await _orderRepo.GetCustomerDetailAsync(order.CustomerId, order.Id)
            ?? throw AppException.NotFound("Order was not found.", "ORDER_NOT_FOUND");
        var foodIds = detail.Items.Select(item => item.FoodItemId).Distinct().ToList();
        if (foodIds.Count == 0)
            throw AppException.Conflict("Order has no items.", "ORDER_HAS_NO_ITEMS");

        var foods = await _foodItemRepo.GetAllFoodItemsByIdsAsync(foodIds);
        if (foods.Count != foodIds.Count)
            throw AppException.Conflict(
                "One or more food items are no longer available for payment.",
                CustomerOrderability.FoodUnavailable);

        foreach (var food in foods)
        {
            var orderability = CustomerOrderability.Evaluate(food, utcNow);
            if (!orderability.CanOrder)
            {
                var boothName = food.Booth?.BoothName ?? "The booth";
                var message = orderability.ReasonCode == CustomerOrderability.BoothClosed
                    && orderability.NextOpenAt.HasValue
                    ? $"{boothName} is currently closed. It opens at {orderability.NextOpenAt.Value:HH:mm}."
                    : CustomerOrderability.GetPublicMessage(orderability.ReasonCode!);
                throw AppException.Conflict(message, orderability.ReasonCode!);
            }
        }
    }
}

using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Orders;
using ApplicationLayer.Services.PayOS;
using ApplicationLayer.Services.Promotions;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PayOS;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class OrderPaymentWebhookTests
{
    [Fact]
    public async Task ExactVerifiedPayment_AtomicallyMarksPaidAndPreparing()
    {
        var (service, orders, usages, payment) = CreateService();

        var result = await service.ProcessPaymentWebhookAsync(Webhook(payment, payment.Amount));

        Assert.Equal(WebhookDispatchResult.OrderHandled, result);
        orders.Verify(repository => repository.UpdatePendingPaymentStatusByIdAsync(
            payment.Id, PaymentStatus.Paid, "bank-ref", It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
        orders.Verify(repository => repository.UpdateOrderStatusIfPlacedAsync(
            payment.Order.OrderCode, OrderStatus.Preparing, It.IsAny<DateTime>()), Times.Once);
        usages.Verify(repository => repository.ConsumeReservedByOrderAsync(
            payment.OrderId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        orders.Verify(repository => repository.CommitTransactionAsync(), Times.Once);
    }

    [Theory]
    [InlineData(59_000)]
    [InlineData(61_000)]
    public async Task WrongAmount_DoesNotOverwriteExpectedAmount_AndRoutesToRefund(long received)
    {
        var (service, orders, usages, payment) = CreateService();

        var result = await service.ProcessPaymentWebhookAsync(Webhook(payment, received));

        Assert.Equal(WebhookDispatchResult.OrderHandled, result);
        Assert.Equal(60_000m, payment.Amount);
        orders.Verify(repository => repository.MarkPendingPaymentForRefundAsync(
            payment.Id, received, "bank-ref", It.IsAny<DateTime>()), Times.Once);
        orders.Verify(repository => repository.UpdateOrderStatusIfPlacedAsync(
            payment.Order.OrderCode, OrderStatus.Cancelled, It.IsAny<DateTime>()), Times.Once);
        usages.Verify(repository => repository.ConsumeReservedByOrderAsync(
            payment.OrderId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DuplicatePaidWebhook_IsNoOp()
    {
        var (service, orders, usages, payment) = CreateService();
        payment.Status = PaymentStatus.Paid;
        payment.Order.Status = OrderStatus.Preparing;

        var result = await service.ProcessPaymentWebhookAsync(Webhook(payment, payment.Amount));

        Assert.Equal(WebhookDispatchResult.AlreadyProcessed, result);
        orders.Verify(repository => repository.BeginTransactionAsync(), Times.Once);
        usages.Verify(repository => repository.ConsumeReservedByOrderAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelledOrder_LateWebhookRoutesPaymentToRefund()
    {
        var (service, orders, usages, payment) = CreateService();
        payment.Order.Status = OrderStatus.Cancelled;
        payment.Status = PaymentStatus.Cancelled;
        var result = await service.ProcessPaymentWebhookAsync(Webhook(payment, payment.Amount));

        Assert.Equal(WebhookDispatchResult.OrderHandled, result);
        Assert.Equal(PaymentStatus.RefundProcessing, payment.Status);
        Assert.Equal(payment.Amount, payment.RefundAmount);
        orders.Verify(repository => repository.CommitTransactionAsync(), Times.Once);
        orders.Verify(repository => repository.SaveChangesAsync(), Times.Once);
        usages.Verify(repository => repository.ConsumeReservedByOrderAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PaymentLinkMismatch_IsRejectedBeforeMutation()
    {
        var (service, orders, _, payment) = CreateService();
        var webhook = Webhook(payment, payment.Amount);
        webhook.PaymentLinkId = "another-link";

        var result = await service.ProcessPaymentWebhookAsync(webhook);

        Assert.Equal(WebhookDispatchResult.Conflict, result);
        orders.Verify(repository => repository.BeginTransactionAsync(), Times.Once);
        orders.Verify(repository => repository.RollbackTransactionAsync(), Times.Once);
    }

    private static (OrderService Service, Mock<IOrderRepository> Orders, Mock<IPromotionUsageRepository> Usages, Payment Payment) CreateService()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            BoothOwnerId = Guid.NewGuid(),
            OrderCode = 100_000_000_000_123L,
            Status = OrderStatus.Placed,
            FinalAmount = 60_000m
        };
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Order = order,
            BoothOwnerId = order.BoothOwnerId,
            Amount = 60_000m,
            Type = PaymentType.PayOS,
            Gateway = PaymentGateway.Payos,
            Status = PaymentStatus.Pending,
            PayOSOrderCode = order.OrderCode,
            PaymentLinkId = "pay-link"
        };
        order.Payments.Add(payment);

        var orders = new Mock<IOrderRepository>();
        orders.Setup(repository => repository.GetOrderCodeByPayOSOrderCodeAsync(order.OrderCode))
            .ReturnsAsync(order.OrderCode);
        orders.Setup(repository => repository.GetOrderByCodeForUpdateAsync(order.OrderCode))
            .ReturnsAsync(order);
        orders.Setup(repository => repository.GetOrderByCodeAsync(order.OrderCode))
            .ReturnsAsync(order);
        orders.Setup(repository => repository.BeginTransactionAsync()).Returns(Task.CompletedTask);
        orders.Setup(repository => repository.CommitTransactionAsync()).Returns(Task.CompletedTask);
        orders.Setup(repository => repository.RollbackTransactionAsync()).Returns(Task.CompletedTask);
        orders.Setup(repository => repository.UpdatePendingPaymentStatusByIdAsync(
                payment.Id, It.IsAny<PaymentStatus>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(1);
        orders.Setup(repository => repository.MarkPendingPaymentForRefundAsync(
                payment.Id, It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<DateTime>()))
            .ReturnsAsync(1);
        orders.Setup(repository => repository.UpdateOrderStatusIfPlacedAsync(
                order.OrderCode, It.IsAny<OrderStatus>(), It.IsAny<DateTime>()))
            .ReturnsAsync(1);

        var usages = new Mock<IPromotionUsageRepository>();
        usages.Setup(repository => repository.ConsumeReservedByOrderAsync(
                order.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = new OrderService(
            orders.Object,
            Mock.Of<IPromotionRepository>(),
            Mock.Of<IPromotionValidationService>(),
            Mock.Of<IPayOSPayoutService>(),
            Mock.Of<IRealtimeNotificationPublisher>(),
            Mock.Of<IFoodItemRepository>(),
            Mock.Of<ILogger<OrderService>>(),
            new ConfigurationBuilder().Build(),
            Mock.Of<IPayOSService>(),
            Mock.Of<IPayOSOrderCodeGenerator>(),
            Mock.Of<IBoothRepository>(),
            usages.Object);

        return (service, orders, usages, payment);
    }

    private static PayOSWebhookData Webhook(Payment payment, decimal amount)
        => new()
        {
            OrderCode = payment.PayOSOrderCode!.Value,
            PaymentLinkId = payment.PaymentLinkId,
            Amount = amount,
            Code = "00",
            IsSuccessful = true,
            Reference = "bank-ref"
        };
}

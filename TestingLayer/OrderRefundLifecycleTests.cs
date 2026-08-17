using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Orders;
using ApplicationLayer.Services.PayOS;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PayOS;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class OrderRefundLifecycleTests
{
    [Fact]
    public async Task Cancellation_PersistsAuthoritativeAmount_AndDoesNotCompleteOnCreate()
    {
        var order = PaidOrder(amount: 75_000m, finalAmount: 99_000m);
        var payment = Assert.Single(order.Payments);
        var repository = Repository(order);
        repository.Setup(value => value.TryClaimPayoutCreationAsync(
                payment.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(1);
        var payouts = new Mock<IPayOSPayoutService>();
        payouts.Setup(service => service.FindByReferenceAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayOSPayoutSnapshot?)null);
        payouts.Setup(service => service.CreateAsync(
                It.IsAny<PayOSPayoutCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayOSPayoutSnapshot(
                "payout-1", $"refund-{payment.Id:N}", 75_000, PayOSPayoutOutcome.Succeeded));

        var service = Service(repository.Object, payouts.Object);
        var result = await service.CancelOrderByBoothOwnerAsync(
            order.BoothOwnerId,
            order.OrderCode,
            new RefundQRRequest { BankBin = "970436", AccountNumber = "123456", RefundReason = "Sold out" });

        Assert.True(result.Success);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(PaymentStatus.RefundProcessing, payment.Status);
        Assert.Equal(75_000m, payment.RefundAmount);
        Assert.Equal($"refund-{payment.Id:N}", payment.RefundReference);
        Assert.Equal("payout-1", payment.PayoutId);
        Assert.Null(payment.RefundedAt);
        payouts.Verify(service => service.CreateAsync(
            It.Is<PayOSPayoutCommand>(command =>
                command.Amount == 75_000
                && command.IdempotencyKey == payment.Id.ToString("N")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reconciliation_RequiresExactProviderAmount_BeforeRefunded()
    {
        var order = PaidOrder(50_000m, 50_000m);
        var payment = Assert.Single(order.Payments);
        payment.Status = PaymentStatus.RefundProcessing;
        payment.RefundAmount = 50_000m;
        payment.RefundReference = $"refund-{payment.Id:N}";
        payment.PayoutId = "payout-2";
        order.Status = OrderStatus.Cancelled;

        var repository = Repository(order);
        repository.Setup(value => value.GetOrderByCodeAsync(order.OrderCode)).ReturnsAsync(order);
        var payouts = new Mock<IPayOSPayoutService>();
        payouts.Setup(service => service.GetAsync("payout-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayOSPayoutSnapshot(
                "payout-2", payment.RefundReference, 50_000, PayOSPayoutOutcome.Succeeded));

        var result = await Service(repository.Object, payouts.Object)
            .ReconcileRefundAsync(order.BoothOwnerId, order.OrderCode);

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.NotNull(payment.RefundedAt);
    }

    private static Order PaidOrder(decimal amount, decimal finalAmount)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), BoothOwnerId = Guid.NewGuid(),
            OrderCode = 100_000_000_123_456, Status = OrderStatus.Preparing,
            TotalAmount = finalAmount, FinalAmount = finalAmount
        };
        order.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), OrderId = order.Id, BoothOwnerId = order.BoothOwnerId,
            Amount = amount, Gateway = PaymentGateway.Payos, Type = PaymentType.PayOS,
            Status = PaymentStatus.Paid
        });
        return order;
    }

    private static Mock<IOrderRepository> Repository(Order order)
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(value => value.GetOrderByCodeForUpdateAsync(order.OrderCode)).ReturnsAsync(order);
        repository.Setup(value => value.BeginTransactionAsync()).Returns(Task.CompletedTask);
        repository.Setup(value => value.CommitTransactionAsync()).Returns(Task.CompletedTask);
        repository.Setup(value => value.RollbackTransactionAsync()).Returns(Task.CompletedTask);
        repository.Setup(value => value.SaveChangesAsync()).ReturnsAsync(1);
        return repository;
    }

    private static OrderService Service(IOrderRepository repository, IPayOSPayoutService payouts)
    {
        var factory = new Mock<IPayOSPayoutServiceFactory>();
        factory.Setup(value => value.ForBoothAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payouts);
        return new OrderService(
            repository,
            Mock.Of<IPromotionRepository>(),
            Mock.Of<ApplicationLayer.Services.Promotions.IPromotionValidationService>(),
            factory.Object,
            Mock.Of<IRealtimeNotificationPublisher>(),
            Mock.Of<IFoodItemRepository>(),
            Mock.Of<ILogger<OrderService>>(),
            new ConfigurationBuilder().Build(),
            Mock.Of<IPayOSService>(),
            Mock.Of<IPayOSOrderCodeGenerator>(),
            Mock.Of<IBoothRepository>(),
            Mock.Of<IPromotionUsageRepository>());
    }
}

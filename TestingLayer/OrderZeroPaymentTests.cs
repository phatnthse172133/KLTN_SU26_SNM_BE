using ApplicationLayer.DTOs.Requests;
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

public class OrderZeroPaymentTests
{
    [Fact]
    public async Task FullyDiscountedPayOSOrder_DoesNotCallProvider_AndIsSettledAtomically()
    {
        var customerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var food = CreateOrderableFood(boothId, ownerId, 60_000m);
        var promotion = new Promotion
        {
            Id = Guid.NewGuid(),
            BoothId = boothId,
            PromotionCode = "FREE",
            Title = "Free order"
        };

        var orders = new Mock<IOrderRepository>();
        Order? savedOrder = null;
        orders.Setup(repository => repository.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(order => savedOrder = order)
            .Returns(Task.CompletedTask);
        orders.Setup(repository => repository.BeginTransactionAsync()).Returns(Task.CompletedTask);
        orders.Setup(repository => repository.CommitTransactionAsync()).Returns(Task.CompletedTask);
        orders.Setup(repository => repository.RollbackTransactionAsync()).Returns(Task.CompletedTask);
        orders.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);

        var foods = new Mock<IFoodItemRepository>();
        foods.Setup(repository => repository.GetAllFoodItemsByIdsAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync([food]);

        var promotions = new Mock<IPromotionRepository>();
        promotions.Setup(repository => repository.GetByCodeAsync(
                boothId, "FREE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promotion);
        promotions.Setup(repository => repository.AcquireReservationLockAsync(
                promotion.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        promotions.Setup(repository => repository.GetReservationDetailsAsync(
                promotion.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(promotion);

        var validation = new Mock<IPromotionValidationService>();
        validation.Setup(service => service.ValidateAsync(
                customerId,
                promotion,
                It.IsAny<IReadOnlyCollection<CartItem>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromotionValidationResponse
            {
                PromotionId = promotion.Id,
                BoothId = boothId,
                DiscountAmount = 60_000m,
                ActualDiscount = 60_000m,
                FinalAmount = 0m
            });

        var payos = new Mock<IPayOSService>();
        var codeGenerator = new Mock<IPayOSOrderCodeGenerator>();
        codeGenerator.Setup(generator => generator.GenerateAsync(PayOSOrderSource.Order))
            .ReturnsAsync(100_000_000_000_001L);
        var notifications = new Mock<IRealtimeNotificationPublisher>();
        notifications.Setup(publisher => publisher.PublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<NotificationListItemResponse>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var persistedNotifications = new Mock<INotificationService>();
        persistedNotifications.Setup(service => service.NotifyAsync(
                It.IsAny<NotificationMessage>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new OrderService(
            orders.Object,
            promotions.Object,
            validation.Object,
            Mock.Of<IPayOSPayoutService>(),
            notifications.Object,
            foods.Object,
            Mock.Of<ILogger<OrderService>>(),
            new ConfigurationBuilder().Build(),
            payos.Object,
            codeGenerator.Object,
            Mock.Of<IBoothRepository>(),
            Mock.Of<IPromotionUsageRepository>(),
            persistedNotifications.Object);

        var response = await service.CreateOrderAsync(new CreateOrderDto
        {
            CheckoutRequestId = Guid.NewGuid(),
            CustomerId = customerId,
            BoothId = boothId,
            BoothOwnerId = ownerId,
            PaymentMethod = PaymentType.PayOS,
            PromotionCode = "FREE",
            Items =
            [
                new CartItemDto
                {
                    FoodItemId = food.Id,
                    Quantity = 1,
                    UnitPrice = 60_000m
                }
            ]
        });

        Assert.NotNull(savedOrder);
        Assert.Equal(OrderStatus.Preparing, savedOrder.Status);
        Assert.Equal(0m, savedOrder.FinalAmount);
        var payment = Assert.Single(savedOrder.Payments);
        Assert.Equal(0m, payment.Amount);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(PaymentGateway.None, payment.Gateway);
        Assert.Null(payment.PayOSOrderCode);
        Assert.NotNull(payment.PaidAt);
        Assert.Equal(PromotionUsageStatus.Consumed, Assert.Single(savedOrder.PromotionUsages).Status);
        Assert.Null(response.Data!.PaymentUrl);
        payos.Verify(provider => provider.CreatePaymentLinkAsync(
            It.IsAny<PayOSPaymentRequest>()), Times.Never);
        orders.Verify(repository => repository.CommitTransactionAsync(), Times.Once);
        persistedNotifications.Verify(service => service.NotifyAsync(
            It.Is<NotificationMessage>(message =>
                message.UserId == ownerId
                && message.Type == NotificationType.OrderCreated
                && message.ReferenceType == "Order"
                && message.ReferenceId == savedOrder.Id
                && message.BoothId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CashOrder_UsesNoExternalPaymentGateway_AndStaysPendingUntilBoothCollection()
    {
        var customerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var food = CreateOrderableFood(boothId, ownerId, 45_000m);
        var orders = new Mock<IOrderRepository>();
        Order? savedOrder = null;
        orders.Setup(repository => repository.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(order => savedOrder = order)
            .Returns(Task.CompletedTask);
        orders.Setup(repository => repository.BeginTransactionAsync()).Returns(Task.CompletedTask);
        orders.Setup(repository => repository.CommitTransactionAsync()).Returns(Task.CompletedTask);
        orders.Setup(repository => repository.RollbackTransactionAsync()).Returns(Task.CompletedTask);
        orders.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);
        var foods = new Mock<IFoodItemRepository>();
        foods.Setup(repository => repository.GetAllFoodItemsByIdsAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync([food]);
        var payos = new Mock<IPayOSService>();
        var codeGenerator = new Mock<IPayOSOrderCodeGenerator>();
        codeGenerator.Setup(generator => generator.GenerateAsync(PayOSOrderSource.Order))
            .ReturnsAsync(100_000_000_000_003L);

        var service = new OrderService(
            orders.Object,
            Mock.Of<IPromotionRepository>(),
            Mock.Of<IPromotionValidationService>(),
            Mock.Of<IPayOSPayoutService>(),
            Mock.Of<IRealtimeNotificationPublisher>(),
            foods.Object,
            Mock.Of<ILogger<OrderService>>(),
            new ConfigurationBuilder().Build(),
            payos.Object,
            codeGenerator.Object,
            Mock.Of<IBoothRepository>(),
            Mock.Of<IPromotionUsageRepository>(),
            Mock.Of<INotificationService>());

        var response = await service.CreateOrderAsync(new CreateOrderDto
        {
            CheckoutRequestId = Guid.NewGuid(),
            CustomerId = customerId,
            BoothId = boothId,
            BoothOwnerId = ownerId,
            PaymentMethod = PaymentType.Cash,
            Items = [new CartItemDto { FoodItemId = food.Id, Quantity = 1, UnitPrice = 45_000m }]
        });

        Assert.NotNull(savedOrder);
        Assert.Equal(OrderStatus.Placed, savedOrder.Status);
        var payment = Assert.Single(savedOrder.Payments);
        Assert.Equal(PaymentType.Cash, payment.Type);
        Assert.Equal(PaymentGateway.None, payment.Gateway);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.CheckoutUrl);
        Assert.Null(response.Data!.PaymentUrl);
        payos.Verify(provider => provider.CreatePaymentLinkAsync(It.IsAny<PayOSPaymentRequest>()), Times.Never);
    }

    [Fact]
    public async Task CancellingZeroPaymentOrder_DoesNotRequireRefundBankDetails()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            BoothOwnerId = Guid.NewGuid(),
            OrderCode = 100_000_000_000_002L,
            Status = OrderStatus.Preparing,
            FinalAmount = 0m
        };
        order.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            BoothOwnerId = order.BoothOwnerId,
            Amount = 0m,
            Gateway = PaymentGateway.None,
            Type = PaymentType.PayOS,
            Status = PaymentStatus.Paid
        });

        var orders = new Mock<IOrderRepository>();
        orders.Setup(repository => repository.GetOrderByCodeForUpdateAsync(order.OrderCode))
            .ReturnsAsync(order);
        orders.Setup(repository => repository.BeginTransactionAsync()).Returns(Task.CompletedTask);
        orders.Setup(repository => repository.CommitTransactionAsync()).Returns(Task.CompletedTask);
        orders.Setup(repository => repository.RollbackTransactionAsync()).Returns(Task.CompletedTask);
        orders.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);
        var notifications = new Mock<IRealtimeNotificationPublisher>();
        notifications.Setup(publisher => publisher.PublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<NotificationListItemResponse>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new OrderService(
            orders.Object,
            Mock.Of<IPromotionRepository>(),
            Mock.Of<IPromotionValidationService>(),
            Mock.Of<IPayOSPayoutService>(),
            notifications.Object,
            Mock.Of<IFoodItemRepository>(),
            Mock.Of<ILogger<OrderService>>(),
            new ConfigurationBuilder().Build(),
            Mock.Of<IPayOSService>(),
            Mock.Of<IPayOSOrderCodeGenerator>(),
            Mock.Of<IBoothRepository>(),
            Mock.Of<IPromotionUsageRepository>());

        var result = await service.CancelOrderByBoothOwnerAsync(
            order.BoothOwnerId,
            order.OrderCode,
            new RefundQRRequest { RefundReason = "Out of stock" });

        Assert.True(result.Success);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(PaymentStatus.Cancelled, Assert.Single(order.Payments).Status);
        orders.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    private static FoodItem CreateOrderableFood(Guid boothId, Guid ownerId, decimal price)
    {
        var market = new NightMarket
        {
            Id = Guid.NewGuid(),
            Name = "Market",
            Address = "Ho Chi Minh City",
            Status = NightMarketStatus.Open,
            ModerationStatus = ModerationStatus.Active,
            OpeningHours = new TimeOnly(0, 0),
            ClosingHours = new TimeOnly(23, 59),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var booth = new Booth
        {
            Id = boothId,
            RegistrationId = Guid.NewGuid(),
            NightMarketId = market.Id,
            BoothOwnerId = ownerId,
            BoothName = "Booth",
            Status = BoothStatus.Active,
            NightMarket = market,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var category = new FoodCategory
        {
            Id = Guid.NewGuid(),
            BoothId = boothId,
            Name = "Category",
            Booth = booth,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return new FoodItem
        {
            Id = Guid.NewGuid(),
            BoothId = boothId,
            CategoryId = category.Id,
            Name = "Food",
            Price = price,
            IsAvailable = true,
            Booth = booth,
            Category = category,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}

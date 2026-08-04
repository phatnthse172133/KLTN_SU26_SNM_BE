using System.Text.Json;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Orders;
using ApplicationLayer.Services.Promotions;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class CustomerCheckoutServiceTests
{
    [Theory]
    [InlineData("PayOS", PaymentType.PayOS)]
    [InlineData("Cash", PaymentType.Cash)]
    public void CheckoutRequest_AcceptsTheStringPaymentContractUsedByTheCustomerApp(
        string wireValue,
        PaymentType expected)
    {
        var request = JsonSerializer.Deserialize<CheckoutCartBoothRequest>(
            $$"""{"checkoutRequestId":"{{Guid.NewGuid()}}","boothId":"{{Guid.NewGuid()}}","paymentMethod":"{{wireValue}}"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        Assert.Equal(expected, request.PaymentMethod);
    }

    [Fact]
    public async Task Checkout_UsesAuthoritativeBoothItems_AndDelegatesRemovalToOrderTransaction()
    {
        var customerId = Guid.NewGuid();
        var booth = new Booth { Id = Guid.NewGuid(), BoothOwnerId = Guid.NewGuid(), BoothName = "Booth" };
        var food = new FoodItem { Id = Guid.NewGuid(), BoothId = booth.Id, Booth = booth, Name = "Food", Price = 42_000m, IsAvailable = true };
        var cart = new Cart { Id = Guid.NewGuid(), CustomerId = customerId };
        var item = new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, FoodItemId = food.Id, FoodItem = food, Quantity = 3 };
        var carts = new Mock<ICartRepository>();
        var cartItems = new Mock<ICartItemRepository>();
        var booths = new Mock<IBoothRepository>();
        var orders = new Mock<IOrderService>();
        var orderRepository = new Mock<IOrderRepository>();
        CreateOrderDto? captured = null;

        carts.Setup(x => x.GetActiveByCustomerAsync(customerId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        cartItems.Setup(x => x.GetActiveByCartAndBoothAsync(cart.Id, booth.Id, It.IsAny<CancellationToken>())).ReturnsAsync([item]);
        cartItems.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        booths.Setup(x => x.GetByIdAsync(booth.Id)).ReturnsAsync(booth);
        orders.Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderDto>()))
            .Callback<CreateOrderDto>(dto => captured = dto)
            .ReturnsAsync(ApiResponse<OrderResponseDto>.SuccessResponse(new OrderResponseDto { OrderId = Guid.NewGuid() }));

        var service = new CustomerCheckoutService(carts.Object, cartItems.Object, booths.Object, orders.Object, orderRepository.Object);
        await service.CheckoutBoothAsync(customerId, new CheckoutCartBoothRequest
        {
            CheckoutRequestId = Guid.NewGuid(), BoothId = booth.Id, PaymentMethod = PaymentType.PayOS,
            PromotionCode = "  SAVE10  ", Note = "  no onion  "
        });

        Assert.NotNull(captured);
        Assert.Equal(customerId, captured!.CustomerId);
        Assert.Equal(booth.BoothOwnerId, captured.BoothOwnerId);
        Assert.Equal("SAVE10", captured.PromotionCode);
        Assert.Equal("no onion", captured.Note);
        Assert.Collection(captured.Items, line => { Assert.Equal(food.Id, line.FoodItemId); Assert.Equal(3, line.Quantity); Assert.Equal(42_000m, line.UnitPrice); });
        Assert.Equal([item.Id], captured.CheckoutCartItemIds);
        Assert.False(item.IsDeleted);
        cartItems.Verify(x => x.SaveChangesAsync(), Times.Never);
        cartItems.Verify(x => x.GetActiveByCartAndBoothAsync(cart.Id, booth.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Checkout_OrderCreationFails_DoesNotDeleteCartItems()
    {
        var customerId = Guid.NewGuid();
        var booth = new Booth { Id = Guid.NewGuid(), BoothOwnerId = Guid.NewGuid(), BoothName = "Booth" };
        var food = new FoodItem { Id = Guid.NewGuid(), BoothId = booth.Id, Booth = booth, Name = "Food", Price = 42_000m, IsAvailable = true };
        var cart = new Cart { Id = Guid.NewGuid(), CustomerId = customerId };
        var item = new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, FoodItemId = food.Id, FoodItem = food, Quantity = 1 };
        var carts = new Mock<ICartRepository>();
        var cartItems = new Mock<ICartItemRepository>();
        var booths = new Mock<IBoothRepository>();
        var orders = new Mock<IOrderService>();

        carts.Setup(x => x.GetActiveByCustomerAsync(customerId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        cartItems.Setup(x => x.GetActiveByCartAndBoothAsync(cart.Id, booth.Id, It.IsAny<CancellationToken>())).ReturnsAsync([item]);
        booths.Setup(x => x.GetByIdAsync(booth.Id)).ReturnsAsync(booth);
        orders.Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderDto>())).ThrowsAsync(new InvalidOperationException("database failure"));

        var service = new CustomerCheckoutService(carts.Object, cartItems.Object, booths.Object, orders.Object, Mock.Of<IOrderRepository>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckoutBoothAsync(customerId, new CheckoutCartBoothRequest
        {
            CheckoutRequestId = Guid.NewGuid(), BoothId = booth.Id, PaymentMethod = PaymentType.Cash
        }));

        Assert.False(item.IsDeleted);
        cartItems.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateCustomerOrder_TwoBoothCart_UsesOnlyRequestedBooth()
    {
        var customerId = Guid.NewGuid();
        var market = new NightMarket
        {
            Id = Guid.NewGuid(), Name = "Market", Address = "Ho Chi Minh City",
            Status = NightMarketStatus.Open, ModerationStatus = ModerationStatus.Active,
            OpeningHours = new TimeOnly(0, 0), ClosingHours = new TimeOnly(23, 59)
        };
        var boothA = new Booth { Id = Guid.NewGuid(), NightMarketId = market.Id, NightMarket = market, BoothOwnerId = Guid.NewGuid(), BoothName = "A", Status = BoothStatus.Active };
        var boothB = new Booth { Id = Guid.NewGuid(), NightMarketId = market.Id, NightMarket = market, BoothOwnerId = Guid.NewGuid(), BoothName = "B", Status = BoothStatus.Active };
        var categoryA = new FoodCategory { Id = Guid.NewGuid(), BoothId = boothA.Id, Booth = boothA, Name = "A foods" };
        var categoryB = new FoodCategory { Id = Guid.NewGuid(), BoothId = boothB.Id, Booth = boothB, Name = "B foods" };
        var foodA = new FoodItem { Id = Guid.NewGuid(), BoothId = boothA.Id, Booth = boothA, CategoryId = categoryA.Id, Category = categoryA, Name = "Food A", Price = 30_000m, IsAvailable = true };
        var foodB = new FoodItem { Id = Guid.NewGuid(), BoothId = boothB.Id, Booth = boothB, CategoryId = categoryB.Id, Category = categoryB, Name = "Food B", Price = 40_000m, IsAvailable = true };
        var cart = new Cart { Id = Guid.NewGuid(), CustomerId = customerId };
        var itemA = new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, FoodItemId = foodA.Id, FoodItem = foodA, Quantity = 1 };
        var itemB = new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, FoodItemId = foodB.Id, FoodItem = foodB, Quantity = 2 };
        var carts = new Mock<ICartRepository>();
        var cartItems = new Mock<ICartItemRepository>();
        var booths = new Mock<IBoothRepository>();
        var orders = new Mock<IOrderService>();
        var orderRepository = new Mock<IOrderRepository>();
        CreateOrderDto? captured = null;

        carts.Setup(x => x.GetActiveByCustomerAsync(customerId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        cartItems.Setup(x => x.GetActiveByCartAndBoothAsync(cart.Id, boothA.Id, It.IsAny<CancellationToken>())).ReturnsAsync([itemA]);
        booths.Setup(x => x.GetByIdAsync(boothA.Id)).ReturnsAsync(boothA);
        orders.Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderDto>()))
            .Callback<CreateOrderDto>(dto => captured = dto)
            .ReturnsAsync(ApiResponse<OrderResponseDto>.SuccessResponse(new OrderResponseDto { OrderId = Guid.NewGuid() }));

        var service = new CustomerCheckoutService(
            carts.Object, cartItems.Object, booths.Object, orders.Object, orderRepository.Object,
            Mock.Of<IPromotionRepository>(), Mock.Of<IPromotionValidationService>());
        await service.CreateOrderAsync(customerId, new CreateCustomerOrderRequest
        {
            BoothId = boothA.Id,
            PaymentMethod = PaymentType.Cash
        }, Guid.NewGuid().ToString("D"));

        Assert.NotNull(captured);
        Assert.Equal(boothA.Id, captured.BoothId);
        Assert.Equal([itemA.Id], captured.CheckoutCartItemIds);
        Assert.DoesNotContain(captured.Items, line => line.FoodItemId == itemB.FoodItemId);
        Assert.False(itemA.IsDeleted);
        Assert.False(itemB.IsDeleted);
        cartItems.Verify(x => x.GetActiveByCartAndBoothAsync(cart.Id, boothB.Id, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Checkout_ReusedRequestId_ReturnsExistingOrderWithoutCreatingAnother()
    {
        var customerId = Guid.NewGuid();
        var checkoutId = Guid.NewGuid();
        var existing = new Order { Id = Guid.NewGuid(), CustomerId = customerId, CheckoutRequestId = checkoutId, OrderCode = 1234, Status = OrderStatus.Placed };
        var orders = new Mock<IOrderService>();
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository.Setup(x => x.GetByCheckoutRequestAsync(customerId, checkoutId)).ReturnsAsync(existing);
        var service = new CustomerCheckoutService(Mock.Of<ICartRepository>(), Mock.Of<ICartItemRepository>(), Mock.Of<IBoothRepository>(), orders.Object, orderRepository.Object);

        var response = await service.CheckoutBoothAsync(customerId, new CheckoutCartBoothRequest
        {
            CheckoutRequestId = checkoutId, BoothId = Guid.NewGuid(), PaymentMethod = PaymentType.Cash
        });

        Assert.Equal(existing.Id, response.Data!.OrderId);
        orders.Verify(x => x.CreateOrderAsync(It.IsAny<CreateOrderDto>()), Times.Never);
    }

    [Fact]
    public async Task Checkout_EmptyRequestId_IsRejectedBeforeRepositoryAccess()
    {
        var orderRepository = new Mock<IOrderRepository>();
        var service = new CustomerCheckoutService(Mock.Of<ICartRepository>(), Mock.Of<ICartItemRepository>(), Mock.Of<IBoothRepository>(), Mock.Of<IOrderService>(), orderRepository.Object);

        var error = await Assert.ThrowsAsync<AppException>(() => service.CheckoutBoothAsync(Guid.NewGuid(), new CheckoutCartBoothRequest
        {
            CheckoutRequestId = Guid.Empty, BoothId = Guid.NewGuid(), PaymentMethod = PaymentType.PayOS
        }));

        Assert.Equal("CHECKOUT_REQUEST_ID_REQUIRED", error.ErrorCode);
        orderRepository.Verify(x => x.GetByCheckoutRequestAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }
}

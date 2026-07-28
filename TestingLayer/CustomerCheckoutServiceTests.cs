using System.Text.Json;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Orders;
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
    public async Task Checkout_UsesAuthoritativeCartQuantityAndCurrentPrice_ThenRemovesBoothItems()
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
        Assert.True(item.IsDeleted);
        cartItems.Verify(x => x.SaveChangesAsync(), Times.Once);
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

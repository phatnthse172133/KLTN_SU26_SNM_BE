using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.Carts;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class CartCheckoutOrderabilityTests
{
    private static readonly DateTime DuringOpeningUtc =
        new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc); // 19:00 in Vietnam

    [Fact]
    public void Orderability_AllCurrentCustomerRulesPass_CanOrder()
    {
        var food = CreateFood();

        var result = CustomerOrderability.Evaluate(food, DuringOpeningUtc);

        Assert.True(result.CanOrder);
        Assert.Null(result.ReasonCode);
    }

    [Fact]
    public async Task AddItem_AllRulesPass_AddsCartItem()
    {
        var customerId = Guid.NewGuid();
        var food = CreateFood();
        var cart = new Cart { Id = Guid.NewGuid(), CustomerId = customerId };
        var carts = new Mock<ICartRepository>();
        var cartItems = new Mock<ICartItemRepository>();
        var foods = new Mock<IFoodItemRepository>();
        var mapper = new Mock<IMapper>();
        carts.Setup(repository => repository.GetActiveByCustomerAsync(
                customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        cartItems.Setup(repository => repository.GetActiveByCartAndFoodAsync(
                cart.Id, food.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartItem?)null);
        foods.Setup(repository => repository.GetForCartAsync(
                food.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(food);
        mapper.Setup(service => service.Map<CartItemResponse>(It.IsAny<CartItem>()))
            .Returns(new CartItemResponse());
        var service = new CartService(
            carts.Object,
            cartItems.Object,
            foods.Object,
            mapper.Object,
            new FixedTimeProvider(DuringOpeningUtc));

        var response = await service.AddItemAsync(customerId, new AddCartItemRequest
        {
            FoodItemId = food.Id,
            Quantity = 2
        });

        Assert.True(response.Success);
        Assert.True(response.Data!.CanOrder);
        Assert.Equal(25_000m, response.Data.CurrentUnitPrice);
        cartItems.Verify(repository => repository.AddAsync(
            It.Is<CartItem>(item => item.Quantity == 2 && item.FoodItemId == food.Id)),
            Times.Once);
    }

    [Fact]
    public async Task AddItem_MarketCloses_DoesNotAddOrIncreaseItem()
    {
        var customerId = Guid.NewGuid();
        var food = CreateFood();
        food.Booth.NightMarket.Status = NightMarketStatus.Closed;
        var foods = new Mock<IFoodItemRepository>();
        var cartItems = new Mock<ICartItemRepository>();
        foods.Setup(repository => repository.GetForCartAsync(
                food.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(food);
        var service = new CartService(
            Mock.Of<ICartRepository>(),
            cartItems.Object,
            foods.Object,
            Mock.Of<IMapper>(),
            new FixedTimeProvider(DuringOpeningUtc));

        var error = await Assert.ThrowsAsync<AppException>(() =>
            service.AddItemAsync(customerId, new AddCartItemRequest
            {
                FoodItemId = food.Id,
                Quantity = 1
            }));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal(CustomerOrderability.MarketClosed, error.ErrorCode);
        cartItems.Verify(repository => repository.AddAsync(It.IsAny<CartItem>()), Times.Never);
        cartItems.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Theory]
    [InlineData(NightMarketStatus.Upcoming)]
    [InlineData(NightMarketStatus.Closed)]
    public void Orderability_MarketIsNotOperational_ReturnsMarketClosed(
        NightMarketStatus status)
    {
        var food = CreateFood();
        food.Booth.NightMarket.Status = status;

        var result = CustomerOrderability.Evaluate(food, DuringOpeningUtc);

        Assert.False(result.CanOrder);
        Assert.Equal(CustomerOrderability.MarketClosed, result.ReasonCode);
    }

    [Fact]
    public void Orderability_SuspendedMarket_ReturnsMarketUnavailable()
    {
        var food = CreateFood();
        food.Booth.NightMarket.ModerationStatus = ModerationStatus.Suspended;

        var result = CustomerOrderability.Evaluate(food, DuringOpeningUtc);

        Assert.False(result.CanOrder);
        Assert.Equal(CustomerOrderability.MarketUnavailable, result.ReasonCode);
    }

    [Fact]
    public void Orderability_OutsideBoothSchedule_ReturnsBoothClosed()
    {
        var food = CreateFood();
        food.Booth.OpenTime = new TimeOnly(20, 0);
        food.Booth.CloseTime = new TimeOnly(23, 0);

        var result = CustomerOrderability.Evaluate(food, DuringOpeningUtc);

        Assert.False(result.CanOrder);
        Assert.Equal(CustomerOrderability.BoothClosed, result.ReasonCode);
    }

    [Fact]
    public void Orderability_UnavailableFood_ReturnsFoodUnavailable()
    {
        var food = CreateFood();
        food.IsAvailable = false;

        var result = CustomerOrderability.Evaluate(food, DuringOpeningUtc);

        Assert.False(result.CanOrder);
        Assert.Equal(CustomerOrderability.FoodUnavailable, result.ReasonCode);
    }

    [Fact]
    public void Orderability_OvernightSchedulesUseHalfOpenIntervals()
    {
        var food = CreateFood();
        food.Booth.NightMarket.OpeningHours = new TimeOnly(18, 0);
        food.Booth.NightMarket.ClosingHours = new TimeOnly(2, 0);
        food.Booth.OpenTime = new TimeOnly(20, 0);
        food.Booth.CloseTime = new TimeOnly(1, 0);

        var atMidnightVietnam = new DateTime(2026, 7, 27, 17, 0, 0, DateTimeKind.Utc);
        var atBoothCloseVietnam = new DateTime(2026, 7, 27, 18, 0, 0, DateTimeKind.Utc);

        Assert.True(CustomerOrderability.Evaluate(food, atMidnightVietnam).CanOrder);
        Assert.Equal(
            CustomerOrderability.BoothClosed,
            CustomerOrderability.Evaluate(food, atBoothCloseVietnam).ReasonCode);
    }

    [Fact]
    public void Orderability_BoothOvernightScheduleCannotExtendMarketHours()
    {
        var food = CreateFood();
        food.Booth.OpenTime = new TimeOnly(18, 0);
        food.Booth.CloseTime = new TimeOnly(2, 0);
        var halfPastMidnightVietnam =
            new DateTime(2026, 7, 27, 17, 30, 0, DateTimeKind.Utc);

        var result = CustomerOrderability.Evaluate(food, halfPastMidnightVietnam);

        Assert.False(result.CanOrder);
        Assert.Equal(CustomerOrderability.MarketClosed, result.ReasonCode);
    }

    [Fact]
    public async Task CartRepository_PreservesSoftDeletedFoodAsInvalidCartItem()
    {
        await using var context = CreateContext();
        var food = CreateFood();
        food.IsDeleted = true;
        var cart = new Cart
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CreatedAt = DuringOpeningUtc,
            UpdatedAt = DuringOpeningUtc
        };
        var cartItem = new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = cart.Id,
            FoodItemId = food.Id,
            Quantity = 1,
            Cart = cart,
            FoodItem = food,
            CreatedAt = DuringOpeningUtc,
            UpdatedAt = DuringOpeningUtc
        };

        context.AddRange(food.Booth.NightMarket, food.Booth, food.Category, food, cart, cartItem);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var loaded = Assert.Single(
            await new CartItemRepository(context).GetActiveByCartAsync(cart.Id));

        Assert.True(loaded.FoodItem.IsDeleted);
        Assert.False(CustomerOrderability.Evaluate(loaded.FoodItem, DuringOpeningUtc).CanOrder);
    }

    private static FoodItem CreateFood()
    {
        var market = new NightMarket
        {
            Id = Guid.NewGuid(),
            Name = "Market",
            Address = "Ho Chi Minh City",
            Status = NightMarketStatus.Open,
            ModerationStatus = ModerationStatus.Active,
            OpeningHours = new TimeOnly(18, 0),
            ClosingHours = new TimeOnly(23, 0),
            CreatedAt = DuringOpeningUtc,
            UpdatedAt = DuringOpeningUtc
        };
        var booth = new Booth
        {
            Id = Guid.NewGuid(),
            RegistrationId = Guid.NewGuid(),
            NightMarketId = market.Id,
            BoothOwnerId = Guid.NewGuid(),
            BoothName = "Booth",
            Status = BoothStatus.Active,
            NightMarket = market,
            CreatedAt = DuringOpeningUtc,
            UpdatedAt = DuringOpeningUtc
        };
        var category = new FoodCategory
        {
            Id = Guid.NewGuid(),
            BoothId = booth.Id,
            Name = "Category",
            Booth = booth,
            CreatedAt = DuringOpeningUtc,
            UpdatedAt = DuringOpeningUtc
        };
        return new FoodItem
        {
            Id = Guid.NewGuid(),
            BoothId = booth.Id,
            CategoryId = category.Id,
            Name = "Food",
            Price = 25_000m,
            IsAvailable = true,
            Booth = booth,
            Category = category,
            CreatedAt = DuringOpeningUtc,
            UpdatedAt = DuringOpeningUtc
        };
    }

    private static SNMDbContext CreateContext()
        => new(new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}

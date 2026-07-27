using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Promotions;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class PromotionValidationServiceTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SpecificFoodPercentage_UsesEffectivePriceAndMaximumDiscount()
    {
        var (service, usages) = CreateService();
        var boothId = Guid.NewGuid();
        var eligible = CreateItem(boothId, price: 100m, quantity: 2, currentPrice: 80m);
        var other = CreateItem(boothId, price: 200m, quantity: 1, currentPrice: 150m);
        var promotion = CreatePromotion(boothId, PromotionScope.SpecificFoodItems);
        promotion.DiscountType = DiscountType.Percentage;
        promotion.DiscountValue = 50m;
        promotion.MaximumDiscountAmount = 50m;
        promotion.PromotionFoodItems.Add(new PromotionFoodItem
        {
            PromotionId = promotion.Id,
            FoodItemId = eligible.FoodItemId,
            FoodItem = eligible.FoodItem
        });

        var result = await service.ValidateAsync(Guid.NewGuid(), promotion, [eligible, other]);

        Assert.Equal(310m, result.TotalAmount);
        Assert.Equal(160m, result.EligibleAmount);
        Assert.Equal(50m, result.DiscountAmount);
        Assert.Equal(260m, result.FinalAmount);
        Assert.Equal(80m, result.CalculatedDiscount);
        Assert.Equal(50m, result.ActualDiscount);
        usages.Verify(repository => repository.CountActiveAsync(promotion.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MinimumOrder_UsesCurrentEffectiveBoothSubtotal()
    {
        var (service, _) = CreateService();
        var boothId = Guid.NewGuid();
        var item = CreateItem(boothId, price: 120m, quantity: 1, currentPrice: 90m);
        var promotion = CreatePromotion(boothId, PromotionScope.EntireBoothOrder);
        promotion.MinimumOrderAmount = 100m;

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.ValidateAsync(Guid.NewGuid(), promotion, [item]));

        Assert.Equal("PROMOTION_MINIMUM_ORDER_NOT_MET", exception.ErrorCode);
    }

    [Fact]
    public async Task CustomerLimit_ReturnsStableConflictCode()
    {
        var customerId = Guid.NewGuid();
        var (service, usages) = CreateService();
        var boothId = Guid.NewGuid();
        var item = CreateItem(boothId, price: 100m, quantity: 1);
        var promotion = CreatePromotion(boothId, PromotionScope.EntireBoothOrder);
        promotion.UsageLimitPerCustomer = 1;
        usages.Setup(repository => repository.CountActiveByCustomerAsync(
                promotion.Id,
                customerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.ValidateAsync(customerId, promotion, [item]));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("PROMOTION_CUSTOMER_LIMIT_REACHED", exception.ErrorCode);
    }

    [Fact]
    public async Task FuturePromotion_ReturnsNotStartedBeforeQueryingUsage()
    {
        var (service, usages) = CreateService();
        var boothId = Guid.NewGuid();
        var promotion = CreatePromotion(boothId, PromotionScope.EntireBoothOrder);
        promotion.StartDate = UtcNow.AddMinutes(1);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.ValidateAsync(
            Guid.NewGuid(),
            promotion,
            [CreateItem(boothId, 100m, 1)]));

        Assert.Equal("PROMOTION_NOT_STARTED", exception.ErrorCode);
        usages.Verify(repository => repository.CountActiveAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvalidPersistedPercentage_IsRejectedAtApplicationTime()
    {
        var (service, _) = CreateService();
        var boothId = Guid.NewGuid();
        var promotion = CreatePromotion(boothId, PromotionScope.EntireBoothOrder);
        promotion.DiscountType = DiscountType.Percentage;
        promotion.DiscountValue = 101m;

        var exception = await Assert.ThrowsAsync<AppException>(() => service.ValidateAsync(
            Guid.NewGuid(),
            promotion,
            [CreateItem(boothId, 100m, 1)]));

        Assert.Equal("INVALID_PROMOTION_VALUE", exception.ErrorCode);
    }

    [Theory]
    [InlineData(200, 60, 60, 0)]
    [InlineData(60, 60, 60, 0)]
    [InlineData(20, 60, 20, 40)]
    public async Task FixedDiscount_IsClampedToSubtotal(
        decimal discountValue,
        decimal subtotal,
        decimal expectedDiscount,
        decimal expectedFinal)
    {
        var (service, _) = CreateService();
        var boothId = Guid.NewGuid();
        var promotion = CreatePromotion(boothId, PromotionScope.EntireBoothOrder);
        promotion.DiscountValue = discountValue;

        var result = await service.ValidateAsync(
            Guid.NewGuid(),
            promotion,
            [CreateItem(boothId, subtotal, 1)]);

        Assert.Equal(discountValue, result.CalculatedDiscount);
        Assert.Equal(expectedDiscount, result.ActualDiscount);
        Assert.Equal(expectedFinal, result.FinalAmount);
        Assert.True(result.ActualDiscount <= result.EligibleSubtotal);
        Assert.True(result.FinalAmount >= 0);
    }

    [Fact]
    public async Task Percentage100_CannotExceedEligibleSubtotalAfterRounding()
    {
        var (service, _) = CreateService();
        var boothId = Guid.NewGuid();
        var promotion = CreatePromotion(boothId, PromotionScope.EntireBoothOrder);
        promotion.DiscountType = DiscountType.Percentage;
        promotion.DiscountValue = 100m;

        var result = await service.ValidateAsync(
            Guid.NewGuid(),
            promotion,
            [CreateItem(boothId, 0.005m, 1)]);

        Assert.Equal(0.005m, result.ActualDiscount);
        Assert.Equal(0m, result.FinalAmount);
    }

    [Fact]
    public async Task MinimumOrder_ExactlyMet_IsEligible()
    {
        var (service, _) = CreateService();
        var boothId = Guid.NewGuid();
        var promotion = CreatePromotion(boothId, PromotionScope.EntireBoothOrder);
        promotion.MinimumOrderAmount = 100m;

        var result = await service.ValidateAsync(
            Guid.NewGuid(),
            promotion,
            [CreateItem(boothId, 100m, 1)]);

        Assert.Equal(100m, result.OrderSubtotal);
    }

    [Fact]
    public async Task ScopedFixedDiscount_IsClampedToScopedSubtotal()
    {
        var (service, _) = CreateService();
        var boothId = Guid.NewGuid();
        var eligible = CreateItem(boothId, 40m, 1);
        var other = CreateItem(boothId, 100m, 1);
        var promotion = CreatePromotion(boothId, PromotionScope.SpecificFoodItems);
        promotion.DiscountValue = 80m;
        promotion.PromotionFoodItems.Add(new PromotionFoodItem
        {
            PromotionId = promotion.Id,
            FoodItemId = eligible.FoodItemId,
            FoodItem = eligible.FoodItem
        });

        var result = await service.ValidateAsync(
            Guid.NewGuid(), promotion, [eligible, other]);

        Assert.Equal(140m, result.OrderSubtotal);
        Assert.Equal(40m, result.EligibleSubtotal);
        Assert.Equal(40m, result.ActualDiscount);
        Assert.Equal(100m, result.FinalAmount);
    }

    [Fact]
    public async Task PersistedFixedPromotion_WithMaximumCap_IsRejected()
    {
        var (service, _) = CreateService();
        var boothId = Guid.NewGuid();
        var promotion = CreatePromotion(boothId, PromotionScope.EntireBoothOrder);
        promotion.MaximumDiscountAmount = 5m;

        var exception = await Assert.ThrowsAsync<AppException>(() => service.ValidateAsync(
            Guid.NewGuid(), promotion, [CreateItem(boothId, 100m, 1)]));

        Assert.Equal("INVALID_PROMOTION_VALUE", exception.ErrorCode);
    }

    [Fact]
    public async Task NegativeEffectivePrice_IsRejected()
    {
        var (service, _) = CreateService();
        var boothId = Guid.NewGuid();
        var promotion = CreatePromotion(boothId, PromotionScope.EntireBoothOrder);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.ValidateAsync(
            Guid.NewGuid(), promotion, [CreateItem(boothId, -1m, 1)]));

        Assert.Equal("INVALID_ORDER_SUBTOTAL", exception.ErrorCode);
    }

    private static (PromotionValidationService Service, Mock<IPromotionUsageRepository> Usages) CreateService()
    {
        var usages = new Mock<IPromotionUsageRepository>();
        usages.Setup(repository => repository.CountActiveAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        usages.Setup(repository => repository.CountActiveByCustomerAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(provider => provider.GetUtcNow())
            .Returns(new DateTimeOffset(UtcNow));
        return (new PromotionValidationService(usages.Object, timeProvider.Object), usages);
    }

    private static Promotion CreatePromotion(Guid boothId, PromotionScope scope)
        => new()
        {
            Id = Guid.NewGuid(),
            BoothId = boothId,
            Title = "Promotion",
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 10m,
            Scope = scope,
            StartDate = UtcNow.AddDays(-1),
            EndDate = UtcNow.AddDays(1),
            Status = PromotionStatus.Active
        };

    private static CartItem CreateItem(
        Guid boothId,
        decimal price,
        int quantity,
        decimal? currentPrice = null)
    {
        var foodId = Guid.NewGuid();
        var food = new FoodItem
        {
            Id = foodId,
            BoothId = boothId,
            CategoryId = Guid.NewGuid(),
            Name = "Food",
            Price = price
        };
        if (currentPrice.HasValue)
        {
            food.FoodPrices.Add(new FoodPrice
            {
                Id = Guid.NewGuid(),
                FoodItemId = foodId,
                Price = currentPrice.Value,
                StartDate = UtcNow.AddHours(-1),
                EndDate = UtcNow.AddHours(1),
                CreatedAt = UtcNow.AddHours(-1)
            });
        }

        return new CartItem
        {
            Id = Guid.NewGuid(),
            FoodItemId = foodId,
            Quantity = quantity,
            FoodItem = food
        };
    }
}

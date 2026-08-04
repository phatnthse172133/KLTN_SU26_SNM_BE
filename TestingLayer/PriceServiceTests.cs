using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ApplicationLayer.Services.Prices;
using ApplicationLayer.DTOs.Requests;
using DomainLayer.Entities;
using DomainLayer.Common;
using DomainLayer.InterfaceRepository;
using AutoMapper;
using System.Collections.Generic;
using ApplicationLayer.Exceptions;
using System.Linq.Expressions;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class PriceServiceTests
{
    private readonly Mock<IGenericRepository<Package>> _packagesMock;
    private readonly Mock<IBoothRepository> _boothsMock;
    private readonly Mock<IFoodItemRepository> _foodItemsMock;
    private readonly Mock<IPackagePriceRepository> _packagePricesMock;
    private readonly Mock<IFoodPriceRepository> _foodPricesMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly PriceService _priceService;

    public PriceServiceTests()
    {
        _packagesMock = new Mock<IGenericRepository<Package>>();
        _boothsMock = new Mock<IBoothRepository>();
        _foodItemsMock = new Mock<IFoodItemRepository>();
        _packagePricesMock = new Mock<IPackagePriceRepository>();
        _foodPricesMock = new Mock<IFoodPriceRepository>();
        _mapperMock = new Mock<IMapper>();

        _priceService = new PriceService(
            _boothsMock.Object,
            _foodItemsMock.Object,
            _foodPricesMock.Object,
            _packagesMock.Object,
            _packagePricesMock.Object,
            _mapperMock.Object
        );
    }

    [Fact]
    public async Task CreatePackagePriceAsync_RegularPrice_Success()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var package = new Package { Id = packageId, Price = 100, DurationDays = 30 };
        _packagesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Package, bool>>>()))
                     .ReturnsAsync(package);

        _packagePricesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<PackagePrice, bool>>>()))
                          .ReturnsAsync((PackagePrice)null!);

        var request = new CreatePriceRequest
        {
            Price = 90,
            DurationDays = 15,
            StartDate = null,
            EndDate = null
        };

        _mapperMock.Setup(m => m.Map<PackagePrice>(request)).Returns(new PackagePrice());

        // Act
        var result = await _priceService.CreatePackagePriceAsync(packageId, request);

        // Assert
        Assert.NotNull(result);
        _packagePricesMock.Verify(x => x.AddAsync(It.IsAny<PackagePrice>()), Times.Once);
    }

    [Fact]
    public async Task CreatePackagePriceAsync_Promotion_WithNoBasePrice_ThrowsBadRequest()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        // Duration 30, base price 100
        var package = new Package { Id = packageId, Price = 100, DurationDays = 30 };
        _packagesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Package, bool>>>()))
                     .ReturnsAsync(package);

        _packagePricesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<PackagePrice, bool>>>()))
                          .ReturnsAsync((PackagePrice)null!);

        // Requesting promo for duration 15, but there's no regular price for 15
        var request = new CreatePriceRequest
        {
            Price = 40,
            DurationDays = 15,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(5)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppException>(() => _priceService.CreatePackagePriceAsync(packageId, request));
        Assert.Contains("no regular base price exists", ex.Message);
    }

    [Fact]
    public async Task CreatePackagePriceAsync_Promotion_Valid_Success()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var package = new Package { Id = packageId, Price = 100, DurationDays = 30 };
        _packagesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Package, bool>>>()))
                     .ReturnsAsync(package);

        // No overlapping promos, and base price falls back to package.Price since duration = 30
        _packagePricesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<PackagePrice, bool>>>()))
                          .ReturnsAsync((PackagePrice)null!);

        var request = new CreatePriceRequest
        {
            Price = 80,
            DurationDays = 30,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(5)
        };

        _mapperMock.Setup(m => m.Map<PackagePrice>(request)).Returns(new PackagePrice());

        // Act
        var result = await _priceService.CreatePackagePriceAsync(packageId, request);

        // Assert
        Assert.NotNull(result);
        _packagePricesMock.Verify(x => x.AddAsync(It.IsAny<PackagePrice>()), Times.Once);
    }

    [Fact]
    public async Task CreatePackagePriceAsync_DurationNull_UsesPackageDuration()
    {
        var packageId = Guid.NewGuid();
        var package = new Package { Id = packageId, Price = 100, DurationDays = 365 };
        _packagesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Package, bool>>>())).ReturnsAsync(package);

        var request = new CreatePriceRequest { Price = 50, DurationDays = null, StartDate = DateTime.UtcNow.AddDays(1), EndDate = DateTime.UtcNow.AddDays(10) };
        _mapperMock.Setup(m => m.Map<PackagePrice>(request)).Returns(new PackagePrice());

        var result = await _priceService.CreatePackagePriceAsync(packageId, request);

        _packagePricesMock.Verify(x => x.AddAsync(It.Is<PackagePrice>(p => p.DurationDays == 365)), Times.Once);
    }

    [Fact]
    public async Task CreatePackagePriceAsync_DuplicateRegularDuration_ThrowsBadRequest()
    {
        var packageId = Guid.NewGuid();
        var package = new Package { Id = packageId, Price = 100, DurationDays = 30 };
        _packagesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Package, bool>>>())).ReturnsAsync(package);

        _packagePricesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<PackagePrice, bool>>>()))
                          .ReturnsAsync(new PackagePrice { Id = Guid.NewGuid(), DurationDays = 15 }); // existing regular

        var request = new CreatePriceRequest { Price = 90, DurationDays = 15 };

        var ex = await Assert.ThrowsAsync<AppException>(() => _priceService.CreatePackagePriceAsync(packageId, request));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task CreatePackagePriceAsync_MissingStartDateOrEndDate_ThrowsBadRequest()
    {
        var packageId = Guid.NewGuid();
        var package = new Package { Id = packageId, Price = 100, DurationDays = 30 };
        _packagesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Package, bool>>>())).ReturnsAsync(package);

        var request = new CreatePriceRequest { Price = 80, DurationDays = 30, StartDate = DateTime.UtcNow }; // missing EndDate

        var ex = await Assert.ThrowsAsync<AppException>(() => _priceService.CreatePackagePriceAsync(packageId, request));
        Assert.Contains("PROMOTION_DATES_REQUIRED", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePackagePriceAsync_StartDateEqualsEndDate_ThrowsBadRequest()
    {
        var packageId = Guid.NewGuid();
        var package = new Package { Id = packageId, Price = 100, DurationDays = 30 };
        _packagesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Package, bool>>>())).ReturnsAsync(package);

        var date = DateTime.UtcNow.AddDays(1);
        var request = new CreatePriceRequest { Price = 80, DurationDays = 30, StartDate = date, EndDate = date };

        var ex = await Assert.ThrowsAsync<AppException>(() => _priceService.CreatePackagePriceAsync(packageId, request));
        Assert.Contains("INVALID_PROMOTION_DATE", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePackagePriceAsync_PromotionExpired_ThrowsBadRequest()
    {
        var packageId = Guid.NewGuid();
        var package = new Package { Id = packageId, Price = 100, DurationDays = 30 };
        _packagesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Package, bool>>>())).ReturnsAsync(package);

        var request = new CreatePriceRequest { Price = 80, DurationDays = 30, StartDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(-1) };

        var ex = await Assert.ThrowsAsync<AppException>(() => _priceService.CreatePackagePriceAsync(packageId, request));
        Assert.Contains("PROMOTION_ALREADY_EXPIRED", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePackagePriceAsync_PromotionPriceEqualsBasePrice_ThrowsBadRequest()
    {
        var packageId = Guid.NewGuid();
        var package = new Package { Id = packageId, Price = 100, DurationDays = 30 };
        _packagesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Package, bool>>>())).ReturnsAsync(package);

        var request = new CreatePriceRequest { Price = 100, DurationDays = 30, StartDate = DateTime.UtcNow.AddDays(1), EndDate = DateTime.UtcNow.AddDays(10) };

        var ex = await Assert.ThrowsAsync<AppException>(() => _priceService.CreatePackagePriceAsync(packageId, request));
        Assert.Contains("INVALID_PROMOTION_PRICE", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePackagePriceAsync_PromotionPriceGreaterThanBasePrice_ThrowsBadRequest()
    {
        var packageId = Guid.NewGuid();
        var package = new Package { Id = packageId, Price = 100, DurationDays = 30 };
        _packagesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Package, bool>>>())).ReturnsAsync(package);

        var request = new CreatePriceRequest { Price = 150, DurationDays = 30, StartDate = DateTime.UtcNow.AddDays(1), EndDate = DateTime.UtcNow.AddDays(10) };

        var ex = await Assert.ThrowsAsync<AppException>(() => _priceService.CreatePackagePriceAsync(packageId, request));
        Assert.Contains("INVALID_PROMOTION_PRICE", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePackagePriceAsync_PromotionOverlap_ThrowsBadRequest()
    {
        var packageId = Guid.NewGuid();
        var package = new Package { Id = packageId, Price = 100, DurationDays = 30 };
        _packagesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Package, bool>>>())).ReturnsAsync(package);

        // Setup overlapping promotion
        _packagePricesMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<PackagePrice, bool>>>()))
                          .ReturnsAsync(new PackagePrice()); // overlap check

        var request = new CreatePriceRequest { Price = 80, DurationDays = 30, StartDate = DateTime.UtcNow.AddDays(1), EndDate = DateTime.UtcNow.AddDays(10) };

        var ex = await Assert.ThrowsAsync<AppException>(() => _priceService.CreatePackagePriceAsync(packageId, request));
        Assert.Contains("overlaps", ex.Message.ToLower());
    }

    [Fact]
    public void FoodPriceResolver_PrefersLatestDatedPriceOverTimelessPrice()
    {
        var now = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
        var food = new FoodItem { Price = 50_000m };
        food.FoodPrices.Add(new FoodPrice
        {
            Price = 45_000m,
            StartDate = null,
            EndDate = null,
            CreatedAt = now.AddDays(-10)
        });
        food.FoodPrices.Add(new FoodPrice
        {
            Price = 35_000m,
            StartDate = now.AddHours(-1),
            EndDate = now.AddHours(1),
            CreatedAt = now.AddHours(-1)
        });

        Assert.Equal(35_000m, FoodPriceResolver.GetCurrentPrice(food, now));
    }

    [Fact]
    public void FoodPriceResolver_UsesActiveInclusiveRange_AndDeterministicCreatedAtTieBreak()
    {
        var now = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
        var sameStart = now.AddHours(-1);
        var food = new FoodItem { Price = 50_000m };
        food.FoodPrices.Add(new FoodPrice
        {
            Price = 10_000m, StartDate = now.AddDays(-2), EndDate = now.AddSeconds(-1), CreatedAt = now.AddDays(-2)
        });
        food.FoodPrices.Add(new FoodPrice
        {
            Price = 20_000m, StartDate = now.AddSeconds(1), EndDate = now.AddDays(1), CreatedAt = now
        });
        food.FoodPrices.Add(new FoodPrice
        {
            Price = 40_000m, StartDate = sameStart, EndDate = now, CreatedAt = now.AddMinutes(-2)
        });
        food.FoodPrices.Add(new FoodPrice
        {
            Price = 30_000m, StartDate = sameStart, EndDate = now, CreatedAt = now.AddMinutes(-1)
        });

        Assert.Equal(30_000m, FoodPriceResolver.GetCurrentPrice(food, now));
        Assert.Equal(50_000m, FoodPriceResolver.GetCurrentPrice(food, now.AddDays(2)));
    }

}

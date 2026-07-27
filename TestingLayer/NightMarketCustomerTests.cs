using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.NightMarkets;
using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.Subscriptions;
using AutoMapper;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class NightMarketCustomerTests
{
    [Fact]
    public async Task CustomerList_ReturnsOnlyVisibleMarkets_AndProjectsCounts()
    {
        await using var context = CreateContext();
        var visible = CreateMarket("Visible market", NightMarketStatus.Active);
        var draft = CreateMarket("Inactive market", NightMarketStatus.Inactive);
        var suspended = CreateMarket("Suspended market", NightMarketStatus.Active);
        suspended.ModerationStatus = ModerationStatus.Suspended;

        visible.Booths.Add(CreateBooth(visible.Id, BoothStatus.Active));
        visible.Booths.Add(CreateBooth(visible.Id, BoothStatus.Inactive));
        visible.MarketLayouts.Add(new MarketLayout
        {
            Id = Guid.NewGuid(),
            NightMarketId = visible.Id,
            LayoutName = "Public layout",
            Version = 1,
            Width = 100,
            Height = 100,
            Status = MarketLayoutStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        context.NightMarkets.AddRange(visible, draft, suspended);
        await context.SaveChangesAsync();

        var repository = new NightMarketRepository(context);
        var result = await repository.GetCustomerPagedAsync(
            null, null, new TimeOnly(19, 0), 1, 10, "name", true);

        var item = Assert.Single(result.Items);
        Assert.Equal(visible.Id, item.Id);
        Assert.Equal(1, item.ActiveBoothCount);
        Assert.True(item.HasLayout);
    }

    [Fact]
    public async Task CustomerDetail_DoesNotReturnNonVisibleMarkets()
    {
        await using var context = CreateContext();
        var draft = CreateMarket("Inactive market", NightMarketStatus.Inactive);
        var suspended = CreateMarket("Suspended market", NightMarketStatus.Active);
        suspended.ModerationStatus = ModerationStatus.Suspended;
        var cancelled = CreateMarket("Another inactive market", NightMarketStatus.Inactive);
        var deleted = CreateMarket("Deleted market", NightMarketStatus.Active);
        deleted.IsDeleted = true;
        context.NightMarkets.AddRange(draft, suspended, cancelled, deleted);
        await context.SaveChangesAsync();

        var repository = new NightMarketRepository(context);

        Assert.Null(await repository.GetCustomerByIdAsync(draft.Id));
        Assert.Null(await repository.GetCustomerByIdAsync(suspended.Id));
        Assert.Null(await repository.GetCustomerByIdAsync(cancelled.Id));
        Assert.Null(await repository.GetCustomerByIdAsync(deleted.Id));
        Assert.False(await repository.CustomerVisibleExistsAsync(suspended.Id));
        Assert.False(await repository.CustomerVisibleExistsAsync(deleted.Id));
    }

    [Fact]
    public async Task CustomerList_SearchIsTrimmedAndCaseInsensitive()
    {
        await using var context = CreateContext();
        var market = CreateMarket("Ben Thanh Night Market", NightMarketStatus.Active);
        context.NightMarkets.Add(market);
        await context.SaveChangesAsync();

        var repository = new NightMarketRepository(context);
        var result = await repository.GetCustomerPagedAsync(
            "  bEN tHANH  ", null, default, 1, 10, "name", true);

        Assert.Equal(market.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task CustomerList_OpenNowFiltersByStatusAndSameDaySchedule()
    {
        await using var context = CreateContext();
        var open = CreateMarket("Open market", NightMarketStatus.Active);
        open.OpeningHours = new TimeOnly(18, 0);
        open.ClosingHours = new TimeOnly(23, 0);
        var outsideSchedule = CreateMarket("Outside schedule", NightMarketStatus.Active);
        outsideSchedule.OpeningHours = new TimeOnly(8, 0);
        outsideSchedule.ClosingHours = new TimeOnly(17, 0);
        context.NightMarkets.AddRange(open, outsideSchedule);
        await context.SaveChangesAsync();

        var repository = new NightMarketRepository(context);
        var result = await repository.GetCustomerPagedAsync(
            null, true, new TimeOnly(19, 0), 1, 10, "name", true);

        var closedResult = await repository.GetCustomerPagedAsync(
            null, false, new TimeOnly(19, 0), 1, 10, "name", true);

        Assert.Equal(open.Id, Assert.Single(result.Items).Id);
        Assert.Equal(outsideSchedule.Id, Assert.Single(closedResult.Items).Id);
    }

    [Fact]
    public async Task CustomerList_OpenNowSupportsOvernightSchedule()
    {
        await using var context = CreateContext();
        var overnight = CreateMarket("Overnight market", NightMarketStatus.Active);
        overnight.OpeningHours = new TimeOnly(18, 0);
        overnight.ClosingHours = new TimeOnly(2, 0);
        context.NightMarkets.Add(overnight);
        await context.SaveChangesAsync();

        var repository = new NightMarketRepository(context);
        var afterMidnight = await repository.GetCustomerPagedAsync(
            null, true, new TimeOnly(1, 0), 1, 20, "name", true);
        var afternoon = await repository.GetCustomerPagedAsync(
            null, true, new TimeOnly(15, 0), 1, 20, "name", true);

        Assert.Contains(afterMidnight.Items, market => market.Id == overnight.Id);
        Assert.DoesNotContain(afternoon.Items, market => market.Id == overnight.Id);
    }

    [Fact]
    public void Availability_SupportsOvernightScheduleAndExclusiveClosingBoundary()
    {
        Assert.True(NightMarketAvailability.IsWithinSchedule(
            new TimeOnly(18, 0), new TimeOnly(2, 0), new TimeOnly(1, 59)));
        Assert.False(NightMarketAvailability.IsWithinSchedule(
            new TimeOnly(18, 0), new TimeOnly(2, 0), new TimeOnly(2, 0)));
        Assert.False(NightMarketAvailability.IsWithinSchedule(
            new TimeOnly(18, 0), new TimeOnly(18, 0), new TimeOnly(18, 0)));
    }

    [Fact]
    public void Availability_UsesVietnamTimeAndDoesNotFakeMissingHours()
    {
        var scheduled = CreateReadModel(
            NightMarketStatus.Active,
            new TimeOnly(18, 0),
            new TimeOnly(23, 0));
        var missingHours = CreateReadModel(NightMarketStatus.Active, null, null);

        var duringOpening = NightMarketAvailability.Evaluate(
            scheduled,
            new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc));
        var missing = NightMarketAvailability.Evaluate(
            missingHours,
            new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(duringOpening.IsOpenNow);
        Assert.False(missing.IsOpenNow);
        Assert.Equal("Hours unavailable", missing.StatusText);
    }

    [Fact]
    public async Task CustomerBoothAndFoodQueries_ReturnOnlyActiveData_AndEffectivePrice()
    {
        await using var context = CreateContext();
        var market = CreateMarket("Visible market", NightMarketStatus.Active);
        var activeBooth = CreateBooth(market.Id, BoothStatus.Active);
        var inactiveBooth = CreateBooth(market.Id, BoothStatus.Inactive);
        market.Booths.Add(activeBooth);
        market.Booths.Add(inactiveBooth);

        var category = new FoodCategory
        {
            Id = Guid.NewGuid(),
            BoothId = activeBooth.Id,
            Name = "Street food",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        activeBooth.FoodCategories.Add(category);
        var food = new FoodItem
        {
            Id = Guid.NewGuid(),
            BoothId = activeBooth.Id,
            CategoryId = category.Id,
            Name = "Banh mi",
            Price = 30_000m,
            IsAvailable = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        food.FoodPrices.Add(new FoodPrice
        {
            Id = Guid.NewGuid(),
            FoodItemId = food.Id,
            Price = 25_000m,
            StartDate = DateTime.UtcNow.AddHours(-1),
            EndDate = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        category.FoodItems.Add(food);
        context.NightMarkets.Add(market);
        await context.SaveChangesAsync();

        var boothResult = await new BoothRepository(context)
            .GetCustomerByNightMarketPagedAsync(market.Id, 1, 10);
        var foodResult = await new FoodItemRepository(context)
            .GetCustomerByNightMarketPagedAsync(market.Id, DateTime.UtcNow, 1, 10);

        Assert.Equal(activeBooth.Id, Assert.Single(boothResult.Items).Id);
        Assert.Equal(25_000m, Assert.Single(foodResult.Items).Price);
    }

    [Fact]
    public async Task CustomerFoodQuery_UsesSameEffectivePriceForListAndDetail()
    {
        await using var context = CreateContext();
        var market = CreateMarket("Visible market", NightMarketStatus.Open);
        market.OpeningHours = new TimeOnly(18, 0);
        market.ClosingHours = new TimeOnly(23, 0);
        var booth = CreateBooth(market.Id, BoothStatus.Active);
        booth.OpenTime = new TimeOnly(20, 0);
        booth.CloseTime = new TimeOnly(2, 0);
        var category = new FoodCategory
        {
            Id = Guid.NewGuid(), BoothId = booth.Id, Name = "Drinks",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var food = new FoodItem
        {
            Id = Guid.NewGuid(), BoothId = booth.Id, CategoryId = category.Id,
            Name = "Special tea", Price = 20_000m, IsAvailable = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        food.FoodPrices.Add(new FoodPrice
        {
            Id = Guid.NewGuid(), FoodItemId = food.Id, Price = 15_000m,
            StartDate = DateTime.UtcNow.AddHours(-1), EndDate = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        category.FoodItems.Add(food);
        booth.FoodCategories.Add(category);
        market.Booths.Add(booth);
        context.NightMarkets.Add(market);
        await context.SaveChangesAsync();

        var result = await new FoodItemRepository(context).GetCustomerPagedAsync(
            market.Id, booth.Id, null, " special ", 15_000m, 15_000m, false,
            DateTime.UtcNow, new TimeOnly(21, 0), 1, 10, "priceAsc");
        var orderableDuringIntersection = await new FoodItemRepository(context).GetCustomerPagedAsync(
            market.Id, booth.Id, null, null, null, null, true,
            DateTime.UtcNow, new TimeOnly(21, 0), 1, 10, "featured");
        var orderableAfterMarketClose = await new FoodItemRepository(context).GetCustomerPagedAsync(
            market.Id, booth.Id, null, null, null, null, true,
            DateTime.UtcNow, new TimeOnly(1, 0), 1, 10, "featured");
        var item = Assert.Single(result.Items);
        var detail = await new FoodItemRepository(context).GetCustomerByIdAsync(food.Id, DateTime.UtcNow);

        Assert.True(item.IsAvailable);
        Assert.Equal(15_000m, item.EffectivePrice);
        Assert.Equal(item.EffectivePrice, Assert.IsType<CustomerFoodReadModel>(detail).EffectivePrice);
        Assert.True(item.IsAvailable && CustomerAvailability.IsOpenNow(item, new TimeOnly(21, 0)));
        Assert.Single(orderableDuringIntersection.Items);
        Assert.Empty(orderableAfterMarketClose.Items);
    }

    [Fact]
    public async Task CustomerDirectQueries_BlockHiddenParents()
    {
        await using var context = CreateContext();
        var market = CreateMarket("Suspended market", NightMarketStatus.Open);
        market.ModerationStatus = ModerationStatus.Suspended;
        var booth = CreateBooth(market.Id, BoothStatus.Active);
        var category = new FoodCategory
        {
            Id = Guid.NewGuid(), BoothId = booth.Id, Name = "Hidden menu",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var food = new FoodItem
        {
            Id = Guid.NewGuid(), BoothId = booth.Id, CategoryId = category.Id,
            Name = "Hidden food", Price = 10_000m, IsAvailable = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        category.FoodItems.Add(food);
        booth.FoodCategories.Add(category);
        market.Booths.Add(booth);
        context.NightMarkets.Add(market);
        await context.SaveChangesAsync();

        Assert.Null(await new BoothRepository(context).GetCustomerByIdAsync(booth.Id));
        Assert.Null(await new FoodItemRepository(context).GetCustomerByIdAsync(food.Id, DateTime.UtcNow));

        var visibleMarket = CreateMarket("Visible market", NightMarketStatus.Open);
        var visibleBooth = CreateBooth(visibleMarket.Id, BoothStatus.Active);
        var visibleCategory = new FoodCategory
        {
            Id = Guid.NewGuid(), BoothId = visibleBooth.Id, Name = "Visible category",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var hiddenFood = new FoodItem
        {
            Id = Guid.NewGuid(), BoothId = visibleBooth.Id, CategoryId = visibleCategory.Id,
            Name = "Temporarily hidden food", Price = 10_000m, IsAvailable = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var visibleFood = new FoodItem
        {
            Id = Guid.NewGuid(), BoothId = visibleBooth.Id, CategoryId = visibleCategory.Id,
            Name = "Visible food", Price = 12_000m, IsAvailable = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        visibleCategory.FoodItems.Add(hiddenFood);
        visibleCategory.FoodItems.Add(visibleFood);
        visibleBooth.FoodCategories.Add(visibleCategory);
        visibleMarket.Booths.Add(visibleBooth);

        var inactiveBooth = CreateBooth(visibleMarket.Id, BoothStatus.Inactive);
        var inactiveCategory = new FoodCategory
        {
            Id = Guid.NewGuid(), BoothId = inactiveBooth.Id, Name = "Inactive booth category",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var inactiveBoothFood = new FoodItem
        {
            Id = Guid.NewGuid(), BoothId = inactiveBooth.Id, CategoryId = inactiveCategory.Id,
            Name = "Food under inactive booth", Price = 10_000m, IsAvailable = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        inactiveCategory.FoodItems.Add(inactiveBoothFood);
        inactiveBooth.FoodCategories.Add(inactiveCategory);
        visibleMarket.Booths.Add(inactiveBooth);
        context.NightMarkets.Add(visibleMarket);
        await context.SaveChangesAsync();

        Assert.Null(await new FoodItemRepository(context).GetCustomerByIdAsync(hiddenFood.Id, DateTime.UtcNow));
        Assert.Null(await new BoothRepository(context).GetCustomerByIdAsync(inactiveBooth.Id));
        Assert.Null(await new FoodItemRepository(context).GetCustomerByIdAsync(inactiveBoothFood.Id, DateTime.UtcNow));

        visibleMarket.IsDeleted = true;
        await context.SaveChangesAsync();
        Assert.Null(await new BoothRepository(context).GetCustomerByIdAsync(visibleBooth.Id));
        Assert.Null(await new FoodItemRepository(context).GetCustomerByIdAsync(visibleFood.Id, DateTime.UtcNow));
    }

    [Fact]
    public void CustomerAvailability_SupportsInheritedAndOvernightSchedules()
    {
        Assert.True(CustomerAvailability.IsOpenNow(
            true, new TimeOnly(18, 0), new TimeOnly(23, 0), null, null, new TimeOnly(19, 0)));
        Assert.True(CustomerAvailability.IsOpenNow(
            true, new TimeOnly(18, 0), new TimeOnly(23, 0), new TimeOnly(20, 0), new TimeOnly(2, 0), new TimeOnly(21, 0)));
        Assert.False(CustomerAvailability.IsOpenNow(
            true, new TimeOnly(18, 0), new TimeOnly(23, 0), new TimeOnly(20, 0), new TimeOnly(2, 0), new TimeOnly(1, 0)));
        Assert.False(CustomerAvailability.IsOpenNow(
            true, new TimeOnly(18, 0), new TimeOnly(23, 0), new TimeOnly(20, 0), null, new TimeOnly(21, 0)));
        Assert.False(CustomerAvailability.IsOpenNow(
            false, new TimeOnly(18, 0), new TimeOnly(23, 0), null, null, new TimeOnly(19, 0)));
        Assert.False(CustomerAvailability.IsOpenNow(
            true, new TimeOnly(18, 0), new TimeOnly(18, 0), null, null, new TimeOnly(18, 0)));
    }

    private static SNMDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SNMDbContext(options);
    }

    private static NightMarket CreateMarket(string name, NightMarketStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Address = "Ho Chi Minh City",
            Status = status,
            ModerationStatus = ModerationStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Booth CreateBooth(Guid marketId, BoothStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            RegistrationId = Guid.NewGuid(),
            NightMarketId = marketId,
            BoothOwnerId = Guid.NewGuid(),
            BoothName = $"{status} booth",
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static NightMarketCustomerReadModel CreateReadModel(
        NightMarketStatus status,
        TimeOnly? opening,
        TimeOnly? closing)
        => new(
            Guid.NewGuid(), "Market", null, "Address", 10.7m, 106.7m,
            opening, closing, null, status, 0, false, null, null);
}

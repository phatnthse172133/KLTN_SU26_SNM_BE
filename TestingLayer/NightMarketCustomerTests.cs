using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.NightMarkets;
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

using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.MapNavigation;
using AutoMapper;
using DomainLayer.InterfaceCore.JWT;
using DomainLayer.Enums;
using InfrastructureLayer.Cores.Helppers;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Seeders;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class IntegrationDemoDataSeederTests
{
    [Fact]
    public void IsEnabled_RequiresExplicitOptIn()
    {
        Assert.False(IntegrationDemoDataSeeder.IsEnabled(Configuration()));
        Assert.False(IntegrationDemoDataSeeder.IsEnabled(Configuration(("SeedDemoData", "false"))));
        Assert.True(IntegrationDemoDataSeeder.IsEnabled(Configuration(("SeedDemoData", "true"))));
    }

    [Fact]
    public async Task SeedAsync_RunTwice_IsIdempotentAndBusinessConsistent()
    {
        await using var provider = Provider();

        await SystemFoodTaxonomySeeder.SeedAsync(provider);
        var first = await IntegrationDemoDataSeeder.SeedAsync(provider);
        var second = await IntegrationDemoDataSeeder.SeedAsync(provider);

        Assert.Equal(first, second);
        Assert.Equal(new DemoSeedReport(1, 5, 20, 12, 2, 1, 10, 12, 2, 5), second);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var market = await db.NightMarkets.SingleAsync(x => x.Id == IntegrationDemoDataSeeder.MarketId);
        Assert.Equal(NightMarketStatus.Active, market.Status);
        Assert.Equal(ModerationStatus.Active, market.ModerationStatus);
        Assert.InRange(market.Latitude!.Value, -90, 90);
        Assert.InRange(market.Longitude!.Value, -180, 180);
        Assert.EndsWith("/market-v2.jpg", market.ThumbnailUrl, StringComparison.Ordinal);
        Assert.Equal(2, await db.NightMarketImages.CountAsync(x => x.NightMarketId == market.Id));
        Assert.Contains(await db.NightMarketImages.Where(x => x.NightMarketId == market.Id).ToListAsync(),
            image => image.IsCover && image.ImageUrl.EndsWith("/market-v2.jpg", StringComparison.Ordinal));
        var foodImageUrls = await db.FoodItems.Where(x => x.Booth.NightMarketId == market.Id)
            .Select(x => x.ThumbnailUrl).ToListAsync();
        Assert.Equal(20, foodImageUrls.Distinct().Count());
        Assert.DoesNotContain(foodImageUrls, url => url is null || !url.EndsWith("-v2.jpg", StringComparison.Ordinal));
        var boothImageUrls = await db.Booths.Where(x => x.NightMarketId == market.Id)
            .Select(x => x.ThumbnailUrl).ToListAsync();
        Assert.Equal(5, boothImageUrls.Distinct().Count());
        Assert.All(boothImageUrls, url => Assert.EndsWith("-v2.jpg", url, StringComparison.Ordinal));

        var courseLinks = await db.FoodItemTags
            .Where(link => link.FoodItem.Booth.NightMarketId == market.Id && link.FoodTag.Code.StartsWith("COURSE_"))
            .Select(link => link.FoodTag.Code)
            .ToListAsync();
        var courseCoverage = courseLinks.GroupBy(code => code).ToDictionary(group => group.Key, group => group.Count());
        Assert.True(courseCoverage["COURSE_APPETIZER"] >= 2);
        Assert.True(courseCoverage["COURSE_MAIN_COURSE"] >= 2);
        Assert.True(courseCoverage["COURSE_DRINK"] >= 2);
        Assert.True(courseCoverage["COURSE_DESSERT"] >= 2);
        Assert.Equal(20, await db.FoodItemCourses.Select(link => link.FoodItemId).Distinct().CountAsync());
        Assert.True(await db.FoodItemCourses.AnyAsync(link => link.Course == FoodCourse.DRINK));
        Assert.True(await db.FoodItemCourses.AnyAsync(link => link.Course == FoodCourse.DESSERT));
        Assert.Equal(20, await db.FoodItemDiningPurposes.Select(link => link.FoodItemId).Distinct().CountAsync());

        var ratings = await db.Booths.Where(x => x.NightMarketId == market.Id)
            .OrderBy(x => x.BoothCode).Select(x => x.AverageRating).ToListAsync();
        Assert.Equal(new decimal?[] { 4.8m, 4.5m, 4m, 3.5m, 0m }, ratings);
        Assert.All(await db.Reviews.ToListAsync(), review =>
        {
            var order = db.Orders.Single(x => x.Id == review.OrderId);
            var detail = db.OrderDetails.Single(x => x.OrderId == order.Id);
            Assert.Equal(OrderStatus.Completed, order.Status);
            Assert.Equal(review.CustomerId, order.CustomerId);
            Assert.Equal(review.BoothId, db.FoodItems.Single(x => x.Id == detail.FoodItemId).BoothId);
            Assert.Contains(db.Payments, payment => payment.OrderId == order.Id && payment.Status == PaymentStatus.Paid);
        });
        Assert.Single(await db.ReviewReplies.ToListAsync());

        var nodes = await db.LayoutNodes.Where(x => x.LayoutId == IntegrationDemoDataSeeder.LayoutId).ToDictionaryAsync(x => x.Id);
        Assert.All(await db.LayoutEdges.Where(x => x.LayoutId == IntegrationDemoDataSeeder.LayoutId).ToListAsync(), edge =>
        {
            Assert.True(edge.Distance > 0);
            Assert.NotEqual(edge.FromNodeId, edge.ToNodeId);
            Assert.True(nodes.ContainsKey(edge.FromNodeId));
            Assert.True(nodes.ContainsKey(edge.ToNodeId));
        });
        Assert.All(await db.BoothLocations.ToListAsync(), location =>
        {
            Assert.Equal(IntegrationDemoDataSeeder.LayoutId, location.LayoutId);
            Assert.True(nodes.ContainsKey(location.LayoutNodeId));
            Assert.Equal(market.Id, db.Booths.Single(x => x.Id == location.BoothId).NightMarketId);
        });
    }

    [Fact]
    public async Task SeededDataset_SatisfiesCustomerDiscoveryAndShortestRouteContracts()
    {
        await using var provider = Provider();
        await IntegrationDemoDataSeeder.SeedAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var markets = new NightMarketRepository(db);
        var booths = new BoothRepository(db);
        var foods = new FoodItemRepository(db);
        var discovery = new CustomerDiscoveryService(booths, foods, markets, new FixedTimeProvider());

        var list = await discovery.GetBoothsAsync(new CustomerBoothQueryRequest
        {
            MarketId = IntegrationDemoDataSeeder.MarketId, Page = 1, PageSize = 20, Sort = "rating"
        });
        Assert.Equal(5, list.Data!.Total);
        Assert.Equal(new[] { 4.8m, 4.5m, 4m, 3.5m, 0m }, list.Data.Items.Select(x => x.AverageRating));

        var firstPage = await discovery.GetBoothsAsync(new CustomerBoothQueryRequest
        {
            MarketId = IntegrationDemoDataSeeder.MarketId, Page = 1, PageSize = 2, Sort = "rating"
        });
        var secondPage = await discovery.GetBoothsAsync(new CustomerBoothQueryRequest
        {
            MarketId = IntegrationDemoDataSeeder.MarketId, Page = 2, PageSize = 2, Sort = "rating"
        });
        Assert.Equal(3, firstPage.Data!.TotalPages);
        Assert.Equal(2, firstPage.Data.Items.Count);
        Assert.Equal(2, secondPage.Data!.Items.Count);
        Assert.Empty(firstPage.Data.Items.Select(x => x.Id).Intersect(secondPage.Data.Items.Select(x => x.Id)));

        var search = await discovery.GetBoothsAsync(new CustomerBoothQueryRequest
        {
            MarketId = IntegrationDemoDataSeeder.MarketId, Search = "Nhà Mây", Page = 1, PageSize = 20
        });
        Assert.Single(search.Data!.Items);

        var noMatch = await discovery.GetBoothsAsync(new CustomerBoothQueryRequest
        {
            MarketId = IntegrationDemoDataSeeder.MarketId, Search = "không-tồn-tại", Page = 1, PageSize = 20
        });
        Assert.Empty(noMatch.Data!.Items);

        var highRated = await discovery.GetBoothsAsync(new CustomerBoothQueryRequest
        {
            MarketId = IntegrationDemoDataSeeder.MarketId, MinimumRating = 4.5m, Page = 1, PageSize = 20
        });
        Assert.Equal(2, highRated.Data!.Total);

        var open = await discovery.GetBoothsAsync(new CustomerBoothQueryRequest
        {
            MarketId = IntegrationDemoDataSeeder.MarketId, OpenNow = true, Page = 1, PageSize = 20
        });
        Assert.Equal(4, open.Data!.Total);
        Assert.All(open.Data.Items, item => Assert.True(item.IsOpenNow));

        var boothDetail = await discovery.GetBoothAsync(IntegrationDemoDataSeeder.BoothId(1));
        Assert.NotNull(boothDetail.Data!.Location);
        Assert.Equal(IntegrationDemoDataSeeder.LayoutId, boothDetail.Data.Location.LayoutId);
        Assert.Equal(4, boothDetail.Data.FoodCount);
        Assert.Equal(5, boothDetail.Data.ReviewCount);

        var menu = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest
        {
            BoothId = IntegrationDemoDataSeeder.BoothId(1), Page = 1, PageSize = 20
        });
        Assert.Equal(4, menu.Data!.Total);
        Assert.Contains(menu.Data.Items, food => food.EffectivePrice < food.BasePrice);

        var mapper = new MapperConfiguration(
            config => config.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        var navigation = new MapNavigationService(
            markets,
            new MarketLayoutRepository(db),
            new ZoneRepository(db),
            new LayoutNodeRepository(db),
            new LayoutEdgeRepository(db),
            new BoothLocationRepository(db),
            booths,
            mapper);

        var map = await navigation.GetMapAsync(IntegrationDemoDataSeeder.MarketId);
        Assert.Equal(IntegrationDemoDataSeeder.MarketId, map.Data!.MarketId);
        Assert.Equal(IntegrationDemoDataSeeder.LayoutId, map.Data.Layout.Id);
        Assert.Equal(1, map.Data.Layout.Version);
        Assert.Equal(5, map.Data!.Booths.Count);
        Assert.Equal(2, map.Data.StartingPoints.Count);
        Assert.NotEmpty(map.Data.Nodes);
        Assert.NotEmpty(map.Data.Edges);
        Assert.All(map.Data.Nodes, node => Assert.Equal(map.Data.Layout.Id, node.LayoutId));
        Assert.All(map.Data.Edges, edge => Assert.Equal(map.Data.Layout.Id, edge.LayoutId));
        var publicNodeIds = map.Data.Nodes.Select(node => node.Id).ToHashSet();
        Assert.All(map.Data.Edges, edge =>
        {
            Assert.Contains(edge.FromNodeId, publicNodeIds);
            Assert.Contains(edge.ToNodeId, publicNodeIds);
        });
        Assert.All(map.Data.Booths, booth => Assert.Contains(booth.NodeId, publicNodeIds));

        var mapJson = JsonSerializer.Serialize(map.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"nodes\"", mapJson);
        Assert.Contains("\"edges\"", mapJson);
        Assert.DoesNotContain("isDeleted", mapJson, StringComparison.OrdinalIgnoreCase);

        var boothOneRoute = await navigation.FindRouteToBoothAsync(
            IntegrationDemoDataSeeder.LayoutId,
            IntegrationDemoDataSeeder.MainEntranceNodeId,
            IntegrationDemoDataSeeder.BoothId(1));
        Assert.Equal(24m, boothOneRoute.Data!.TotalDistanceMeters);
        Assert.True(boothOneRoute.Data.IsDistanceCalibrated);

        var boothTwoRoute = await navigation.FindRouteToBoothAsync(
            IntegrationDemoDataSeeder.LayoutId,
            IntegrationDemoDataSeeder.MainEntranceNodeId,
            IntegrationDemoDataSeeder.BoothId(2));
        Assert.Equal(44m, boothTwoRoute.Data!.TotalDistanceMeters);
        Assert.Equal(
            new[] { IntegrationDemoDataSeeder.NodeId(1), IntegrationDemoDataSeeder.NodeId(2), IntegrationDemoDataSeeder.NodeId(3), IntegrationDemoDataSeeder.NodeId(4) },
            boothTwoRoute.Data.Path.Select(x => x.NodeId));
    }

    [Fact]
    public async Task SeededDataset_FoodDiscoveryFiltersSortsPagingAndDetail_AreBusinessConsistent()
    {
        await using var provider = Provider();
        await IntegrationDemoDataSeeder.SeedAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var discovery = new CustomerDiscoveryService(
            new BoothRepository(db),
            new FoodItemRepository(db),
            new NightMarketRepository(db),
            new FixedTimeProvider());

        var all = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest
        {
            Page = 1, PageSize = 100, Sort = "featured"
        });
        Assert.Equal(20, await db.FoodItems.CountAsync());
        Assert.Equal(19, all.Data!.Total);
        Assert.Equal(19, all.Data.Items.Select(x => x.Id).Distinct().Count());
        Assert.All(all.Data.Items, food => Assert.True(food.IsAvailable));
        Assert.All(all.Data.Items, food => Assert.Equal(IntegrationDemoDataSeeder.MarketId, food.MarketId));

        var market = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest
        {
            MarketId = IntegrationDemoDataSeeder.MarketId, Page = 1, PageSize = 100
        });
        Assert.Equal(all.Data.Total, market.Data!.Total);

        var selected = all.Data.Items.First();
        var search = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest
        {
            Search = selected.Name, Page = 1, PageSize = 20
        });
        Assert.Contains(search.Data!.Items, food => food.Id == selected.Id);
        var noMatch = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest
        {
            Search = "mon-khong-ton-tai-05", Page = 1, PageSize = 20
        });
        Assert.Empty(noMatch.Data!.Items);

        foreach (var categoryId in all.Data.Items.Select(x => x.CategoryId).Distinct().Take(2))
        {
            var category = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest
            {
                CategoryId = categoryId, Page = 1, PageSize = 100
            });
            Assert.NotEmpty(category.Data!.Items);
            Assert.All(category.Data.Items, food => Assert.Equal(categoryId, food.CategoryId));
        }

        var orderedPrices = all.Data.Items.Select(x => x.EffectivePrice).OrderBy(x => x).ToArray();
        var lowerBound = orderedPrices[orderedPrices.Length / 3];
        var upperBound = orderedPrices[orderedPrices.Length * 2 / 3];
        var ranged = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest
        {
            MinPrice = lowerBound, MaxPrice = upperBound, Page = 1, PageSize = 100
        });
        Assert.NotEmpty(ranged.Data!.Items);
        Assert.All(ranged.Data.Items, food => Assert.InRange(food.EffectivePrice, lowerBound, upperBound));

        var invalidRange = await Assert.ThrowsAsync<AppException>(() => discovery.GetFoodsAsync(
            new CustomerFoodQueryRequest { MinPrice = 100_000, MaxPrice = 50_000 }));
        Assert.Equal(400, invalidRange.StatusCode);
        Assert.Equal("INVALID_PRICE_RANGE", invalidRange.ErrorCode);

        var available = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest
        {
            AvailableOnly = true, Page = 1, PageSize = 100
        });
        Assert.NotEmpty(available.Data!.Items);
        Assert.All(available.Data.Items, food =>
        {
            Assert.True(food.IsAvailable);
            Assert.True(food.CanOrder);
        });

        var byName = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest
        {
            Sort = "name", Page = 1, PageSize = 100
        });
        Assert.Equal(byName.Data!.Items.OrderBy(x => x.Name).Select(x => x.Id), byName.Data.Items.Select(x => x.Id));
        var priceAsc = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest
        {
            Sort = "priceAsc", Page = 1, PageSize = 100
        });
        Assert.Equal(priceAsc.Data!.Items.OrderBy(x => x.EffectivePrice).ThenBy(x => x.Name).Select(x => x.Id), priceAsc.Data.Items.Select(x => x.Id));
        var priceDesc = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest
        {
            Sort = "priceDesc", Page = 1, PageSize = 100
        });
        Assert.Equal(priceDesc.Data!.Items.OrderByDescending(x => x.EffectivePrice).ThenBy(x => x.Name).Select(x => x.Id), priceDesc.Data.Items.Select(x => x.Id));

        var firstPage = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest { Page = 1, PageSize = 3 });
        var secondPage = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest { Page = 2, PageSize = 3 });
        Assert.Equal(7, firstPage.Data!.TotalPages);
        Assert.Empty(firstPage.Data.Items.Select(x => x.Id).Intersect(secondPage.Data!.Items.Select(x => x.Id)));

        var normal = all.Data.Items.First(x => x.BasePrice == x.EffectivePrice);
        var reduced = all.Data.Items.First(x => x.EffectivePrice < x.BasePrice);
        var nonOrderable = all.Data.Items.First(x => !x.CanOrder);
        var detailTag = new DomainLayer.Entities.FoodTag
        {
            Id = Guid.NewGuid(), Code = "detail-tag", Name = "Detail tag",
            TagGroup = FoodTagGroup.Dietary, Status = FoodTagStatus.Active,
            IsSystem = true, IsSelectable = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.FoodTags.Add(detailTag);
        db.FoodItemTags.Add(new DomainLayer.Entities.FoodItemTag
        {
            FoodItemId = normal.Id, FoodTagId = detailTag.Id, CreatedAt = DateTime.UtcNow
        });
        var normalizedDietary = new DomainLayer.Entities.DietaryAttribute
        {
            Id = Guid.NewGuid(), Code = "DIET_DETAIL", Name = "Normalized detail diet",
            IsSystem = true, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.DietaryAttributes.Add(normalizedDietary);
        db.FoodItemDietaryAttributes.Add(new DomainLayer.Entities.FoodItemDietaryAttribute
        {
            FoodItemId = normal.Id, DietaryAttributeId = normalizedDietary.Id,
            SuitabilityStatus = DomainLayer.Enums.DietarySuitabilityStatus.UNVERIFIED,
            Source = DomainLayer.Enums.MetadataSource.OWNER_DECLARED,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var normalDetail = (await discovery.GetFoodAsync(normal.Id)).Data!;
        Assert.Equal(normal.Id, normalDetail.Id);
        var returnedTag = Assert.Single(normalDetail.Tags);
        Assert.Equal(normalizedDietary.Id, returnedTag.Id);
        Assert.NotEqual(detailTag.Id, returnedTag.Id);
        Assert.Equal("Dietary", returnedTag.TagGroup);
        Assert.True((await discovery.GetFoodAsync(reduced.Id)).Data!.EffectivePrice < reduced.BasePrice);
        var nonOrderableDetail = (await discovery.GetFoodAsync(nonOrderable.Id)).Data!;
        Assert.True(nonOrderableDetail.IsAvailable);
        Assert.False(nonOrderableDetail.CanOrder);
        Assert.Equal(nonOrderable.BoothId, nonOrderableDetail.Booth.Id);
        Assert.Equal(nonOrderable.MarketId, nonOrderableDetail.Market.Id);

        var hiddenFoodId = await db.FoodItems.Where(x => !x.IsAvailable).Select(x => x.Id).SingleAsync();
        var hiddenError = await Assert.ThrowsAsync<AppException>(() => discovery.GetFoodAsync(hiddenFoodId));
        Assert.Equal(404, hiddenError.StatusCode);
        Assert.DoesNotContain(all.Data.Items, food => food.Id == hiddenFoodId);
    }

    [Fact]
    public async Task SeededDataset_DisconnectedDestination_ReturnsStableRouteNotFoundError()
    {
        await using var provider = Provider();
        await IntegrationDemoDataSeeder.SeedAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var destinationNodeId = IntegrationDemoDataSeeder.NodeId(4);
        var destinationEdges = await db.LayoutEdges
            .Where(x => x.LayoutId == IntegrationDemoDataSeeder.LayoutId
                && (x.FromNodeId == destinationNodeId || x.ToNodeId == destinationNodeId))
            .ToListAsync();
        db.LayoutEdges.RemoveRange(destinationEdges);
        await db.SaveChangesAsync();

        var mapper = new MapperConfiguration(
            config => config.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        var navigation = new MapNavigationService(
            new NightMarketRepository(db),
            new MarketLayoutRepository(db),
            new ZoneRepository(db),
            new LayoutNodeRepository(db),
            new LayoutEdgeRepository(db),
            new BoothLocationRepository(db),
            new BoothRepository(db),
            mapper);

        var error = await Assert.ThrowsAsync<AppException>(() => navigation.FindRouteToBoothAsync(
            IntegrationDemoDataSeeder.LayoutId,
            IntegrationDemoDataSeeder.MainEntranceNodeId,
            IntegrationDemoDataSeeder.BoothId(2)));

        Assert.Equal(404, error.StatusCode);
        Assert.Equal("ROUTE_NOT_FOUND", error.ErrorCode);
    }

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddLogging();
        services.AddDbContext<SNMDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<IConfiguration>(Configuration(("SeedDemoData", "true"), ("SeedDemoDataPassword", "ControlledDemo!2026")));
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        return services.BuildServiceProvider();
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(
            values.ToDictionary(x => x.Key, x => (string?)x.Value)).Build();

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
    }
}

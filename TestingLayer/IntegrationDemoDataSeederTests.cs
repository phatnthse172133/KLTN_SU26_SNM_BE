using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.MapNavigation;
using AutoMapper;
using DomainLayer.InterfaceCore.JWT;
using InfrastructureLayer.Cores.Helppers;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Seeders;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
        Assert.Equal(2, await db.NightMarketImages.CountAsync(x => x.NightMarketId == market.Id));

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
        Assert.Equal(5, map.Data!.Booths.Count);
        Assert.Equal(2, map.Data.StartingPoints.Count);

        var boothOneRoute = await navigation.FindRouteToBoothAsync(
            IntegrationDemoDataSeeder.LayoutId,
            IntegrationDemoDataSeeder.MainEntranceNodeId,
            IntegrationDemoDataSeeder.BoothId(1));
        Assert.Equal(32m, boothOneRoute.Data!.TotalDistance);

        var boothTwoRoute = await navigation.FindRouteToBoothAsync(
            IntegrationDemoDataSeeder.LayoutId,
            IntegrationDemoDataSeeder.MainEntranceNodeId,
            IntegrationDemoDataSeeder.BoothId(2));
        Assert.Equal(54m, boothTwoRoute.Data!.TotalDistance);
        Assert.Equal(
            new[] { IntegrationDemoDataSeeder.NodeId(1), IntegrationDemoDataSeeder.NodeId(2), IntegrationDemoDataSeeder.NodeId(3), IntegrationDemoDataSeeder.NodeId(4) },
            boothTwoRoute.Data.Path.Select(x => x.NodeId));
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

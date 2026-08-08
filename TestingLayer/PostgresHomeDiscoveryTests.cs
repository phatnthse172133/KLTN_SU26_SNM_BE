using ApplicationLayer.AI;
using ApplicationLayer.AI.Services;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.CustomerDiscovery;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Seeders;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class PostgresHomeDiscoveryTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task AppliedSeededDatabase_Phase05FoodDiscoveryContracts_AreVerified()
    {
        var connectionString = Environment.GetEnvironmentVariable("SNM_APPLIED_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<SNMDbContext>().UseNpgsql(connectionString).Options;
        await using var context = new SNMDbContext(options);
        Assert.Equal(20, await context.FoodItems.CountAsync(x => x.Booth.NightMarketId == IntegrationDemoDataSeeder.MarketId));
        var discovery = new CustomerDiscoveryService(new BoothRepository(context), new FoodItemRepository(context),
            new NightMarketRepository(context), new Phase05TimeProvider());

        var all = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest
        {
            MarketId = IntegrationDemoDataSeeder.MarketId, Page = 1, PageSize = 100
        });
        Assert.Equal(19, all.Data!.Total);
        Assert.All(all.Data.Items, x => Assert.Equal(IntegrationDemoDataSeeder.MarketId, x.MarketId));

        var first = all.Data.Items.First();
        var booth = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest { BoothId = first.BoothId, Page = 1, PageSize = 100 });
        Assert.NotEmpty(booth.Data!.Items);
        Assert.All(booth.Data.Items, x => Assert.Equal(first.BoothId, x.BoothId));
        var search = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest { Search = first.Name, Page = 1, PageSize = 20 });
        Assert.Contains(search.Data!.Items, x => x.Id == first.Id);
        Assert.Empty((await discovery.GetFoodsAsync(new CustomerFoodQueryRequest
            { Search = "phase05-khong-ton-tai", Page = 1, PageSize = 20 })).Data!.Items);

        foreach (var categoryId in all.Data.Items.Select(x => x.CategoryId).Distinct().Take(2))
        {
            var category = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest { CategoryId = categoryId, Page = 1, PageSize = 100 });
            Assert.All(category.Data!.Items, x => Assert.Equal(categoryId, x.CategoryId));
        }

        var prices = all.Data.Items.Select(x => x.EffectivePrice).OrderBy(x => x).ToArray();
        var min = prices[prices.Length / 3];
        var max = prices[prices.Length * 2 / 3];
        var ranged = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest { MinPrice = min, MaxPrice = max, Page = 1, PageSize = 100 });
        Assert.All(ranged.Data!.Items, x => Assert.InRange(x.EffectivePrice, min, max));
        var invalid = await Assert.ThrowsAsync<AppException>(() => discovery.GetFoodsAsync(
            new CustomerFoodQueryRequest { MinPrice = max, MaxPrice = min - 1 }));
        Assert.Equal("INVALID_PRICE_RANGE", invalid.ErrorCode);

        var available = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest { AvailableOnly = true, Page = 1, PageSize = 100 });
        Assert.All(available.Data!.Items, x => Assert.True(x.IsAvailable && x.CanOrder));
        var asc = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest { Sort = "priceAsc", Page = 1, PageSize = 100 });
        Assert.Equal(asc.Data!.Items.OrderBy(x => x.EffectivePrice).ThenBy(x => x.Name).Select(x => x.Id), asc.Data.Items.Select(x => x.Id));
        var desc = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest { Sort = "priceDesc", Page = 1, PageSize = 100 });
        Assert.Equal(desc.Data!.Items.OrderByDescending(x => x.EffectivePrice).ThenBy(x => x.Name).Select(x => x.Id), desc.Data.Items.Select(x => x.Id));

        var page1 = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest { MarketId = IntegrationDemoDataSeeder.MarketId, Page = 1, PageSize = 3 });
        var page2 = await discovery.GetFoodsAsync(new CustomerFoodQueryRequest { MarketId = IntegrationDemoDataSeeder.MarketId, Page = 2, PageSize = 3 });
        Assert.Equal(7, page1.Data!.TotalPages);
        Assert.Empty(page1.Data.Items.Select(x => x.Id).Intersect(page2.Data!.Items.Select(x => x.Id)));

        var normal = all.Data.Items.First(x => x.BasePrice == x.EffectivePrice);
        var reduced = all.Data.Items.First(x => x.EffectivePrice < x.BasePrice);
        Assert.Equal(normal.BasePrice, (await discovery.GetFoodAsync(normal.Id)).Data!.EffectivePrice);
        Assert.True((await discovery.GetFoodAsync(reduced.Id)).Data!.EffectivePrice < reduced.BasePrice);
        Assert.Contains(all.Data.Items, x => !x.CanOrder);
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task LatestMigrations_HomeDiscoveryQueries_FindOnlyTheDeterministicFixtureWhenFiltered()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection))
            return;

        var databaseName = $"snm_home_discovery_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            await create.ExecuteNonQueryAsync();

        var databaseBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName };
        try
        {
            var options = new DbContextOptionsBuilder<SNMDbContext>()
                .UseNpgsql(databaseBuilder.ConnectionString)
                .Options;

            await using var context = new SNMDbContext(options);
            await context.Database.MigrateAsync();
            await SeedLatestHomeFixtureAsync(databaseBuilder.ConnectionString);
            context.ChangeTracker.Clear();

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            Assert.Empty(pendingMigrations);

            await AssertHomeQueriesAsync(context, new HomeFixture(MarketId, BoothId, FoodId));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new NpgsqlConnection(adminBuilder.ConnectionString);
            await cleanup.OpenAsync();
            await using var drop = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", cleanup);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task AppliedDatabase_HomeDiscoveryQueries_DoNotThrow()
    {
        var connectionString = Environment.GetEnvironmentVariable("SNM_APPLIED_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new SNMDbContext(options);

        await AssertHomeQueriesAsync(context, null);
    }

    private static async Task AssertHomeQueriesAsync(SNMDbContext context, HomeFixture? fixture)
    {
        var marketFilter = fixture is null ? null : "pg-home-fixture";
        var marketId = fixture?.MarketId;
        var markets = await new NightMarketRepository(context).GetActivePagedAsync(
            marketFilter, NightMarketStatus.Active, 1, 100, "name", true);
        var foods = await new FoodItemRepository(context).GetCustomerPagedAsync(
            marketId, null, null, null, null, null, true,
            FixtureUtcNow, FixtureLocalTime, 1, 100, "featured");
        var foodsByName = await new FoodItemRepository(context).GetCustomerPagedAsync(
            marketId, null, null, null, null, null, true,
            FixtureUtcNow, FixtureLocalTime, 1, 100, "name");
        var foodsByPrice = await new FoodItemRepository(context).GetCustomerPagedAsync(
            marketId, null, null, null, 0, decimal.MaxValue, true,
            FixtureUtcNow, FixtureLocalTime, 1, 100, "priceAsc");
        var foodsByPriceDescending = await new FoodItemRepository(context).GetCustomerPagedAsync(
            marketId, null, null, null, null, null, true,
            FixtureUtcNow, FixtureLocalTime, 1, 100, "priceDesc");
        var aiOrderableFoods = await new FoodItemRepository(context).GetAiOrderableCandidatesAsync(
            marketId, FixtureLocalTime, 200);
        var booths = await new BoothRepository(context).GetCustomerPagedAsync(
            marketId, null, null, FixtureLocalTime, null, 1, 100, "featured");
        var foodTagRepository = new FoodTagRepository(context);
        var foodTags = await foodTagRepository.GetPagedTagsAsync(null, null, 1, 100);
        var aiHome = await CreateAiService(foodTagRepository).GetHomeAsync(null);

        Assert.NotNull(markets.Items);
        Assert.NotNull(foods.Items);
        Assert.NotNull(foodsByName.Items);
        Assert.NotNull(foodsByPrice.Items);
        Assert.NotNull(foodsByPriceDescending.Items);
        Assert.NotNull(aiOrderableFoods);
        Assert.NotNull(booths.Items);
        Assert.NotNull(foodTags.Items);
        Assert.NotNull(aiHome.Data);
        Assert.NotNull(aiHome.Data!.PopularTags);
        Assert.Equal(5, aiHome.Data.DiningStyles.Count);

        if (fixture is null)
            return;

        Assert.Equal(fixture.MarketId, Assert.Single(markets.Items).Id);
        Assert.Equal(fixture.FoodId, Assert.Single(foods.Items).Id);
        Assert.Equal(fixture.FoodId, Assert.Single(foodsByName.Items).Id);
        Assert.Equal(fixture.FoodId, Assert.Single(foodsByPrice.Items).Id);
        Assert.Equal(fixture.FoodId, Assert.Single(foodsByPriceDescending.Items).Id);
        Assert.Equal(fixture.FoodId, Assert.Single(aiOrderableFoods).Id);
        Assert.Equal(fixture.BoothId, Assert.Single(booths.Items).Id);
        Assert.Equal(1, markets.TotalCount);
        Assert.Equal(1, foods.TotalCount);
        Assert.Equal(1, booths.TotalCount);
    }

    private static async Task SeedLatestHomeFixtureAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SET session_replication_role = replica;
            INSERT INTO "User" ("Id","RoleId","UserName","PasswordHash","FullName","Email","AuthProvider","Status","CreatedAt","UpdatedAt") VALUES
            ('51000000-0000-0000-0000-000000000001','51000000-0000-0000-0000-000000000099','pg-home-owner','hash','PG Home Owner','pg-home-owner@test.local','Local','Active',now(),now());
            INSERT INTO "NightMarket" ("Id","Name","Address","OpeningHours","ClosingHours","Status","ModerationStatus","IsDeleted","TotalBooth","CreatedAt","UpdatedAt") VALUES
            ('52000000-0000-0000-0000-000000000001','pg-home-fixture-market','pg-home-fixture-address','00:00','23:59','Active','Active',false,1,now(),now());
            INSERT INTO "Booth" ("Id","NightMarketId","BoothOwnerId","BoothName","OpenTime","CloseTime","Status","CreatedAt","UpdatedAt") VALUES
            ('53000000-0000-0000-0000-000000000001','52000000-0000-0000-0000-000000000001','51000000-0000-0000-0000-000000000001','pg-home-fixture-booth','00:00','23:59','Active',now(),now());
            INSERT INTO "FoodCategories" ("Id","BoothId","Code","Name","IsSystem","IsActive","DisplayOrder","IsSelectable","IsDeleted","CreatedAt","UpdatedAt") VALUES
            ('54000000-0000-0000-0000-000000000001','53000000-0000-0000-0000-000000000001','PG_HOME_FIXTURE','PG home fixture',false,true,0,true,false,now(),now());
            INSERT INTO "FoodItem" ("Id","BoothId","CategoryId","Name","Description","Price","IsAvailable","IsFeatured","IsDeleted","SpiceLevel","CreatedAt","UpdatedAt") VALUES
            ('55000000-0000-0000-0000-000000000001','53000000-0000-0000-0000-000000000001','54000000-0000-0000-0000-000000000001','pg-home-fixture-food','Deterministic PostgreSQL home fixture',42000,true,true,false,'NON_SPICY',now(),now());
            SET session_replication_role = origin;
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static readonly Guid MarketId = Guid.Parse("52000000-0000-0000-0000-000000000001");
    private static readonly Guid BoothId = Guid.Parse("53000000-0000-0000-0000-000000000001");
    private static readonly Guid FoodId = Guid.Parse("55000000-0000-0000-0000-000000000001");
    private static readonly DateTime FixtureUtcNow = new(2026, 8, 4, 13, 0, 0, DateTimeKind.Utc);
    private static readonly TimeOnly FixtureLocalTime = new(20, 0);
    private sealed record HomeFixture(Guid MarketId, Guid BoothId, Guid FoodId);

    private static AIRecommendationService CreateAiService(FoodTagRepository foodTags)
        => new(
            new Mock<IFoodItemRepository>().Object,
            foodTags,
            new Mock<ICustomerPreferenceRepository>().Object,
            new Mock<IAIRecommendationLogRepository>().Object,
            new Mock<IAIProviderService>().Object,
            new Mock<IAICustomerContextRepository>().Object,
            Options.Create(new AIProviderSettings { EnableExternalProvider = false }),
            TimeProvider.System);

    private sealed class Phase05TimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
    }
}

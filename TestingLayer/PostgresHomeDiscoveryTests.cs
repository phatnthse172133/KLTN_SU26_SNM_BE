using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class PostgresHomeDiscoveryTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task LatestMigrations_HomeDiscoveryQueries_ReturnEmptyPagesOnPostgres()
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

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            Assert.Empty(pendingMigrations);

            await AssertHomeQueriesAsync(context, expectEmpty: true);
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

        await AssertHomeQueriesAsync(context, expectEmpty: false);
    }

    private static async Task AssertHomeQueriesAsync(SNMDbContext context, bool expectEmpty)
    {
        var markets = await new NightMarketRepository(context).GetActivePagedAsync(
            null, NightMarketStatus.Active, 1, 6, "name", true);
        var foods = await new FoodItemRepository(context).GetCustomerPagedAsync(
            null, null, null, null, null, null, true,
            DateTime.UtcNow, new TimeOnly(20, 0), 1, 6, "featured");
        var foodsByName = await new FoodItemRepository(context).GetCustomerPagedAsync(
            null, null, null, null, null, null, true,
            DateTime.UtcNow, new TimeOnly(20, 0), 1, 6, "name");
        var foodsByPrice = await new FoodItemRepository(context).GetCustomerPagedAsync(
            null, null, null, null, 0, decimal.MaxValue, true,
            DateTime.UtcNow, new TimeOnly(20, 0), 1, 6, "priceAsc");
        var foodsByPriceDescending = await new FoodItemRepository(context).GetCustomerPagedAsync(
            null, null, null, null, null, null, true,
            DateTime.UtcNow, new TimeOnly(20, 0), 1, 6, "priceDesc");
        var aiOrderableFoods = await new FoodItemRepository(context).GetAiOrderableCandidatesAsync(
            null, new TimeOnly(20, 0), 200);
        var booths = await new BoothRepository(context).GetCustomerPagedAsync(
            null, null, null, new TimeOnly(20, 0), null, 1, 6, "featured");

        Assert.NotNull(markets.Items);
        Assert.NotNull(foods.Items);
        Assert.NotNull(foodsByName.Items);
        Assert.NotNull(foodsByPrice.Items);
        Assert.NotNull(foodsByPriceDescending.Items);
        Assert.NotNull(aiOrderableFoods);
        Assert.NotNull(booths.Items);

        if (!expectEmpty)
            return;

        Assert.Empty(markets.Items);
        Assert.Empty(foods.Items);
        Assert.Empty(foodsByName.Items);
        Assert.Empty(foodsByPrice.Items);
        Assert.Empty(foodsByPriceDescending.Items);
        Assert.Empty(booths.Items);
        Assert.Equal(0, markets.TotalCount);
        Assert.Equal(0, foods.TotalCount);
        Assert.Equal(0, booths.TotalCount);
    }
}

using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TestingLayer;

public class PostgresConcurrencyTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task Migrations_UniqueCheckoutIndex_AndAdvisoryLock_WorkOnPostgres()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection))
        {
            // Kept opt-in so ordinary unit-test runs never touch a developer database.
            // CI/audit runs set SNM_TEST_POSTGRES and execute the real assertions below.
            return;
        }

        var databaseName = $"snm_concurrency_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            await create.ExecuteNonQueryAsync();

        var testBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName };
        try
        {
            var options = new DbContextOptionsBuilder<SNMDbContext>()
                .UseNpgsql(testBuilder.ConnectionString)
                .Options;
            await using (var migrationContext = new SNMDbContext(options))
                await migrationContext.Database.MigrateAsync();

            await AssertCheckoutIndexAsync(testBuilder.ConnectionString);
            await AssertCheckoutIndexSemanticsAsync(testBuilder.ConnectionString);
            await AssertAdvisoryLockSerializesAsync(options);
            await AssertPromotionRowLockSerializesAsync(testBuilder.ConnectionString, options);
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

    private static async Task AssertCheckoutIndexAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var definitionCommand = new NpgsqlCommand(
            "SELECT indexdef FROM pg_indexes WHERE indexname = 'ux_order_customer_checkout_request'", connection);
        var definition = (string?)await definitionCommand.ExecuteScalarAsync();
        Assert.NotNull(definition);
        Assert.Contains("UNIQUE", definition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CheckoutRequestId", definition, StringComparison.Ordinal);
        Assert.Contains("IS NOT NULL", definition, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertAdvisoryLockSerializesAsync(DbContextOptions<SNMDbContext> options)
    {
        await using var first = new SNMDbContext(options);
        await using var second = new SNMDbContext(options);
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        await first.Database.BeginTransactionAsync();
        await second.Database.BeginTransactionAsync();
        await new OrderRepository(first).AcquireCheckoutLockAsync(customerId, requestId);

        var waiting = new OrderRepository(second).AcquireCheckoutLockAsync(customerId, requestId);
        await Task.Delay(200);
        Assert.False(waiting.IsCompleted);

        await first.Database.CommitTransactionAsync();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        await second.Database.RollbackTransactionAsync();
    }

    private static async Task AssertCheckoutIndexSemanticsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var replica = new NpgsqlCommand("SET session_replication_role = replica", connection))
            await replica.ExecuteNonQueryAsync();

        var customer = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var request = Guid.NewGuid();
        async Task InsertAsync(long code, Guid? checkoutRequestId)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO "Order"
                    ("Id", "CustomerId", "BoothOwnerId", "BoothId", "OrderCode", "CheckoutRequestId",
                     "Status", "TotalAmount", "DiscountAmount", "FinalAmount", "CreatedAt", "UpdatedAt")
                VALUES
                    (@id, @customer, @owner, @booth, @code, @request,
                     'Placed', 0, 0, 0, now(), now())
                """, connection);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("customer", customer);
            command.Parameters.AddWithValue("owner", owner);
            command.Parameters.AddWithValue("booth", Guid.NewGuid());
            command.Parameters.AddWithValue("code", code);
            command.Parameters.AddWithValue("request", checkoutRequestId.HasValue
                ? checkoutRequestId.Value
                : DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }

        await InsertAsync(700_000_000_000_001, null);
        await InsertAsync(700_000_000_000_002, null);
        await InsertAsync(700_000_000_000_003, request);
        var duplicate = await Assert.ThrowsAsync<PostgresException>(
            () => InsertAsync(700_000_000_000_004, request));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
    }

    private static async Task AssertPromotionRowLockSerializesAsync(
        string connectionString,
        DbContextOptions<SNMDbContext> options)
    {
        var promotionId = Guid.NewGuid();
        await using (var seed = new NpgsqlConnection(connectionString))
        {
            await seed.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                SET session_replication_role = replica;
                INSERT INTO "Promotion"
                    ("Id", "BoothId", "Title", "DiscountType", "Scope", "DiscountValue",
                     "IsPublic", "StartDate", "EndDate", "Status", "IsDeleted", "CreatedAt", "UpdatedAt")
                VALUES
                    (@id, @booth, 'Lock probe', 'FixedAmount', 'EntireBoothOrder', 1,
                     false, now(), now() + interval '1 day', 'Active', false, now(), now());
                """, seed);
            command.Parameters.AddWithValue("id", promotionId);
            command.Parameters.AddWithValue("booth", Guid.NewGuid());
            await command.ExecuteNonQueryAsync();
        }

        await using var first = new SNMDbContext(options);
        await using var second = new SNMDbContext(options);
        await first.Database.BeginTransactionAsync();
        await second.Database.BeginTransactionAsync();
        await new PromotionRepository(first).AcquireReservationLockAsync(promotionId);

        var waiting = new PromotionRepository(second).AcquireReservationLockAsync(promotionId);
        await Task.Delay(200);
        Assert.False(waiting.IsCompleted);
        await first.Database.CommitTransactionAsync();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        await second.Database.RollbackTransactionAsync();
    }
}

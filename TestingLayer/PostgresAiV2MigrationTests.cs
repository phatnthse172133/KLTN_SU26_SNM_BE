using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace TestingLayer;

public sealed class PostgresAiV2MigrationTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task AdditiveMigration_WorksFromEmptyAndCurrentSchema_AndPreservesLegacyObjects()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;

        await WithDatabase(adminConnection, "empty", async (connectionString, options) =>
        {
            await using var db = new SNMDbContext(options);
            await db.Database.MigrateAsync();
            await AssertSchemaAsync(connectionString);
        });

        await WithDatabase(adminConnection, "current", async (connectionString, options) =>
        {
            await using (var db = new SNMDbContext(options))
                await db.GetService<IMigrator>().MigrateAsync("20260729121302_FixOrderPaymentConcurrencyTokens");
            await SeedLegacyProbeAsync(connectionString);
            await using (var db = new SNMDbContext(options))
                await db.Database.MigrateAsync();
            await AssertSchemaAsync(connectionString);
            await using var verify = new NpgsqlConnection(connectionString);
            await verify.OpenAsync();
            Assert.Equal(1L, await ScalarLong(verify, "SELECT count(*) FROM \"FoodTag\" WHERE \"Code\" = 'AUDIT_LEGACY'") );
            Assert.Equal(1L, await ScalarLong(verify, "SELECT count(*) FROM \"FoodItemTag\"") );
            Assert.Equal(1L, await ScalarLong(verify, "SELECT count(*) FROM \"CustomerPreference\"") );
            Assert.True(await RelationExists(verify, "AIRecommendationLog"));
        });
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task AiV2IndexesForeignKeysPrecisionAndConcurrency_WorkOnPostgres()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        await WithDatabase(adminConnection, "behavior", async (connectionString, options) =>
        {
            await using (var db = new SNMDbContext(options)) await db.Database.MigrateAsync();
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            Assert.Equal("12,2", await ScalarText(connection,
                "SELECT numeric_precision || ',' || numeric_scale FROM information_schema.columns WHERE table_name='AiMealPlan' AND column_name='TotalPrice'"));
            Assert.Contains("UNIQUE", await ScalarText(connection,
                "SELECT indexdef FROM pg_indexes WHERE indexname='ux_fooditemcourse_primary'"), StringComparison.OrdinalIgnoreCase);

            var foodId = Guid.NewGuid();
            await Execute(connection, "SET session_replication_role = replica");
            await Execute(connection, $"INSERT INTO \"FoodItemCourse\" (\"FoodItemId\", \"Course\", \"IsPrimary\", \"CreatedAt\") VALUES ('{foodId}', 'DRINK', true, now())");
            await Execute(connection, "SET session_replication_role = origin");
            var duplicate = await Assert.ThrowsAsync<PostgresException>(() => Execute(connection,
                $"INSERT INTO \"FoodItemCourse\" (\"FoodItemId\", \"Course\", \"IsPrimary\", \"CreatedAt\") VALUES ('{foodId}', 'DESSERT', true, now())"));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);

            var ingredientId = Guid.NewGuid();
            await Execute(connection, $"INSERT INTO \"Ingredient\" (\"Id\",\"Code\",\"Name\",\"NormalizedName\",\"IsSystem\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('{ingredientId}','ING_AUDIT','Audit','AUDIT',true,true,0,now(),now())");
            await Execute(connection, "SET session_replication_role = replica");
            await Execute(connection, $"INSERT INTO \"FoodItemIngredient\" (\"FoodItemId\",\"IngredientId\",\"IsPrimary\",\"IsOptional\",\"CreatedAt\") VALUES ('{foodId}','{ingredientId}',true,false,now())");
            await Execute(connection, "SET session_replication_role = origin");
            var restricted = await Assert.ThrowsAsync<PostgresException>(() => Execute(connection, $"DELETE FROM \"Ingredient\" WHERE \"Id\"='{ingredientId}'"));
            Assert.Equal(PostgresErrorCodes.RestrictViolation, restricted.SqlState);
            await Execute(connection, "SET session_replication_role = replica");
            var compositeDuplicate = await Assert.ThrowsAsync<PostgresException>(() => Execute(connection, $"INSERT INTO \"FoodItemIngredient\" (\"FoodItemId\",\"IngredientId\",\"IsPrimary\",\"IsOptional\",\"CreatedAt\") VALUES ('{foodId}','{ingredientId}',false,false,now())"));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, compositeDuplicate.SqlState);
            await Execute(connection, "SET session_replication_role = origin");

            await SeedPlanProbeAsync(connection);
            await using var first = new SNMDbContext(options);
            await using var second = new SNMDbContext(options);
            var firstPlan = await first.AiMealPlans.SingleAsync();
            var secondPlan = await second.AiMealPlans.SingleAsync();
            first.Entry(firstPlan).Property(value => value.Version).CurrentValue = 1;
            second.Entry(secondPlan).Property(value => value.Version).CurrentValue = 1;
            await first.SaveChangesAsync();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        });
    }

    private static async Task AssertSchemaAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var table in new[] { "FoodTag", "FoodItemTag", "CustomerPreference", "AIRecommendationLog", "Ingredient", "FoodItemCourse", "CustomerFoodProfile", "AiRecommendationSession", "AiMealPlanSession", "AiMealPlan", "AiMealPlanItem", "FoodAiProfile" })
            Assert.True(await RelationExists(connection, table), $"Missing table {table}");
        Assert.True(await ScalarLong(connection, "SELECT count(*) FROM information_schema.table_constraints WHERE constraint_type='FOREIGN KEY' AND table_name LIKE 'Ai%'") >= 8);
        Assert.Contains("UNIQUE", await ScalarText(connection,
            "SELECT indexdef FROM pg_indexes WHERE indexname='ux_aimealplansession_customer_idempotency'"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("jsonb", await ScalarText(connection,
            "SELECT data_type FROM information_schema.columns WHERE table_name='AiMealPlan' AND column_name='WarningsJson'"));
    }

    private static async Task SeedLegacyProbeAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await Execute(connection,
            """
            SET session_replication_role = replica;
            INSERT INTO "FoodTag" ("Id","Name","Code","TagGroup","Status","IsSystem","DisplayOrder","IsSelectable","IsPreferenceSelectable","IsAutoAssigned","IsDeleted","CreatedAt","UpdatedAt")
            VALUES ('10000000-0000-0000-0000-000000000001','Legacy probe','AUDIT_LEGACY','Other','Active',false,0,true,true,false,false,now(),now());
            INSERT INTO "FoodItemTag" ("FoodItemId","FoodTagId","CreatedAt") VALUES ('20000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001',now());
            INSERT INTO "CustomerPreference" ("Id","CustomerId","FoodTagId","PreferenceKind","PreferenceSource","CreatedAt","UpdatedAt") VALUES ('30000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','Like','UserSelected',now(),now());
            SET session_replication_role = origin;
            """);
    }

    private static async Task SeedPlanProbeAsync(NpgsqlConnection connection)
    {
        await Execute(connection,
            """
            SET session_replication_role = replica;
            INSERT INTO "AiMealPlanSession" ("Id","PartySize","Budget","DiningStyle","Status","CreatedAt","ExpiresAt") VALUES ('50000000-0000-0000-0000-000000000001',2,200000,'SHARED','COMPLETED',now(),now()+interval '1 hour');
            INSERT INTO "AiMealPlan" ("Id","SessionId","MarketId","PlanCode","PlanTitle","Strategy","TotalPrice","RemainingBudget","CompatibilityScore","IsComplete","Version","Status","CreatedAt","UpdatedAt") VALUES ('60000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000001','70000000-0000-0000-0000-000000000001','P1','Plan','BALANCED',0,200000,80,false,0,'DRAFT',now(),now());
            SET session_replication_role = origin;
            """);
    }

    private static async Task WithDatabase(string adminConnection, string suffix, Func<string, DbContextOptions<SNMDbContext>, Task> action)
    {
        var name = $"snm_aiv2_{suffix}_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        await Execute(admin, $"CREATE DATABASE \"{name}\"");
        var testBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = name };
        var options = new DbContextOptionsBuilder<SNMDbContext>().UseNpgsql(testBuilder.ConnectionString).Options;
        try { await action(testBuilder.ConnectionString, options); }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new NpgsqlConnection(adminBuilder.ConnectionString);
            await cleanup.OpenAsync();
            await Execute(cleanup, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    private static async Task<bool> RelationExists(NpgsqlConnection connection, string name)
        => (bool)(await new NpgsqlCommand("SELECT to_regclass(quote_ident(@name)) IS NOT NULL", connection) { Parameters = { new("name", name) } }.ExecuteScalarAsync())!;
    private static async Task<long> ScalarLong(NpgsqlConnection connection, string sql) => Convert.ToInt64(await new NpgsqlCommand(sql, connection).ExecuteScalarAsync());
    private static async Task<string> ScalarText(NpgsqlConnection connection, string sql) => Convert.ToString(await new NpgsqlCommand(sql, connection).ExecuteScalarAsync())!;
    private static async Task Execute(NpgsqlConnection connection, string sql) { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
}

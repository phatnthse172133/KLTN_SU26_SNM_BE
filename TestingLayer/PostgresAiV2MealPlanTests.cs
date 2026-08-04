using ApplicationLayer.AI.V2.Services;
using DomainLayer.Entities;
using DomainLayer.Enums;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TestingLayer;

public sealed class PostgresAiV2MealPlanTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task MealPlanPersistence_Idempotency_RowLockAndCleanup_WorkOnPostgres()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        var name = $"snm_aiv2_meal_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString); await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin).ExecuteNonQueryAsync();
        var testBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = name };
        var options = new DbContextOptionsBuilder<SNMDbContext>().UseNpgsql(testBuilder.ConnectionString).Options;
        try
        {
            await using (var migrate = new SNMDbContext(options)) await migrate.Database.MigrateAsync();
            await SeedParents(testBuilder.ConnectionString);
            var now = DateTime.UtcNow;
            await using (var createDb = new SNMDbContext(options))
            {
                var repository = new MealPlanV2Repository(createDb);
                var session = Session(now, "same-key", "HASH-A");
                Assert.Equal(MealPlanIdempotencyStatus.CREATED, (await repository.SaveCreateAsync(session, default)).Status);
                Assert.Equal(MealPlanIdempotencyStatus.EXISTING,
                    (await repository.SaveCreateAsync(Session(now, "same-key", "HASH-A"), default)).Status);
                Assert.Equal(MealPlanIdempotencyStatus.CONFLICT,
                    (await repository.SaveCreateAsync(Session(now, "same-key", "HASH-B"), default)).Status);
            }

            var planId = PlanId;
            await using var firstDb = new SNMDbContext(options);
            await using var secondDb = new SNMDbContext(options);
            var first = await new MealPlanV2Repository(firstDb).BeginOwnedMutationAsync(CustomerId, planId, default);
            Assert.NotNull(first); Assert.Equal(0, first!.Plan.Version);
            var waiting = new MealPlanV2Repository(secondDb).BeginOwnedMutationAsync(CustomerId, planId, default);
            await Task.Delay(100);
            Assert.False(waiting.IsCompleted);
            first.Plan.ApplyAuthoritativeCalculation(200_000, null, false, 0, "[]", now);
            await first.CommitAsync(default); await first.DisposeAsync();
            await using (var second = await waiting)
            {
                Assert.NotNull(second); Assert.Equal(1, second!.Plan.Version); Assert.Empty(second.Plan.Items);
            }


            await using (var addDb = new SNMDbContext(options))
            await using (var add = await new MealPlanV2Repository(addDb).BeginOwnedMutationAsync(CustomerId, planId, default))
            {
                Assert.NotNull(add);
                add!.Plan.AddItem(new AiMealPlanItem { Id = Guid.NewGuid(), FoodNameSnapshot = "Snapshot food",
                    BoothNameSnapshot = "Snapshot booth", Course = FoodCourse.MAIN_COURSE, Quantity = 1,
                    UnitPriceSnapshot = 50_000, ServingCountSnapshot = 2, CompatibilityScore = 80,
                    Reason = "deterministic", CreatedAt = now, UpdatedAt = now }, MarketId, 200_000, false);
                add.Plan.ApplyAuthoritativeCalculation(200_000, 2, true, 80, "[]", now);
                await add.CommitAsync(default);
            }

            await using (var removeDb = new SNMDbContext(options))
            await using (var remove = await new MealPlanV2Repository(removeDb).BeginOwnedMutationAsync(CustomerId, planId, default))
            {
                Assert.NotNull(remove); var item = Assert.Single(remove!.Plan.Items); item.MarkRemoved(now.AddMinutes(1));
                remove.Plan.ApplyAuthoritativeCalculation(200_000, null, false, 0, "[\"SERVING_DATA_INSUFFICIENT\"]", now.AddMinutes(1));
                await remove.CommitAsync(default);
            }
            await using (var verifyDb = new SNMDbContext(options))
            {
                var persisted = await verifyDb.AiMealPlans.Include(value => value.Items).SingleAsync();
                Assert.Equal(3, persisted.Version); Assert.Equal(0, persisted.TotalPrice); Assert.True(Assert.Single(persisted.Items).IsRemoved);
            }

            await using var cleanupDb = new SNMDbContext(options);
            var cleanup = new MealPlanV2Repository(cleanupDb);
            Assert.Equal(1, await cleanup.DeleteExpiredBatchAsync(now.AddDays(1), 10, default));
            Assert.False(await cleanupDb.AiMealPlanSessions.AnyAsync());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new NpgsqlConnection(adminBuilder.ConnectionString); await cleanup.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", cleanup).ExecuteNonQueryAsync();
        }
    }

    private static AiMealPlanSession Session(DateTime now, string key, string hash) => new()
    {
        Id = Guid.NewGuid(), CustomerId = CustomerId, PartySize = 2, Budget = 200_000, DiningStyle = "FULL_MEAL",
        IdempotencyKey = key, RequestHash = hash, Status = AiSessionStatus.COMPLETED, CreatedAt = now,
        ExpiresAt = now.AddHours(2), Plans = [new AiMealPlan { Id = PlanId, MarketId = MarketId, PlanCode = "A",
            PlanTitle = "Plan", Strategy = "BEST_MATCH", Status = AiMealPlanStatus.READY, CreatedAt = now, UpdatedAt = now }]
    };

    private static readonly Guid CustomerId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid MarketId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    private static readonly Guid PlanId = Guid.Parse("a3000000-0000-0000-0000-000000000001");

    private static async Task SeedParents(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await new NpgsqlCommand(
            """
            SET session_replication_role = replica;
            INSERT INTO "User" ("Id","RoleId","UserName","PasswordHash","FullName","Email","AuthProvider","Status","CreatedAt","UpdatedAt")
            VALUES ('a1000000-0000-0000-0000-000000000001','a4000000-0000-0000-0000-000000000001','meal-customer','hash','Customer','meal@test.local','Local','Active',now(),now());
            INSERT INTO "NightMarket" ("Id","Name","Address","Status","ModerationStatus","IsDeleted","TotalBooth","CreatedAt","UpdatedAt")
            VALUES ('a2000000-0000-0000-0000-000000000001','Meal market','Address','Active','Active',false,0,now(),now());
            SET session_replication_role = origin;
            """, connection).ExecuteNonQueryAsync();
    }
}

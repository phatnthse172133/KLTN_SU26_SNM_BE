using ApplicationLayer.AI.V2.Services;
using DomainLayer.Entities;
using DomainLayer.Enums;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TestingLayer;

public sealed class PostgresAiV2RecommendationTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task SessionResultsFeedbackAndRetention_WorkOnMigratedPostgres()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        var databaseName = $"snm_aiv2_recommendation_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        var testBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName };
        var dbOptions = new DbContextOptionsBuilder<SNMDbContext>().UseNpgsql(testBuilder.ConnectionString).Options;
        try
        {
            await using var db = new SNMDbContext(dbOptions); await db.Database.MigrateAsync();
            await SeedParents(testBuilder.ConnectionString);
            var now = DateTime.UtcNow;
            var candidate = Assert.Single(await new FoodRecommendationReadRepository(db).GetCandidatesAsync(now, 100, CancellationToken.None));
            Assert.Equal(FoodId, candidate.FoodId);
            Assert.Equal(80_000, candidate.CurrentPrice);
            Assert.Equal("ING_BEEF", Assert.Single(candidate.IngredientCodes));
            Assert.Equal(4.5m, candidate.Rating);
            Assert.Equal(2, candidate.ReviewCount);
            var repository = new AiRecommendationSessionRepository(db);
            var sessionId = Guid.NewGuid();
            await repository.SaveSessionAsync(new AiRecommendationSession
            {
                Id = sessionId, CustomerId = CustomerId, OriginalQuery = "bo nuong", Status = AiSessionStatus.COMPLETED,
                CreatedAt = now, ExpiresAt = now.AddMinutes(30), UsedFallback = true
            }, [new AiRecommendationResult
            {
                SessionId = sessionId, FoodItemId = FoodId, Rank = 1, Score = 81.25m,
                MatchTier = RecommendationMatchTier.STRONG_MATCH, CreatedAt = now
            }], CancellationToken.None);

            Assert.Equal(1, await db.AiRecommendationResults.CountAsync(value => value.SessionId == sessionId));
            Assert.Equal(RecommendationFeedbackRecordStatus.FOOD_NOT_IN_SESSION,
                (await repository.RecordFeedbackAsync(CustomerId, sessionId, Guid.NewGuid(), AiRecommendationFeedbackAction.LIKED, now, 30, CancellationToken.None)).Status);
            Assert.Equal(RecommendationFeedbackRecordStatus.NOT_FOUND,
                (await repository.RecordFeedbackAsync(Guid.NewGuid(), sessionId, FoodId, AiRecommendationFeedbackAction.LIKED, now, 30, CancellationToken.None)).Status);
            Assert.Equal(RecommendationFeedbackRecordStatus.RECORDED,
                (await repository.RecordFeedbackAsync(CustomerId, sessionId, FoodId, AiRecommendationFeedbackAction.LIKED, now, 30, CancellationToken.None)).Status);
            Assert.Equal(RecommendationFeedbackRecordStatus.RECORDED,
                (await repository.RecordFeedbackAsync(CustomerId, sessionId, FoodId, AiRecommendationFeedbackAction.DISLIKED, now.AddSeconds(1), 30, CancellationToken.None)).Status);
            Assert.Equal(AiRecommendationFeedbackAction.DISLIKED,
                (await db.AiRecommendationFeedback.SingleAsync(value => value.SessionId == sessionId)).Action);
            Assert.Equal(RecommendationFeedbackRecordStatus.EXPIRED,
                (await repository.RecordFeedbackAsync(CustomerId, sessionId, FoodId, AiRecommendationFeedbackAction.VIEWED,
                    now.AddMinutes(31), 30, CancellationToken.None)).Status);

            var concurrentSessionId = Guid.NewGuid();
            await repository.SaveSessionAsync(new AiRecommendationSession
            {
                Id = concurrentSessionId, CustomerId = CustomerId, OriginalQuery = "concurrent", Status = AiSessionStatus.COMPLETED,
                CreatedAt = now, ExpiresAt = now.AddMinutes(30)
            }, [new AiRecommendationResult
            {
                SessionId = concurrentSessionId, FoodItemId = FoodId, Rank = 1, Score = 75,
                MatchTier = RecommendationMatchTier.STRONG_MATCH, CreatedAt = now
            }], CancellationToken.None);
            await using var firstDb = new SNMDbContext(dbOptions);
            await using var secondDb = new SNMDbContext(dbOptions);
            var concurrent = await Task.WhenAll(
                new AiRecommendationSessionRepository(firstDb).RecordFeedbackAsync(CustomerId, concurrentSessionId, FoodId,
                    AiRecommendationFeedbackAction.LIKED, now, 30, CancellationToken.None),
                new AiRecommendationSessionRepository(secondDb).RecordFeedbackAsync(CustomerId, concurrentSessionId, FoodId,
                    AiRecommendationFeedbackAction.DISLIKED, now.AddSeconds(1), 30, CancellationToken.None));
            Assert.All(concurrent, value => Assert.Equal(RecommendationFeedbackRecordStatus.RECORDED, value.Status));
            db.ChangeTracker.Clear();
            var latest = await db.AiRecommendationFeedback.SingleAsync(value => value.SessionId == concurrentSessionId);
            Assert.Equal(AiRecommendationFeedbackAction.DISLIKED, latest.Action);

            var oldId = Guid.NewGuid();
            await repository.SaveSessionAsync(new AiRecommendationSession
            {
                Id = oldId, CustomerId = CustomerId, OriginalQuery = "old", Status = AiSessionStatus.COMPLETED,
                CreatedAt = now.AddDays(-40), ExpiresAt = now.AddDays(-39)
            }, [], CancellationToken.None);
            Assert.Equal(1, await repository.DeleteExpiredBatchAsync(now.AddDays(-30), 10, CancellationToken.None));
            Assert.False(await db.AiRecommendationSessions.AnyAsync(value => value.Id == oldId));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new NpgsqlConnection(adminBuilder.ConnectionString); await cleanup.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", cleanup); await drop.ExecuteNonQueryAsync();
        }
    }

    private static readonly Guid CustomerId = Guid.Parse("22222222-2222-2222-2222-222222222101");
    private static readonly Guid FoodId = Guid.Parse("99999999-9999-9999-9999-999999999101");

    private static async Task SeedParents(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SET session_replication_role = replica;
            INSERT INTO "User" ("Id","RoleId","UserName","PasswordHash","FullName","Email","AuthProvider","Status","CreatedAt","UpdatedAt") VALUES
            ('22222222-2222-2222-2222-222222222101','22222222-2222-2222-2222-222222222001','customer','hash','Customer','customer@recommendation.local','Local','Active',now(),now()),
            ('22222222-2222-2222-2222-222222222102','22222222-2222-2222-2222-222222222002','owner','hash','Owner','owner@recommendation.local','Local','Active',now(),now());
            INSERT INTO "NightMarket" ("Id","Name","Address","Status","ModerationStatus","IsDeleted","TotalBooth","CreatedAt","UpdatedAt") VALUES
            ('33333333-3333-3333-3333-333333333101','Recommendation market','Address','Active','Active',false,1,now(),now());
            INSERT INTO "Booth" ("Id","RegistrationId","NightMarketId","BoothOwnerId","BoothName","Status","CreatedAt","UpdatedAt") VALUES
            ('44444444-4444-4444-4444-444444444101','44444444-4444-4444-4444-444444444199','33333333-3333-3333-3333-333333333101','22222222-2222-2222-2222-222222222102','Recommendation booth','Active',now(),now());
            INSERT INTO "FoodCategories" ("Id","BoothId","Code","Name","IsSystem","IsActive","DisplayOrder","IsSelectable","IsDeleted","CreatedAt","UpdatedAt") VALUES
            ('88888888-8888-8888-8888-888888888101','44444444-4444-4444-4444-444444444101','MAIN_REC','Main',true,true,0,true,false,now(),now());
            INSERT INTO "FoodItem" ("Id","BoothId","CategoryId","Name","Price","IsAvailable","IsFeatured","IsDeleted","SpiceLevel","CreatedAt","UpdatedAt") VALUES
            ('99999999-9999-9999-9999-999999999101','44444444-4444-4444-4444-444444444101','88888888-8888-8888-8888-888888888101','Bò nướng',80000,true,false,false,'MILD',now(),now());
            INSERT INTO "Ingredient" ("Id","NormalizedName","Code","Name","IsSystem","IsActive","DisplayOrder","CreatedAt","UpdatedAt") VALUES
            ('77777777-7777-7777-7777-777777777101','BEEF','ING_BEEF','Thịt bò',true,true,0,now(),now());
            INSERT INTO "FoodItemIngredient" ("FoodItemId","IngredientId","IsPrimary","IsOptional","CreatedAt") VALUES
            ('99999999-9999-9999-9999-999999999101','77777777-7777-7777-7777-777777777101',true,false,now());
            INSERT INTO "Reviews" ("Id","BoothId","CustomerId","OrderId","Rating","Content","IsVisible","CreatedAt","UpdatedAt") VALUES
            ('66666666-6666-6666-6666-666666666101','44444444-4444-4444-4444-444444444101','22222222-2222-2222-2222-222222222101','55555555-5555-5555-5555-555555555101',5,'Great',true,now(),now()),
            ('66666666-6666-6666-6666-666666666102','44444444-4444-4444-4444-444444444101','22222222-2222-2222-2222-222222222101','55555555-5555-5555-5555-555555555102',4,'Good',true,now(),now());
            SET session_replication_role = origin;
            """, connection);
        await command.ExecuteNonQueryAsync();
    }
}

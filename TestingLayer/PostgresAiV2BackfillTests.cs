using DomainLayer.Enums;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Backfill;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;

namespace TestingLayer;

public sealed class PostgresAiV2BackfillTests
{
    private readonly ITestOutputHelper _output;

    public PostgresAiV2BackfillTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void SearchTextAndHash_AreDeterministic_AndSemanticChangesChangeHash()
    {
        var first = AiV2FoodMetadataBackfillService.BuildSearchText(new[] { "  Grilled  Chicken ", "SPICY", "grilled chicken" });
        var second = AiV2FoodMetadataBackfillService.BuildSearchText(new[] { "SPICY", "Grilled Chicken" });
        Assert.Equal(second, first);
        Assert.Equal(AiV2FoodMetadataBackfillService.ComputeContentHash(first), AiV2FoodMetadataBackfillService.ComputeContentHash(second));
        Assert.NotEqual(AiV2FoodMetadataBackfillService.ComputeContentHash(first), AiV2FoodMetadataBackfillService.ComputeContentHash(first + " | vegan"));
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task DryRunExecuteAndRerun_AreSafeReconciledAndIdempotentOnPostgres()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        var databaseName = $"snm_aiv2_backfill_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        var testBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName };

        try
        {
            await using var provider = Provider(testBuilder.ConnectionString);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
                await db.Database.MigrateAsync();
            }
            await SeedLegacyAiDataAsync(testBuilder.ConnectionString);

            int legacyFoodRelations;
            int legacyPreferences;
            AiV2BackfillReport dry;
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
                legacyFoodRelations = await db.FoodItemTags.CountAsync();
                legacyPreferences = await db.CustomerPreferences.CountAsync();
                dry = await new AiV2FoodMetadataBackfillService(db).RunAsync(new(AiV2BackfillMode.DryRun, 7));
                _output.WriteLine("DRY_RUN\n" + dry.ToJson());
                Assert.Equal(0, await db.Ingredients.CountAsync());
                Assert.Equal(0, await db.FoodAiProfiles.CountAsync());
                Assert.False(dry.TransactionCommitted);
                Assert.Equal(12, dry.SoupResolutions.Count);
                Assert.Equal(dry.FoodItemTagBefore + dry.CustomerPreferenceBefore, dry.CoveredLegacyRelations);
            }

            AiV2BackfillReport executed;
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
                executed = await new AiV2FoodMetadataBackfillService(db).RunAsync(new(AiV2BackfillMode.Execute, 7));
                _output.WriteLine("EXECUTE\n" + executed.ToJson());
                Assert.True(executed.TransactionCommitted);
                Assert.Equal(12, await db.FoodAiProfiles.CountAsync());
                Assert.Equal(legacyFoodRelations, await db.FoodItemTags.CountAsync());
                Assert.Equal(legacyPreferences, await db.CustomerPreferences.CountAsync());
                Assert.Equal(0, await db.FoodItemCourses.CountAsync(value => value.Course == FoodCourse.SOUP));
                Assert.True(await db.FoodItemCourses.AnyAsync(value => value.Course == FoodCourse.DRINK));
                Assert.True(await db.FoodItemCourses.AnyAsync(value => value.Course == FoodCourse.DESSERT));
                Assert.True(await db.CustomerPreferredCourses.AnyAsync(value => value.Course == FoodCourse.DRINK));
                Assert.True(await db.CustomerPreferredCourses.AnyAsync(value => value.Course == FoodCourse.DESSERT));
                Assert.Empty(await db.FoodItemAllergens.ToListAsync());
                Assert.All(await db.FoodItemDietaryAttributes.ToListAsync(), value =>
                {
                    Assert.False(value.IsConfirmed);
                    Assert.Equal(DietarySuitabilityStatus.UNVERIFIED, value.SuitabilityStatus);
                });
                Assert.DoesNotContain(await db.FoodAiProfiles.ToListAsync(), value => value.SearchText.Contains("budget", StringComparison.OrdinalIgnoreCase) || value.SearchText.Contains("quick_serve", StringComparison.OrdinalIgnoreCase));
            }

            Assert.Equal(dry.PlannedWrites, executed.PlannedWrites);

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
                var rerun = await new AiV2FoodMetadataBackfillService(db).RunAsync(new(AiV2BackfillMode.Execute, 13));
                _output.WriteLine("RERUN\n" + rerun.ToJson());
                Assert.True(rerun.TransactionCommitted);
                Assert.All(rerun.PlannedWrites, value => Assert.Equal(0, value.Value));
                Assert.Equal(legacyFoodRelations, await db.FoodItemTags.CountAsync());
                Assert.Equal(legacyPreferences, await db.CustomerPreferences.CountAsync());
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new NpgsqlConnection(adminBuilder.ConnectionString);
            await cleanup.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", cleanup);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static ServiceProvider Provider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<SNMDbContext>(options => options.UseNpgsql(connectionString));
        return services.BuildServiceProvider();
    }

    private static async Task SeedLegacyAiDataAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SET session_replication_role = replica;
            INSERT INTO "User" ("Id","RoleId","UserName","PasswordHash","FullName","Email","AuthProvider","Status","CreatedAt","UpdatedAt") VALUES
              ('22222222-2222-2222-2222-222222222101','22222222-2222-2222-2222-222222222001','customer-a','hash','Customer A','customer-a@test.local','Local','Active',now(),now()),
              ('22222222-2222-2222-2222-222222222102','22222222-2222-2222-2222-222222222001','customer-b','hash','Customer B','customer-b@test.local','Local','Active',now(),now()),
              ('22222222-2222-2222-2222-222222222103','22222222-2222-2222-2222-222222222002','owner-a','hash','Owner','owner-a@test.local','Local','Active',now(),now());
            INSERT INTO "NightMarket" ("Id","Name","Address","Status","ModerationStatus","IsDeleted","TotalBooth","CreatedAt","UpdatedAt")
              VALUES ('33333333-3333-3333-3333-333333333001','Audit market','Audit','Open','Active',false,1,now(),now());
            INSERT INTO "Booth" ("Id","RegistrationId","NightMarketId","BoothOwnerId","BoothName","Status","CreatedAt","UpdatedAt")
              VALUES ('44444444-4444-4444-4444-444444444001','44444444-4444-4444-4444-444444444099','33333333-3333-3333-3333-333333333001','22222222-2222-2222-2222-222222222103','Audit booth','Active',now(),now());
            INSERT INTO "FoodCategories" ("Id","BoothId","Code","Name","IsSystem","IsActive","DisplayOrder","IsSelectable","IsDeleted","CreatedAt","UpdatedAt")
              VALUES ('88888888-8888-8888-8888-888888888001','44444444-4444-4444-4444-444444444001','LEGACY_AUDIT','Legacy audit',false,true,0,true,false,now(),now());
            INSERT INTO "FoodItem" ("Id","BoothId","CategoryId","Name","Description","Price","IsAvailable","IsFeatured","IsDeleted","SpiceLevel","SemanticProfileVersion","CreatedAt","UpdatedAt") VALUES
              ('99999999-9999-9999-9999-999999990803','44444444-4444-4444-4444-444444444001','88888888-8888-8888-8888-888888888001','Bò viên nóng','Món nước nóng, dùng nhanh khi trời lạnh.',30000,true,false,false,'UNKNOWN',0,now(),now()),
              ('99999999-9999-9999-9999-999999990401','44444444-4444-4444-4444-444444444001','88888888-8888-8888-8888-888888888001','Bún num bò chóc','Món Campuchia đặc trưng, vị đậm và hơi cay.',50000,true,false,false,'UNKNOWN',0,now(),now()),
              ('99999999-9999-9999-9999-999999990202','44444444-4444-4444-4444-444444444001','88888888-8888-8888-8888-888888888001','Bún thịt nướng chả giò','Bún thịt nướng, rau sống, chả giò giòn.',50000,true,false,false,'UNKNOWN',0,now(),now()),
              ('99999999-9999-9999-9999-999999990301','44444444-4444-4444-4444-444444444001','88888888-8888-8888-8888-888888888001','Chè ba màu','Món tráng miệng lạnh phổ biến.',25000,true,false,false,'UNKNOWN',0,now(),now()),
              ('99999999-9999-9999-9999-999999990403','44444444-4444-4444-4444-444444444001','88888888-8888-8888-8888-888888888001','Gà nướng sả ớt','Gà nướng cay nhẹ, dùng với rau.',60000,true,false,false,'UNKNOWN',0,now(),now()),
              ('99999999-9999-9999-9999-999999990904','44444444-4444-4444-4444-444444444001','88888888-8888-8888-8888-888888888001','Nước ép dâu','Đồ uống lạnh từ dâu.',25000,true,false,false,'UNKNOWN',0,now(),now()),
              ('99999999-9999-9999-9999-999999990604','44444444-4444-4444-4444-444444444001','88888888-8888-8888-8888-888888888001','Nước mía tắc','Đồ uống đường phố giá tốt.',20000,true,false,false,'UNKNOWN',0,now(),now()),
              ('99999999-9999-9999-9999-999999990104','44444444-4444-4444-4444-444444444001','88888888-8888-8888-8888-888888888001','Nước sâm lạnh','Đồ uống mát.',20000,true,false,false,'UNKNOWN',0,now(),now()),
              ('99999999-9999-9999-9999-999999990303','44444444-4444-4444-4444-444444444001','88888888-8888-8888-8888-888888888001','Nước sâm rong biển','Đồ uống mát.',20000,true,false,false,'UNKNOWN',0,now(),now()),
              ('99999999-9999-9999-9999-999999990603','44444444-4444-4444-4444-444444444001','88888888-8888-8888-8888-888888888001','Sữa chua nếp cẩm','Tráng miệng lạnh, vị ngọt nhẹ.',25000,true,false,false,'UNKNOWN',0,now(),now()),
              ('99999999-9999-9999-9999-999999990903','44444444-4444-4444-4444-444444444001','88888888-8888-8888-8888-888888888001','Sữa chua phô mai','Tráng miệng lạnh, vị béo ngọt.',25000,true,false,false,'UNKNOWN',0,now(),now()),
              ('99999999-9999-9999-9999-999999990304','44444444-4444-4444-4444-444444444001','88888888-8888-8888-8888-888888888001','Trái cây dầm','Tráng miệng lạnh, nhiều trái cây.',30000,true,false,false,'UNKNOWN',0,now(),now());
            INSERT INTO "FoodTag" ("Id","Name","Code","TagGroup","Status","IsSystem","DisplayOrder","IsSelectable","IsPreferenceSelectable","IsAutoAssigned","IsDeleted","CreatedAt","UpdatedAt")
              SELECT uuid_generate_v4(), code, code, grp, status, false, 0, true, true, false, false, now(), now()
              FROM (VALUES ('SOUP','CookingMethod','Inactive'),('DRINK','MealPurpose','Active'),('DESSERT','MealPurpose','Active'),('BUDGETFRIENDLY','Budget','Active'),('NOODLE','Ingredient','Active'),('HOT','Temperature','Active'),('GRILLED','CookingMethod','Active'),('SPICY','Taste','Active'),('BEEF','Ingredient','Active'),('CHICKEN','Ingredient','Active'),('PORK','Ingredient','Active'),('SEAFOOD','Ingredient','Active'),('VIETNAMESE','Other','Active')) value(code,grp,status);
            INSERT INTO "FoodItemTag" ("FoodItemId","FoodTagId","CreatedAt") SELECT food."Id", tag."Id", now() FROM "FoodItem" food CROSS JOIN "FoodTag" tag WHERE tag."Code"='SOUP';
            INSERT INTO "FoodItemTag" ("FoodItemId","FoodTagId","CreatedAt") SELECT food."Id", tag."Id", now() FROM "FoodItem" food CROSS JOIN "FoodTag" tag WHERE tag."Code"='BUDGETFRIENDLY';
            INSERT INTO "FoodItemTag" ("FoodItemId","FoodTagId","CreatedAt") SELECT food."Id", tag."Id", now() FROM "FoodItem" food CROSS JOIN "FoodTag" tag WHERE tag."Code"='DRINK' AND food."Name" LIKE 'Nước%';
            INSERT INTO "FoodItemTag" ("FoodItemId","FoodTagId","CreatedAt") SELECT food."Id", tag."Id", now() FROM "FoodItem" food CROSS JOIN "FoodTag" tag WHERE tag."Code"='DESSERT' AND food."Name" IN ('Chè ba màu','Sữa chua nếp cẩm','Sữa chua phô mai','Trái cây dầm');
            INSERT INTO "FoodItemTag" ("FoodItemId","FoodTagId","CreatedAt") SELECT '99999999-9999-9999-9999-999999990403', tag."Id", now() FROM "FoodTag" tag WHERE tag."Code" IN ('CHICKEN','GRILLED','SPICY');
            INSERT INTO "FoodItemTag" ("FoodItemId","FoodTagId","CreatedAt") SELECT '99999999-9999-9999-9999-999999990401', tag."Id", now() FROM "FoodTag" tag WHERE tag."Code" IN ('BEEF','NOODLE','SPICY');
            INSERT INTO "CustomerPreference" ("Id","CustomerId","FoodTagId","PreferenceKind","PreferenceSource","CreatedAt","UpdatedAt")
              SELECT uuid_generate_v4(),'22222222-2222-2222-2222-222222222101',"Id",CASE WHEN "Code"='SEAFOOD' THEN 'Avoid' ELSE 'Like' END,'UserSelected',now(),now() FROM "FoodTag" WHERE "Code" IN ('DRINK','DESSERT','SEAFOOD');
            SET session_replication_role = origin;
            """, connection);
        await command.ExecuteNonQueryAsync();
    }
}

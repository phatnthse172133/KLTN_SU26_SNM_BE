using ApplicationLayer.AI.V2.Configuration;
using ApplicationLayer.AI.V2.Models;
using ApplicationLayer.AI.V2.Services;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class PostgresAiV2RuntimeRecommendationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero); // 19:00 Vietnam

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task RealPostgres_RecommendationOrderabilityAndBroadSemanticQueries()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        var databaseName = $"snm_aiv2_runtime_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        var testBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName };
        var dbOptions = new DbContextOptionsBuilder<SNMDbContext>().UseNpgsql(testBuilder.ConnectionString).Options;
        try
        {
            await using var db = new SNMDbContext(dbOptions);
            await db.Database.MigrateAsync();
            await SeedAsync(testBuilder.ConnectionString);
            var service = CreateService(db);

            var pho = await service.RecommendAsync(Guid.NewGuid(), new() { Query = "Phở" }, CancellationToken.None);
            var phoItems = pho.Data!.Items.Concat(pho.Data.NearMatches).ToArray();
            Assert.Contains(phoItems, value => value.FoodName == "Phở bò");
            Assert.DoesNotContain(phoItems, value => value.FoodName.Contains("Phô mai", StringComparison.OrdinalIgnoreCase));
            Assert.All(phoItems, value => Assert.True(value.CompatibilityScore >= 60));
            Assert.All(phoItems, value => Assert.Null(value.Market.DistanceMeters)); // no location does not trigger distance rejection

            var nearest = await service.RecommendAsync(Guid.NewGuid(), new()
            {
                Query = "Phở", Latitude = 10.0m, Longitude = 106.0m,
                UseDistanceRanking = true, SortPreference = "NEAREST_RELEVANT", PageSize = 10
            }, CancellationToken.None);
            var locatedPho = nearest.Data!.Items.Concat(nearest.Data.NearMatches).ToArray();
            Assert.True(locatedPho.Select(value => value.Market.Id).Distinct().Count() >= 3);
            Assert.True(Array.FindIndex(locatedPho, value => value.Market.Name == "Near market")
                < Array.FindIndex(locatedPho, value => value.Market.Name == "Middle market"));
            Assert.True(Array.FindIndex(locatedPho, value => value.Market.Name == "Middle market")
                < Array.FindIndex(locatedPho, value => value.Market.Name == "Far market"));
            Assert.All(locatedPho, value => { Assert.True(value.DistanceAvailable); Assert.NotNull(value.MarketDistanceMeters); });

            var withinTwoKm = await service.RecommendAsync(Guid.NewGuid(), new()
            {
                Query = "Phở", Latitude = 10.0m, Longitude = 106.0m,
                MaximumDistanceMeters = 2_000, PageSize = 10
            }, CancellationToken.None);
            var within = withinTwoKm.Data!.Items.Concat(withinTwoKm.Data.NearMatches).ToArray();
            Assert.Contains(within, value => value.Market.Name == "Near market");
            Assert.DoesNotContain(within, value => value.Market.Name is "Middle market" or "Far market");
            Assert.All(within, value => Assert.InRange(value.MarketDistanceMeters!.Value, 0, 2_000));

            var cold = await service.RecommendAsync(Guid.NewGuid(), new() { Query = "lạnh lạnh" }, CancellationToken.None);
            Assert.Contains(cold.Data!.Items.Concat(cold.Data.NearMatches), value => value.FoodName == "Nước sâm lạnh");

            var filling = await service.RecommendAsync(Guid.NewGuid(), new() { Query = "no no" }, CancellationToken.None);
            Assert.Contains(filling.Data!.Items.Concat(filling.Data.NearMatches), value => value.FoodName == "Cơm gà no lâu");

            foreach (var rejectedName in new[] { "Món đóng cửa", "Món ngưng bán", "Món không giá", "Món booth inactive" })
            {
                var rejected = await service.RecommendAsync(Guid.NewGuid(), new() { Query = rejectedName }, CancellationToken.None);
                Assert.DoesNotContain(rejected.Data!.Items.Concat(rejected.Data.NearMatches), value => value.FoodName == rejectedName);
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

    private static FoodRecommendationV2Service CreateService(SNMDbContext db)
    {
        var parser = new DeterministicFoodIntentParser();
        var extractor = new Mock<IAiIntentExtractor>();
        extractor.Setup(value => value.ExtractFoodRecommendationIntentAsync(It.IsAny<FoodRecommendationIntentRequest>(), It.IsAny<CancellationToken>()))
            .Returns<FoodRecommendationIntentRequest, CancellationToken>((request, _) => Task.FromResult(parser.Parse(request)));
        var explanation = new Mock<IAiExplanationGenerator>();
        explanation.Setup(value => value.GenerateFoodRecommendationReasonAsync(It.IsAny<FoodRecommendationExplanationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGeneratedTextResult { IsSuccess = true, Text = "Phù hợp với yêu cầu." });
        var sessions = new Mock<IAiRecommendationSessionRepository>();
        sessions.Setup(value => value.SaveSessionAsync(It.IsAny<AiRecommendationSession>(), It.IsAny<IReadOnlyCollection<AiRecommendationResult>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var metadata = new Mock<IFoodSemanticMetadataRepository>();
        metadata.Setup(value => value.GetActiveCatalogsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FoodSemanticCatalogSet([], [], [], [], []));
        metadata.Setup(value => value.GetCustomerProfileAsync(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerFoodProfile?)null);
        var options = Options.Create(new RecommendationV2Options { MaximumExplanationCalls = 0 });
        return new FoodRecommendationV2Service(extractor.Object, explanation.Object, new DeterministicRecommendationReasonBuilder(),
            new FoodRecommendationIntentNormalizer(options), new DeterministicFoodSemanticMatcher(), new FoodRecommendationRanker(options),
            new FoodRecommendationDiversityReranker(options), new FoodRecommendationReadRepository(db), sessions.Object, metadata.Object,
            options, new FixedTimeProvider(Now), Mock.Of<IHostEnvironment>(value => value.EnvironmentName == Environments.Production),
            NullLogger<FoodRecommendationV2Service>.Instance);
    }

    private static async Task SeedAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SET session_replication_role = replica;
            INSERT INTO "NightMarket" ("Id","Name","Address","Latitude","Longitude","OpeningHours","ClosingHours","Status","ModerationStatus","IsDeleted","TotalBooth","CreatedAt","UpdatedAt") VALUES
            ('31000000-0000-0000-0000-000000000001','Far market','Address',10.10,106.0,'16:00','23:59','Active','Active',false,3,now(),now()),
            ('31000000-0000-0000-0000-000000000002','Near market','Address',10.01,106.0,'16:00','23:59','Active','Active',false,1,now(),now()),
            ('31000000-0000-0000-0000-000000000003','Middle market','Address',10.05,106.0,'16:00','23:59','Active','Active',false,1,now(),now());
            INSERT INTO "Booth" ("Id","RegistrationId","NightMarketId","BoothOwnerId","BoothName","OpenTime","CloseTime","Status","CreatedAt","UpdatedAt") VALUES
            ('32000000-0000-0000-0000-000000000001','32000000-0000-0000-0000-000000000101','31000000-0000-0000-0000-000000000001','32000000-0000-0000-0000-000000000201','Open booth','16:00','23:59','Active',now(),now()),
            ('32000000-0000-0000-0000-000000000002','32000000-0000-0000-0000-000000000102','31000000-0000-0000-0000-000000000001','32000000-0000-0000-0000-000000000202','Closed booth','01:00','02:00','Active',now(),now()),
            ('32000000-0000-0000-0000-000000000003','32000000-0000-0000-0000-000000000103','31000000-0000-0000-0000-000000000001','32000000-0000-0000-0000-000000000203','Inactive booth','16:00','23:59','Inactive',now(),now()),
            ('32000000-0000-0000-0000-000000000004','32000000-0000-0000-0000-000000000104','31000000-0000-0000-0000-000000000002','32000000-0000-0000-0000-000000000204','Near booth','16:00','23:59','Active',now(),now()),
            ('32000000-0000-0000-0000-000000000005','32000000-0000-0000-0000-000000000105','31000000-0000-0000-0000-000000000003','32000000-0000-0000-0000-000000000205','Middle booth','16:00','23:59','Active',now(),now());
            INSERT INTO "FoodCategories" ("Id","BoothId","Code","Name","IsSystem","IsActive","DisplayOrder","IsSelectable","IsDeleted","CreatedAt","UpdatedAt") VALUES
            ('33000000-0000-0000-0000-000000000001','32000000-0000-0000-0000-000000000001','MAIN','Main',true,true,0,true,false,now(),now()),
            ('33000000-0000-0000-0000-000000000002','32000000-0000-0000-0000-000000000002','CLOSED','Closed',true,true,0,true,false,now(),now()),
            ('33000000-0000-0000-0000-000000000003','32000000-0000-0000-0000-000000000003','INACTIVE','Inactive',true,true,0,true,false,now(),now()),
            ('33000000-0000-0000-0000-000000000004','32000000-0000-0000-0000-000000000004','NEAR_MAIN','Main',true,true,0,true,false,now(),now()),
            ('33000000-0000-0000-0000-000000000005','32000000-0000-0000-0000-000000000005','MIDDLE_MAIN','Main',true,true,0,true,false,now(),now());
            INSERT INTO "FoodItem" ("Id","BoothId","CategoryId","Name","Description","Price","IsAvailable","IsFeatured","IsDeleted","SpiceLevel","ServingTemperature","CreatedAt","UpdatedAt") VALUES
            ('34000000-0000-0000-0000-000000000001','32000000-0000-0000-0000-000000000001','33000000-0000-0000-0000-000000000001','Phở bò','Phở bò truyền thống',80000,true,false,false,'NON_SPICY','HOT',now(),now()),
            ('34000000-0000-0000-0000-000000000002','32000000-0000-0000-0000-000000000001','33000000-0000-0000-0000-000000000001','Nước sâm lạnh','Đồ uống thanh mát giải nhiệt',30000,true,false,false,'NON_SPICY','COLD',now(),now()),
            ('34000000-0000-0000-0000-000000000003','32000000-0000-0000-0000-000000000001','33000000-0000-0000-0000-000000000001','Cơm gà no lâu','Món chính đủ no',65000,true,false,false,'NON_SPICY','HOT',now(),now()),
            ('34000000-0000-0000-0000-000000000004','32000000-0000-0000-0000-000000000002','33000000-0000-0000-0000-000000000002','Món đóng cửa','Không được order ngoài giờ',50000,true,false,false,'NON_SPICY','HOT',now(),now()),
            ('34000000-0000-0000-0000-000000000005','32000000-0000-0000-0000-000000000001','33000000-0000-0000-0000-000000000001','Món ngưng bán','Không available',50000,false,false,false,'NON_SPICY','HOT',now(),now()),
            ('34000000-0000-0000-0000-000000000006','32000000-0000-0000-0000-000000000001','33000000-0000-0000-0000-000000000001','Món không giá','Giá bằng không',0,true,false,false,'NON_SPICY','HOT',now(),now()),
            ('34000000-0000-0000-0000-000000000007','32000000-0000-0000-0000-000000000003','33000000-0000-0000-0000-000000000003','Món booth inactive','Booth inactive',50000,true,false,false,'NON_SPICY','HOT',now(),now()),
            ('34000000-0000-0000-0000-000000000008','32000000-0000-0000-0000-000000000004','33000000-0000-0000-0000-000000000004','Phở bò','Phở bò truyền thống',80000,true,false,false,'NON_SPICY','HOT',now(),now()),
            ('34000000-0000-0000-0000-000000000009','32000000-0000-0000-0000-000000000005','33000000-0000-0000-0000-000000000005','Phở bò','Phở bò truyền thống',80000,true,false,false,'NON_SPICY','HOT',now(),now());
            INSERT INTO "FoodItemCourse" ("FoodItemId","Course","IsPrimary") VALUES
            ('34000000-0000-0000-0000-000000000001','MAIN_COURSE',true),
            ('34000000-0000-0000-0000-000000000002','DRINK',true),
            ('34000000-0000-0000-0000-000000000003','MAIN_COURSE',true),
            ('34000000-0000-0000-0000-000000000008','MAIN_COURSE',true),
            ('34000000-0000-0000-0000-000000000009','MAIN_COURSE',true);
            INSERT INTO "FoodItemDiningPurpose" ("FoodItemId","Purpose") VALUES
            ('34000000-0000-0000-0000-000000000002','REFRESHMENT'),
            ('34000000-0000-0000-0000-000000000003','FULL_MEAL');
            SET session_replication_role = origin;
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

using ApplicationLayer.AI.DTOs;
using ApplicationLayer.AI.Services;
using ApplicationLayer.Services.Menus;
using DomainLayer.Entities;
using DomainLayer.Enums;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TestingLayer;

public sealed class PostgresAiV2CutoverTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task NormalizedWriteReadProfileAndSearchProfile_WorkOnPostgresWithoutLegacyTags()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        var databaseName = $"snm_aiv2_cutover_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        var testBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName };
        var options = new DbContextOptionsBuilder<SNMDbContext>().UseNpgsql(testBuilder.ConnectionString).Options;
        try
        {
            await using var db = new SNMDbContext(options); await db.Database.MigrateAsync();
            await SeedParents(testBuilder.ConnectionString);
            var ingredient = Catalog<Ingredient>("ING_BEEF"); ingredient.NormalizedName = "BEEF";
            var dietary = Catalog<DietaryAttribute>("DIET_TEST");
            var food = new FoodItem
            {
                Id = Guid.Parse("99999999-9999-9999-9999-999999999001"), BoothId = BoothId, CategoryId = CategoryId,
                Name = "Normalized beef", Description = "Verified source test", Price = 80000, IsAvailable = true,
                SpiceLevel = FoodSpiceLevel.MILD, EstimatedServingCount = 2, IsShareable = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                Courses = [new() { Course = FoodCourse.MAIN_COURSE, IsPrimary = true, CreatedAt = DateTime.UtcNow }],
                Ingredients = [new() { IngredientId = ingredient.Id, Ingredient = ingredient, CreatedAt = DateTime.UtcNow }],
                DietaryAttributes = [new() { DietaryAttributeId = dietary.Id, DietaryAttribute = dietary, SuitabilityStatus = DietarySuitabilityStatus.UNVERIFIED, Source = MetadataSource.OWNER_DECLARED, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }]
            };
            food.Category = await db.FoodCategories.SingleAsync(x => x.Id == CategoryId);
            new FoodAiProfileGenerator().Rebuild(food, DateTime.UtcNow);
            db.FoodItems.Add(food); await db.SaveChangesAsync(); db.ChangeTracker.Clear();

            var detail = await new FoodItemRepository(db).GetCustomerByIdAsync(food.Id, DateTime.UtcNow);
            Assert.NotNull(detail); Assert.Equal(FoodCourse.MAIN_COURSE, detail!.SemanticMetadata.PrimaryCourse);
            Assert.Single(detail.SemanticMetadata.Ingredients); Assert.Equal(2, detail.Tags.Count);
            Assert.Equal(0, await db.FoodItemTags.CountAsync(x => x.FoodItemId == food.Id));
            Assert.Contains("ing_beef", (await db.FoodAiProfiles.SingleAsync(x => x.FoodItemId == food.Id)).SearchText);

            var manualSoup = new FoodItem { Id = Guid.Parse("99999999-9999-9999-9999-999999990803"), BoothId = BoothId, CategoryId = CategoryId, Name = "Bò viên nóng", Price = 30000, IsAvailable = true, SpiceLevel = FoodSpiceLevel.UNKNOWN, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.FoodItems.Add(manualSoup); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.Null((await new FoodItemRepository(db).GetCustomerByIdAsync(manualSoup.Id, DateTime.UtcNow))!.SemanticMetadata.PrimaryCourse);

            var service = new CustomerFoodProfileService(new FoodSemanticMetadataRepository(db));
            await service.UpdateMineAsync(CustomerId, new UpdateCustomerFoodProfileRequest { PreferredIngredientIds = [ingredient.Id], PreferredCourses = [FoodCourse.MAIN_COURSE] });
            await service.UpdateMineAsync(CustomerId, new UpdateCustomerFoodProfileRequest { PreferredIngredientIds = [ingredient.Id], PreferredCourses = [FoodCourse.MAIN_COURSE] });
            Assert.Equal(1, await db.CustomerPreferredIngredients.CountAsync(x => x.CustomerId == CustomerId));
            await service.UpdateMineAsync(CustomerId, new UpdateCustomerFoodProfileRequest { AvoidedIngredientIds = [ingredient.Id] });
            Assert.Equal(0, await db.CustomerPreferredIngredients.CountAsync(x => x.CustomerId == CustomerId));
            Assert.Equal(1, await db.CustomerAvoidedIngredients.CountAsync(x => x.CustomerId == CustomerId));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new NpgsqlConnection(adminBuilder.ConnectionString); await cleanup.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", cleanup); await drop.ExecuteNonQueryAsync();
        }
    }

    private static readonly Guid CustomerId = Guid.Parse("22222222-2222-2222-2222-222222222101");
    private static readonly Guid BoothId = Guid.Parse("44444444-4444-4444-4444-444444444001");
    private static readonly Guid CategoryId = Guid.Parse("88888888-8888-8888-8888-888888888001");
    private static async Task SeedParents(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SET session_replication_role = replica;
            INSERT INTO "User" ("Id","RoleId","UserName","PasswordHash","FullName","Email","AuthProvider","Status","CreatedAt","UpdatedAt") VALUES
            ('22222222-2222-2222-2222-222222222101','22222222-2222-2222-2222-222222222001','customer','hash','Customer','customer@cutover.local','Local','Active',now(),now()),
            ('22222222-2222-2222-2222-222222222102','22222222-2222-2222-2222-222222222002','owner','hash','Owner','owner@cutover.local','Local','Active',now(),now());
            INSERT INTO "NightMarket" ("Id","Name","Address","Status","ModerationStatus","IsDeleted","TotalBooth","CreatedAt","UpdatedAt") VALUES
            ('33333333-3333-3333-3333-333333333001','Cutover market','Address','Active','Active',false,1,now(),now());
            INSERT INTO "Booth" ("Id","RegistrationId","NightMarketId","BoothOwnerId","BoothName","Status","CreatedAt","UpdatedAt") VALUES
            ('44444444-4444-4444-4444-444444444001','44444444-4444-4444-4444-444444444099','33333333-3333-3333-3333-333333333001','22222222-2222-2222-2222-222222222102','Cutover booth','Active',now(),now());
            INSERT INTO "FoodCategories" ("Id","BoothId","Code","Name","IsSystem","IsActive","DisplayOrder","IsSelectable","IsDeleted","CreatedAt","UpdatedAt") VALUES
            ('88888888-8888-8888-8888-888888888001','44444444-4444-4444-4444-444444444001','MAIN','Main',true,true,0,true,false,now(),now());
            SET session_replication_role = origin;
            """, connection);
        await command.ExecuteNonQueryAsync();
    }
    private static T Catalog<T>(string code) where T : SemanticCatalogEntity, new()
        => new() { Id = Guid.NewGuid(), Code = code, Name = code, IsActive = true, IsSystem = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
}

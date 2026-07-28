using ApplicationLayer.AI.Services;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Menus;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Seeders;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class SystemFoodTaxonomyTests
{
    [Fact]
    public async Task Seeder_CreatesExpectedCatalog_AndSecondRunIsIdempotent()
    {
        await using var provider = Provider();

        var first = await SystemFoodTaxonomySeeder.SeedAsync(provider);
        var second = await SystemFoodTaxonomySeeder.SeedAsync(provider);

        Assert.Equal(18, first.CategoriesInserted);
        Assert.Equal(94, first.TagsInserted);
        Assert.Equal(0, second.CategoriesInserted + second.CategoriesUpdated + second.TagsInserted + second.TagsUpdated);
        Assert.Equal(18, second.CategoriesSkipped);
        Assert.Equal(94, second.TagsSkipped);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        Assert.Equal(18, await db.FoodCategories.CountAsync(category => category.IsSystem));
        Assert.Equal(94, await db.FoodTags.CountAsync(tag => tag.IsSystem));
        Assert.False(await db.FoodCategories.GroupBy(category => category.Code).AnyAsync(group => group.Count() > 1));
        Assert.False(await db.FoodTags.GroupBy(tag => tag.Code).AnyAsync(group => group.Count() > 1));

        var systemTags = await db.FoodTags.Where(tag => tag.IsSystem).ToListAsync();
        var counts = systemTags.GroupBy(tag => tag.TagGroup).ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(10, counts[FoodTagGroup.Taste]);
        Assert.Equal(3, counts[FoodTagGroup.Temperature]);
        Assert.Equal(10, counts[FoodTagGroup.MealPurpose]);
        Assert.Equal(15, counts[FoodTagGroup.CookingMethod]);
        Assert.Equal(30, counts[FoodTagGroup.Ingredient]);
        Assert.Equal(13, counts[FoodTagGroup.Dietary]);
        Assert.Equal(5, counts[FoodTagGroup.Budget]);
        Assert.Equal(8, counts[FoodTagGroup.Other]);
        Assert.All(await db.FoodTags.Where(tag => tag.TagGroup == FoodTagGroup.Budget).ToListAsync(), tag =>
        {
            Assert.True(tag.IsAutoAssigned);
            Assert.False(tag.IsSelectable);
        });
    }

    [Fact]
    public async Task MenuCreateAndUpdate_PersistValidatedSystemCategoryAndTags()
    {
        await using var provider = Provider();
        await SystemFoodTaxonomySeeder.SeedAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var ownerId = Guid.NewGuid();
        var booth = new Booth { Id = Guid.NewGuid(), BoothOwnerId = ownerId, BoothName = "Test", Status = BoothStatus.Active };
        var category = await db.FoodCategories.SingleAsync(item => item.Code == "GRILLED_FOOD");
        var grilled = await db.FoodTags.SingleAsync(item => item.Code == "METHOD_GRILLED");
        var spicy = await db.FoodTags.SingleAsync(item => item.Code == "TASTE_SPICY");
        var sweet = await db.FoodTags.SingleAsync(item => item.Code == "TASTE_SWEET");

        var booths = new Mock<IBoothRepository>();
        booths.Setup(repository => repository.GetOwnedBoothAsync(ownerId, booth.Id)).ReturnsAsync(booth);
        var mapper = new MapperConfiguration(config => config.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var service = new MenuService(booths.Object, new FoodCategoryRepository(db), new FoodItemRepository(db),
            new FoodTagRepository(db), mapper);

        var created = await service.CreateFoodItemAsync(ownerId, booth.Id, new CreateFoodItemRequest
        {
            CategoryId = category.Id, Name = "Món thử", Price = 50_000, TagIds = [grilled.Id, spicy.Id]
        });
        var foodId = created.Data!.Id;
        Assert.Equal(2, await db.FoodItemTags.CountAsync(item => item.FoodItemId == foodId));

        var updated = await service.UpdateFoodItemAsync(ownerId, booth.Id, foodId, new UpdateFoodItemRequest
        {
            CategoryId = category.Id, Name = "Món thử cập nhật", Price = 60_000, TagIds = [grilled.Id, sweet.Id]
        });
        var expectedIds = new[] { grilled.Id, sweet.Id }.Order().ToArray();
        Assert.Equal(expectedIds, updated.Data!.TagIds.Order().ToArray());
        var savedIds = await db.FoodItemTags.Where(item => item.FoodItemId == foodId)
            .Select(item => item.FoodTagId).ToListAsync();
        Assert.Equal(expectedIds, savedIds.Order().ToArray());
    }

    [Fact]
    public void SelectionPolicy_RejectsAutoAssignedAndConflictingSpiceTags()
    {
        Assert.Throws<ApplicationLayer.Exceptions.AppException>(() => FoodTagSelectionPolicy.Validate(
            [Tag("BUDGET_UNDER_30000", FoodTagGroup.Budget, selectable: false, auto: true)]));
        Assert.Throws<ApplicationLayer.Exceptions.AppException>(() => FoodTagSelectionPolicy.Validate(
            [Tag("TASTE_MILD_SPICY", FoodTagGroup.Taste), Tag("TASTE_SPICY", FoodTagGroup.Taste)]));
    }

    private static FoodTag Tag(string code, FoodTagGroup group, bool selectable = true, bool auto = false)
        => new() { Id = Guid.NewGuid(), Code = code, Name = code, TagGroup = group, Status = FoodTagStatus.Active,
            IsSelectable = selectable, IsAutoAssigned = auto };

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddDbContext<SNMDbContext>(options => options.UseInMemoryDatabase(databaseName));
        return services.BuildServiceProvider();
    }
}

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
        Assert.Equal(0, second.CategoriesInserted + second.CategoriesUpdated);
        Assert.Equal(18, second.CategoriesSkipped);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        Assert.Equal(18, await db.FoodCategories.CountAsync(category => category.IsSystem));
        Assert.False(await db.FoodCategories.GroupBy(category => category.Code).AnyAsync(group => group.Count() > 1));
    }

    [Fact]
    public async Task MenuCreateAndUpdate_PersistValidatedSystemCategory()
    {
        await using var provider = Provider();
        await SystemFoodTaxonomySeeder.SeedAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var ownerId = Guid.NewGuid();
        var booth = new Booth { Id = Guid.NewGuid(), BoothOwnerId = ownerId, BoothName = "Test", Status = BoothStatus.Active };
        var category = await db.FoodCategories.SingleAsync(item => item.Code == "GRILLED_FOOD");

        var booths = new Mock<IBoothRepository>();
        booths.Setup(repository => repository.GetOwnedBoothAsync(ownerId, booth.Id)).ReturnsAsync(booth);
        var mapper = new MapperConfiguration(config => config.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var service = new MenuService(booths.Object, new FoodCategoryRepository(db), new FoodItemRepository(db), mapper);

        var created = await service.CreateFoodItemAsync(ownerId, booth.Id, new CreateFoodItemRequest
        {
            CategoryId = category.Id, Name = "Món thử", Price = 50_000
        });
        var foodId = created.Data!.Id;
        Assert.Equal("Món thử", created.Data.Name);

        var updated = await service.UpdateFoodItemAsync(ownerId, booth.Id, foodId, new UpdateFoodItemRequest
        {
            CategoryId = category.Id, Name = "Món thử cập nhật", Price = 60_000
        });
        Assert.Equal("Món thử cập nhật", updated.Data!.Name);
        Assert.Equal(60_000, updated.Data.Price);
        var saved = await db.FoodItems.SingleAsync(item => item.Id == foodId);
        Assert.Equal("Món thử cập nhật", saved.Name);
    }

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddDbContext<SNMDbContext>(options => options.UseInMemoryDatabase(databaseName));
        return services.BuildServiceProvider();
    }
}

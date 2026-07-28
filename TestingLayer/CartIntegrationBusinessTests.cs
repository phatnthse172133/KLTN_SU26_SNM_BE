using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Carts;
using ApplicationLayer.Services.CustomerDiscovery;
using AutoMapper;
using DomainLayer.InterfaceCore.JWT;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Seeders;
using InfrastructureLayer.Repositories;
using InfrastructureLayer.Cores.Helppers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestingLayer;

public sealed class CartIntegrationBusinessTests
{
    private static readonly DateTime OpenUtc =
        new(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc); // 19:00 Vietnam

    [Fact]
    public async Task RealRepositories_CartLifecycle_PreservesBackendBusinessRules()
    {
        await using var provider = CreateProvider();
        await IntegrationDemoDataSeeder.SeedAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var customerId = Guid.Parse("d3500000-0000-0000-0000-000000000010");
        var mapper = new MapperConfiguration(
            configuration => configuration.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        var service = new CartService(
            new CartRepository(db),
            new CartItemRepository(db),
            new FoodItemRepository(db),
            mapper,
            new FixedTimeProvider(OpenUtc));

        var empty = await Assert.ThrowsAsync<AppException>(() =>
            service.GetCurrentAsync(customerId, new PaginationReq()));
        Assert.Equal("CART_NOT_FOUND", empty.ErrorCode);

        var firstFoodId = IntegrationDemoDataSeeder.FoodId(1, 1);
        var firstFood = await db.FoodItems
            .Include(food => food.FoodPrices)
            .SingleAsync(food => food.Id == firstFoodId);
        var first = await service.AddItemAsync(customerId, new AddCartItemRequest
        {
            FoodItemId = firstFoodId,
            Quantity = 2
        });
        Assert.InRange(first.Data!.CurrentUnitPrice, 1m, firstFood.Price);
        Assert.Equal(first.Data.CurrentUnitPrice * 2, first.Data.LineTotal);

        var updated = await service.UpdateQuantityAsync(customerId, first.Data.CartItemId,
            new UpdateCartItemQuantityRequest { Quantity = 3 });
        Assert.Equal(3, updated.Data!.Quantity);
        Assert.Equal(updated.Data.CurrentUnitPrice * 3, updated.Data.LineTotal);

        var second = await service.AddItemAsync(customerId, new AddCartItemRequest
        {
            FoodItemId = IntegrationDemoDataSeeder.FoodId(1, 2),
            Quantity = 1
        });
        var anotherBooth = await service.AddItemAsync(customerId, new AddCartItemRequest
        {
            FoodItemId = IntegrationDemoDataSeeder.FoodId(2, 1),
            Quantity = 1
        });

        var grouped = (await service.GetCurrentAsync(customerId,
            new PaginationReq { Page = 1, PageSize = 20 })).Data!;
        Assert.Equal(5, grouped.TotalItemCount);
        Assert.Equal(2, grouped.Booths.Total);
        Assert.Equal(grouped.Booths.Items.Sum(booth => booth.Subtotal), grouped.TotalAmount);
        Assert.True(grouped.CanCheckout);

        var invalidQuantity = await Assert.ThrowsAsync<AppException>(() =>
            service.UpdateQuantityAsync(customerId, second.Data!.CartItemId,
                new UpdateCartItemQuantityRequest { Quantity = 0 }));
        Assert.Equal(400, invalidQuantity.StatusCode);
        Assert.Equal("INVALID_QUANTITY", invalidQuantity.ErrorCode);

        var unavailable = await Assert.ThrowsAsync<AppException>(() =>
            service.AddItemAsync(customerId, new AddCartItemRequest
            {
                FoodItemId = IntegrationDemoDataSeeder.FoodId(5, 4),
                Quantity = 1
            }));
        Assert.Equal(409, unavailable.StatusCode);

        await service.RemoveItemAsync(customerId, second.Data!.CartItemId);
        await service.RemoveBoothItemsAsync(customerId, IntegrationDemoDataSeeder.BoothId(2));
        var oneBooth = (await service.GetCurrentAsync(customerId,
            new PaginationReq { Page = 1, PageSize = 20 })).Data!;
        Assert.Equal(3, oneBooth.TotalItemCount);
        Assert.Single(oneBooth.Booths.Items);

        await service.ClearAsync(customerId);
        var cleared = await Assert.ThrowsAsync<AppException>(() =>
            service.GetCurrentAsync(customerId, new PaginationReq()));
        Assert.Equal("CART_NOT_FOUND", cleared.ErrorCode);
        Assert.True((await db.CartItems.IgnoreQueryFilters().SingleAsync(
            item => item.Id == anotherBooth.Data!.CartItemId)).IsDeleted);
    }

    [Fact]
    public async Task AppliedPostgres_CartLifecycle_IsTransactionalAndUsesSeededCatalog()
    {
        var connectionString = Environment.GetEnvironmentVariable("SNM_APPLIED_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new SNMDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var customerId = Guid.Parse("d3500000-0000-0000-0000-000000000010");
            Assert.True(await db.Users.IgnoreQueryFilters().AnyAsync(user => user.Id == customerId));

            var existingCarts = await db.Carts.IgnoreQueryFilters()
                .Where(cart => cart.CustomerId == customerId && !cart.IsDeleted)
                .ToListAsync();
            var existingCartIds = existingCarts.Select(cart => cart.Id).ToList();
            var existingItems = await db.CartItems.IgnoreQueryFilters()
                .Where(item => existingCartIds.Contains(item.CartId) && !item.IsDeleted)
                .ToListAsync();
            foreach (var item in existingItems) item.IsDeleted = true;
            foreach (var cart in existingCarts) cart.IsDeleted = true;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var service = new CartService(
                new CartRepository(db),
                new CartItemRepository(db),
                new FoodItemRepository(db),
                new MapperConfiguration(
                    configuration => configuration.AddProfile<MappingProfile>(),
                    NullLoggerFactory.Instance).CreateMapper(),
                new FixedTimeProvider(OpenUtc));

            var added = await service.AddItemAsync(customerId, new AddCartItemRequest
            {
                FoodItemId = IntegrationDemoDataSeeder.FoodId(1, 1),
                Quantity = 2
            });
            Assert.True(added.Data!.CurrentUnitPrice > 0);
            Assert.Equal(added.Data.CurrentUnitPrice * 2, added.Data.LineTotal);

            await service.UpdateQuantityAsync(customerId, added.Data.CartItemId,
                new UpdateCartItemQuantityRequest { Quantity = 4 });
            await service.AddItemAsync(customerId, new AddCartItemRequest
            {
                FoodItemId = IntegrationDemoDataSeeder.FoodId(1, 2), Quantity = 1
            });
            await service.AddItemAsync(customerId, new AddCartItemRequest
            {
                FoodItemId = IntegrationDemoDataSeeder.FoodId(2, 1), Quantity = 1
            });

            var snapshot = (await service.GetCurrentAsync(customerId,
                new PaginationReq { PageSize = 20 })).Data!;
            Assert.Equal(6, snapshot.TotalItemCount);
            Assert.Equal(2, snapshot.Booths.Total);
            Assert.Equal(snapshot.Booths.Items.Sum(booth => booth.Subtotal), snapshot.TotalAmount);

            var invalid = await Assert.ThrowsAsync<AppException>(() =>
                service.AddItemAsync(customerId, new AddCartItemRequest
                {
                    FoodItemId = IntegrationDemoDataSeeder.FoodId(1, 1), Quantity = 0
                }));
            Assert.Equal("INVALID_QUANTITY", invalid.ErrorCode);

            var unavailable = await Assert.ThrowsAsync<AppException>(() =>
                service.AddItemAsync(customerId, new AddCartItemRequest
                {
                    FoodItemId = IntegrationDemoDataSeeder.FoodId(5, 4), Quantity = 1
                }));
            Assert.Equal(409, unavailable.StatusCode);

            await service.RemoveItemAsync(customerId, added.Data.CartItemId);
            await service.RemoveBoothItemsAsync(customerId, IntegrationDemoDataSeeder.BoothId(2));
            await service.ClearAsync(customerId);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddLogging();
        services.AddDbContext<SNMDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedDemoData"] = "true",
                ["SeedDemoDataPassword"] = Guid.NewGuid().ToString("N") + "!Aa1"
            }).Build());
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        return services.BuildServiceProvider();
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}

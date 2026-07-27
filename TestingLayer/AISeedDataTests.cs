using DomainLayer.InterfaceCore.JWT;
using InfrastructureLayer.Cores.Helppers;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class AISeedDataTests
{
    [Fact]
    public void IsEnabled_RequiresExplicitTrueFlag()
    {
        Assert.False(AISeedData.IsEnabled(Configuration()));
        Assert.False(AISeedData.IsEnabled(Configuration(("SeedDemoData", "false"))));
        Assert.True(AISeedData.IsEnabled(Configuration(("SeedDemoData", "true"))));
    }

    [Fact]
    public async Task SeedAsync_CreatesSmallValidIsolatedDataset_AndIsIdempotent()
    {
        const string password = "ControlledSeed!2026";
        var databaseName = Guid.NewGuid().ToString("N");
        await using var provider = Provider(databaseName, password);

        await AISeedData.SeedAsync(provider);
        await AssertDatasetAsync(provider, password);

        // Simulate an interrupted/older seed that left the stable demo user with
        // another valid hash. An explicit seed rerun must restore configured access.
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var customer = await context.Users.SingleAsync(user => user.UserName == "demo.customer.ai");
            customer.PasswordHash = hasher.HashPassword("StaleDemoPassword!2026");
            await context.SaveChangesAsync();
        }

        await AISeedData.SeedAsync(provider);
        await AssertDatasetAsync(provider, password);
    }

    private static async Task AssertDatasetAsync(ServiceProvider provider, string password)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        Assert.Equal(20, await context.FoodTags.CountAsync());
        Assert.Equal(2, await context.NightMarkets.CountAsync());
        Assert.Equal(6, await context.Zones.CountAsync());
        Assert.Equal(6, await context.Booths.CountAsync());
        Assert.Equal(18, await context.FoodItems.CountAsync());
        Assert.Equal(18, await context.FoodItemTags.Select(tag => tag.FoodItemId).Distinct().CountAsync());
        Assert.Single(await context.FoodPrices.ToListAsync());
        Assert.Equal(2, await context.Orders.CountAsync());
        Assert.Equal(2, await context.OrderDetails.CountAsync());
        Assert.Equal(2, await context.Payments.CountAsync());
        Assert.Equal(2, await context.Reviews.CountAsync());

        var alwaysOrderableMarket = await context.NightMarkets.SingleAsync(market =>
            market.Id == Guid.Parse("33333333-3333-3333-3333-333333333002"));
        Assert.Equal(NightMarketStatus.Open, alwaysOrderableMarket.Status);
        Assert.Equal(new TimeOnly(0, 0), alwaysOrderableMarket.OpeningHours);
        Assert.Equal(new TimeOnly(23, 59), alwaysOrderableMarket.ClosingHours);
        Assert.Equal(3, await context.Booths.CountAsync(booth =>
            booth.NightMarketId == alwaysOrderableMarket.Id
            && booth.Status == BoothStatus.Active
            && booth.OpenTime == new TimeOnly(0, 0)
            && booth.CloseTime == new TimeOnly(23, 59)));

        var customers = await context.Users
            .Where(user => user.UserName.StartsWith("demo.customer.ai"))
            .OrderBy(user => user.UserName)
            .ToListAsync();
        Assert.Equal(2, customers.Count);
        Assert.All(customers, customer => Assert.True(hasher.VerifyPassword(password, customer.PasswordHash)));

        var preferenceRows = await context.CustomerPreferences
            .Include(preference => preference.FoodTag)
            .ToListAsync();
        var profiles = preferenceRows
            .GroupBy(preference => preference.CustomerId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(preference => $"{preference.PreferenceKind}:{preference.FoodTag.Code}").Order().ToArray());
        Assert.Equal(2, profiles.Count);
        Assert.NotEqual(string.Join('|', profiles.Values.First()), string.Join('|', profiles.Values.Last()));
        Assert.All(
            profiles.Keys,
            customerId => Assert.DoesNotContain(
                preferenceRows
                    .Where(preference => preference.CustomerId == customerId)
                    .GroupBy(preference => preference.FoodTagId),
                group => group.Select(preference => preference.PreferenceKind).Distinct().Count() > 1));

        Assert.All(await context.Orders.ToListAsync(), order => Assert.Equal(OrderStatus.Completed, order.Status));
        Assert.All(await context.Payments.ToListAsync(), payment =>
        {
            Assert.Equal(PaymentType.Cash, payment.Type);
            Assert.Equal(PaymentGateway.None, payment.Gateway);
            Assert.Equal(PaymentStatus.Paid, payment.Status);
            Assert.NotNull(payment.PaidAt);
        });
    }

    private static ServiceProvider Provider(string databaseName, string password)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<SNMDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<IConfiguration>(Configuration(
            ("SeedDemoData", "true"),
            ("SeedDemoDataPassword", password)));
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        return services.BuildServiceProvider();
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(value => value.Key, value => (string?)value.Value))
            .Build();
}

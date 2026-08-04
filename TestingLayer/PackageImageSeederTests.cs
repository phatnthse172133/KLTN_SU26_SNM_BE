using DomainLayer.Entities;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace TestingLayer;

public class PackageImageSeederTests
{
    private static (ServiceProvider Provider, DbContextOptions<SNMDbContext> Options) CreateProvider(string dbName)
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var services = new ServiceCollection();
        services.AddDbContext<SNMDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddLogging();
        return (services.BuildServiceProvider(), options);
    }

    [Fact]
    public async Task SeedAsync_SetsSeedImageOnlyWhenImageUrlIsEmpty()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);

        using (var seedCtx = new SNMDbContext(options))
        {
            seedCtx.Packages.AddRange(
                new Package { Id = Guid.NewGuid(), Code = "MARKET_BASIC", PackageName = "Market Basic", Price = 100 },
                new Package { Id = Guid.NewGuid(), Code = "MARKET_PRO", PackageName = "Market Pro", Price = 300, ImageUrl = "/uploads/images/packages/admin-upload.png" },
                new Package { Id = Guid.NewGuid(), Code = "BOOTH_FREE", PackageName = "Booth Basic", Price = 0 },
                new Package { Id = Guid.NewGuid(), Code = "BOOTH_GROWTH", PackageName = "Booth Boost", Price = 50, ImageUrl = "" },
                new Package { Id = Guid.NewGuid(), Code = "BOOTH_FEATURED", PackageName = "Booth Featured", Price = 150 }
            );
            await seedCtx.SaveChangesAsync();
        }

        await PackageImageSeeder.SeedAsync(provider);

        using var verifyCtx = new SNMDbContext(options);
        var packages = verifyCtx.Packages.ToDictionary(p => p.Code!);
        Assert.Equal("/uploads/images/packages/seed/market_basic.png", packages["MARKET_BASIC"].ImageUrl);
        Assert.Equal("/uploads/images/packages/admin-upload.png", packages["MARKET_PRO"].ImageUrl);
        Assert.Equal("/uploads/images/packages/seed/booth_free.png", packages["BOOTH_FREE"].ImageUrl);
        Assert.Equal("/uploads/images/packages/seed/booth_growth.png", packages["BOOTH_GROWTH"].ImageUrl);
        Assert.Equal("/uploads/images/packages/seed/booth_featured.png", packages["BOOTH_FEATURED"].ImageUrl);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_IsIdempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);

        using (var seedCtx = new SNMDbContext(options))
        {
            seedCtx.Packages.Add(new Package { Id = Guid.NewGuid(), Code = "BOOTH_GROWTH", PackageName = "Booth Boost", Price = 50 });
            await seedCtx.SaveChangesAsync();
        }

        await PackageImageSeeder.SeedAsync(provider);

        DateTime firstUpdatedAt;
        using (var midCtx = new SNMDbContext(options))
        {
            firstUpdatedAt = midCtx.Packages.Single().UpdatedAt;
        }

        await PackageImageSeeder.SeedAsync(provider);

        using var verifyCtx = new SNMDbContext(options);
        var package = verifyCtx.Packages.Single();
        Assert.Equal("/uploads/images/packages/seed/booth_growth.png", package.ImageUrl);
        Assert.Equal(firstUpdatedAt, package.UpdatedAt);
    }

    [Fact]
    public async Task SeedAsync_MissingPackages_DoesNothing()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);

        await PackageImageSeeder.SeedAsync(provider);

        using var verifyCtx = new SNMDbContext(options);
        Assert.Empty(verifyCtx.Packages.ToList());
    }
}

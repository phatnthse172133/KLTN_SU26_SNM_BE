using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Data.Seeders;

public static class SystemFoodTaxonomySeeder
{
    public static async Task<SeedResult> SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SystemFoodTaxonomySeeder");
        IDbContextTransaction? transaction = null;
        if (db.Database.IsRelational())
            transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await UpsertAsync(db, cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            logger.LogInformation(
                "System food taxonomy seed completed. Categories inserted={CategoryInserted}, updated={CategoryUpdated}, skipped={CategorySkipped}.",
                result.CategoriesInserted, result.CategoriesUpdated, result.CategoriesSkipped);
            return result;
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            logger.LogError("System food taxonomy seed failed; transaction rolled back.");
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private static async Task<SeedResult> UpsertAsync(SNMDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var categoryCodes = SystemFoodTaxonomyCatalog.Categories.Select(item => item.Code).ToArray();
        var categories = await db.FoodCategories.IgnoreQueryFilters()
            .Where(item => categoryCodes.Contains(item.Code)).ToDictionaryAsync(item => item.Code, cancellationToken);
        var ci = 0; var cu = 0; var cs = 0;
        foreach (var definition in SystemFoodTaxonomyCatalog.Categories)
        {
            if (!categories.TryGetValue(definition.Code, out var entity))
            {
                db.FoodCategories.Add(new FoodCategory
                {
                    Id = definition.Id, Code = definition.Code, Name = definition.Name, BoothId = null,
                    IsSystem = true, IsActive = true, IsSelectable = true, DisplayOrder = definition.DisplayOrder,
                    IsDeleted = false, CreatedAt = now, UpdatedAt = now
                });
                ci++;
            }
            else if (Apply(entity, definition, now)) cu++; else cs++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new SeedResult(ci, cu, cs);
    }

    private static bool Apply(FoodCategory entity, FoodCategoryDefinition value, DateTime now)
    {
        var changed = entity.Name != value.Name || entity.BoothId is not null || !entity.IsSystem || !entity.IsActive
            || !entity.IsSelectable || entity.DisplayOrder != value.DisplayOrder || entity.IsDeleted;
        if (!changed) return false;
        entity.Name = value.Name; entity.BoothId = null; entity.IsSystem = true; entity.IsActive = true;
        entity.IsSelectable = true; entity.DisplayOrder = value.DisplayOrder; entity.IsDeleted = false; entity.UpdatedAt = now;
        return true;
    }
}

public sealed record SeedResult(int CategoriesInserted, int CategoriesUpdated, int CategoriesSkipped);

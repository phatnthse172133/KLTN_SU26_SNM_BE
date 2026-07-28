using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Data.Seeders;

public static class SystemFoodTaxonomySeeder
{
    private static readonly string[] LegacyTagCodes =
    [
        "SPICY", "MILD", "GRILLED", "FRIED", "SOUP", "HOT", "FULLMEAL", "SNACK", "DRINK", "DESSERT",
        "SHAREABLE", "CHICKEN", "BEEF", "PORK", "SEAFOOD", "NOODLE", "RICE", "BUDGETFRIENDLY", "MIDRANGE", "VIETNAMESE"
    ];

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
                "System food taxonomy seed completed. Categories inserted={CategoryInserted}, updated={CategoryUpdated}, skipped={CategorySkipped}; tags inserted={TagInserted}, updated={TagUpdated}, skipped={TagSkipped}; legacy tags deactivated={LegacyDeactivated}.",
                result.CategoriesInserted, result.CategoriesUpdated, result.CategoriesSkipped,
                result.TagsInserted, result.TagsUpdated, result.TagsSkipped, result.LegacyTagsDeactivated);
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

        var tagCodes = SystemFoodTaxonomyCatalog.Tags.Select(item => item.Code).ToArray();
        var tags = await db.FoodTags.IgnoreQueryFilters()
            .Where(item => tagCodes.Contains(item.Code)).ToDictionaryAsync(item => item.Code, cancellationToken);
        var ti = 0; var tu = 0; var ts = 0;
        foreach (var definition in SystemFoodTaxonomyCatalog.Tags)
        {
            if (!tags.TryGetValue(definition.Code, out var entity))
            {
                db.FoodTags.Add(new FoodTag
                {
                    Id = definition.Id, Code = definition.Code, Name = definition.Name, Description = definition.Name,
                    TagGroup = definition.Group, Status = FoodTagStatus.Active, IsSystem = true,
                    DisplayOrder = definition.DisplayOrder, IsSelectable = definition.IsSelectable,
                    IsPreferenceSelectable = definition.IsPreferenceSelectable, IsAutoAssigned = definition.IsAutoAssigned,
                    IsDeleted = false, CreatedAt = now, UpdatedAt = now
                });
                ti++;
            }
            else if (Apply(entity, definition, now)) tu++; else ts++;
        }

        var legacy = await db.FoodTags.Where(tag => LegacyTagCodes.Contains(tag.Code) && tag.Status == FoodTagStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var tag in legacy)
        {
            tag.Status = FoodTagStatus.Inactive;
            tag.IsSelectable = false;
            tag.IsPreferenceSelectable = false;
            tag.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new SeedResult(ci, cu, cs, ti, tu, ts, legacy.Count);
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

    private static bool Apply(FoodTag entity, FoodTagDefinition value, DateTime now)
    {
        var changed = entity.Name != value.Name || entity.Description != value.Name || entity.TagGroup != value.Group
            || entity.Status != FoodTagStatus.Active || !entity.IsSystem || entity.DisplayOrder != value.DisplayOrder
            || entity.IsSelectable != value.IsSelectable || entity.IsPreferenceSelectable != value.IsPreferenceSelectable
            || entity.IsAutoAssigned != value.IsAutoAssigned || entity.IsDeleted;
        if (!changed) return false;
        entity.Name = value.Name; entity.Description = value.Name; entity.TagGroup = value.Group;
        entity.Status = FoodTagStatus.Active; entity.IsSystem = true; entity.DisplayOrder = value.DisplayOrder;
        entity.IsSelectable = value.IsSelectable; entity.IsPreferenceSelectable = value.IsPreferenceSelectable;
        entity.IsAutoAssigned = value.IsAutoAssigned; entity.IsDeleted = false; entity.UpdatedAt = now;
        return true;
    }
}

public sealed record SeedResult(int CategoriesInserted, int CategoriesUpdated, int CategoriesSkipped,
    int TagsInserted, int TagsUpdated, int TagsSkipped, int LegacyTagsDeactivated);

using ApplicationLayer.Services.Menus;
using DomainLayer.Entities;
using DomainLayer.Enums;
using InfrastructureLayer.Data.Migrations;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Data;

public sealed class LegacyFoodTagMetadataAdapter(SNMDbContext db) : ILegacyFoodTagMetadataAdapter
{
    public async Task ApplySupportedAsync(FoodItem food, IReadOnlyCollection<FoodTag> tags, DateTime utcNow, CancellationToken ct = default)
    {
        var mappings = FoodTagMigrationMapCatalog.Entries.ToDictionary(x => x.LegacyTagCode, StringComparer.Ordinal);
        foreach (var tag in tags.OrderBy(x => x.Code, StringComparer.Ordinal))
        {
            if (tag.Code is "SOUP" or "OTHER_QUICK_SERVE") continue;
            if (tag.Code is "DRINK" or "DESSERT")
            {
                AddCourse(food, tag.Code == "DRINK" ? FoodCourse.DRINK : FoodCourse.DESSERT, utcNow);
                continue;
            }
            if (!mappings.TryGetValue(tag.Code, out var mapping) || mapping.MigrationDisposition != FoodTagMigrationDisposition.MIGRATE || mapping.TargetCode is null) continue;
            switch (mapping.TargetType)
            {
                case "FoodCourse": AddCourse(food, Enum.Parse<FoodCourse>(mapping.TargetCode.Replace("COURSE_", "", StringComparison.Ordinal)), utcNow); break;
                case "Ingredient":
                    var ingredient = await db.Ingredients.SingleOrDefaultAsync(x => x.Code == mapping.TargetCode && x.IsActive, ct);
                    if (ingredient is not null && food.Ingredients.All(x => x.IngredientId != ingredient.Id)) food.Ingredients.Add(new() { FoodItemId = food.Id, IngredientId = ingredient.Id, Ingredient = ingredient, CreatedAt = utcNow });
                    break;
                case "PreparationMethod":
                    var preparation = await db.PreparationMethods.SingleOrDefaultAsync(x => x.Code == mapping.TargetCode && x.IsActive, ct);
                    if (preparation is not null && food.PreparationMethods.All(x => x.PreparationMethodId != preparation.Id)) food.PreparationMethods.Add(new() { FoodItemId = food.Id, PreparationMethodId = preparation.Id, PreparationMethod = preparation, CreatedAt = utcNow });
                    break;
                case "TasteProfile":
                    var taste = await db.TasteProfiles.SingleOrDefaultAsync(x => x.Code == mapping.TargetCode && x.IsActive, ct);
                    if (taste is not null && food.TasteProfiles.All(x => x.TasteProfileId != taste.Id)) food.TasteProfiles.Add(new() { FoodItemId = food.Id, TasteProfileId = taste.Id, TasteProfile = taste, CreatedAt = utcNow });
                    break;
                case "DietaryRestriction":
                    var dietary = await db.DietaryAttributes.SingleOrDefaultAsync(x => x.Code == mapping.TargetCode && x.IsActive, ct);
                    if (dietary is not null && food.DietaryAttributes.All(x => x.DietaryAttributeId != dietary.Id)) food.DietaryAttributes.Add(new() { FoodItemId = food.Id, DietaryAttributeId = dietary.Id, DietaryAttribute = dietary, SuitabilityStatus = DietarySuitabilityStatus.UNVERIFIED, IsConfirmed = false, Source = MetadataSource.SYSTEM_MIGRATED, CreatedAt = utcNow, UpdatedAt = utcNow });
                    break;
                case "SpiceLevel": food.SpiceLevel = ParseSpice(mapping.TargetCode); break;
                case "ServingTemperature": food.ServingTemperature = ParseTemperature(mapping.TargetCode); break;
                case "ServingProfile": food.IsShareable = true; break;
            }
        }
    }

    private static void AddCourse(FoodItem food, FoodCourse course, DateTime now)
    {
        if (food.Courses.Any(x => x.Course == course)) return;
        food.Courses.Add(new() { FoodItemId = food.Id, Course = course, IsPrimary = food.Courses.Count == 0, CreatedAt = now });
    }

    private static FoodSpiceLevel ParseSpice(string code) => code switch
    {
        "TASTE_MILD_SPICY" => FoodSpiceLevel.MILD, "TASTE_SPICY" => FoodSpiceLevel.SPICY,
        "TASTE_VERY_SPICY" => FoodSpiceLevel.VERY_SPICY, _ => FoodSpiceLevel.UNKNOWN
    };
    private static ServingTemperature ParseTemperature(string code) => code switch
    {
        "TEMP_HOT" => ServingTemperature.HOT, "TEMP_COLD" => ServingTemperature.COLD,
        "TEMP_ROOM" => ServingTemperature.ROOM, _ => ServingTemperature.UNKNOWN
    };
}

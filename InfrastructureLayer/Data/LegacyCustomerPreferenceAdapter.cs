using ApplicationLayer.AI.DTOs;
using ApplicationLayer.AI.Services;
using DomainLayer.Entities;
using DomainLayer.Enums;
using InfrastructureLayer.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Data;

public sealed class LegacyCustomerPreferenceAdapter(SNMDbContext db) : ILegacyCustomerPreferenceAdapter
{
    public async Task<CustomerPreferenceResponse?> GetDerivedAsync(Guid customerId, CancellationToken ct = default)
    {
        var profile = await LoadProfile(customerId, false, ct);
        if (profile is null) return null;
        var tags = await db.FoodTags.AsNoTracking().Where(x => !x.IsDeleted && x.Status == FoodTagStatus.Active).ToListAsync(ct);
        var reverse = FoodTagMigrationMapCatalog.Entries.Where(x => x.TargetCode is not null)
            .GroupBy(x => x.TargetCode!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Select(m => tags.SingleOrDefault(t => t.Code == m.LegacyTagCode)).FirstOrDefault(t => t is not null), StringComparer.Ordinal);
        var liked = new List<FoodTag>(); var avoided = new List<FoodTag>();
        Add(liked, profile.PreferredIngredients.Select(x => x.Ingredient.Code)); Add(avoided, profile.AvoidedIngredients.Select(x => x.Ingredient.Code));
        Add(liked, profile.DietaryRequirements.Select(x => x.DietaryAttribute.Code)); Add(liked, profile.PreferredPreparationMethods.Select(x => x.PreparationMethod.Code));
        Add(liked, profile.PreferredTasteProfiles.Select(x => x.TasteProfile.Code)); Add(avoided, profile.AvoidedTasteProfiles.Select(x => x.TasteProfile.Code));
        foreach (var course in profile.PreferredCourses.Select(x => x.Course))
        { var tag = tags.SingleOrDefault(x => x.Code == (course is FoodCourse.DRINK or FoodCourse.DESSERT ? course.ToString() : $"COURSE_{course}")); if (tag is not null) liked.Add(tag); }
        return new() { LikedTags = liked.DistinctBy(x => x.Id).OrderBy(x => x.Code).Select(Map).ToArray(), AvoidTags = avoided.DistinctBy(x => x.Id).OrderBy(x => x.Code).Select(Map).ToArray() };
        void Add(List<FoodTag> destination, IEnumerable<string> codes) { foreach (var code in codes) if (reverse.TryGetValue(code, out var tag) && tag is not null) destination.Add(tag); }
    }

    public async Task AddSupportedAsync(Guid customerId, IReadOnlyCollection<CustomerPreference> preferences, CancellationToken ct = default)
    {
        var profile = await LoadProfile(customerId, true, ct); var now = DateTime.UtcNow;
        if (profile is null) { profile = new() { CustomerId = customerId, CreatedAt = now }; db.CustomerFoodProfiles.Add(profile); }
        profile.UpdatedAt = now;
        var mappings = FoodTagMigrationMapCatalog.Entries.ToDictionary(x => x.LegacyTagCode, StringComparer.Ordinal);
        foreach (var preference in preferences)
        {
            if (preference.PreferenceKind == CustomerPreferenceKind.Like && preference.FoodTag.Code is ("DRINK" or "DESSERT"))
            { var course = preference.FoodTag.Code == "DRINK" ? FoodCourse.DRINK : FoodCourse.DESSERT; if (profile.PreferredCourses.All(x => x.Course != course)) profile.PreferredCourses.Add(new() { CustomerId = customerId, Course = course, CreatedAt = now }); continue; }
            if (!mappings.TryGetValue(preference.FoodTag.Code, out var mapping) || mapping.MigrationDisposition != FoodTagMigrationDisposition.MIGRATE || mapping.TargetCode is null) continue;
            if (mapping.TargetType == "Ingredient")
            {
                var value = await db.Ingredients.SingleOrDefaultAsync(x => x.Code == mapping.TargetCode && x.IsActive, ct); if (value is null) continue;
                if (preference.PreferenceKind == CustomerPreferenceKind.Avoid && profile.AvoidedIngredients.All(x => x.IngredientId != value.Id)) profile.AvoidedIngredients.Add(new() { CustomerId = customerId, IngredientId = value.Id, Ingredient = value, CreatedAt = now });
                if (preference.PreferenceKind == CustomerPreferenceKind.Like && profile.PreferredIngredients.All(x => x.IngredientId != value.Id) && profile.AvoidedIngredients.All(x => x.IngredientId != value.Id)) profile.PreferredIngredients.Add(new() { CustomerId = customerId, IngredientId = value.Id, Ingredient = value, CreatedAt = now });
            }
            else if (mapping.TargetType == "DietaryRestriction" && mapping.IsHardConstraint && preference.PreferenceKind == CustomerPreferenceKind.Like)
            { var value = await db.DietaryAttributes.SingleOrDefaultAsync(x => x.Code == mapping.TargetCode && x.IsActive, ct); if (value is not null && profile.DietaryRequirements.All(x => x.DietaryAttributeId != value.Id)) profile.DietaryRequirements.Add(new() { CustomerId = customerId, DietaryAttributeId = value.Id, DietaryAttribute = value, CreatedAt = now }); }
            else if (mapping.TargetType == "PreparationMethod" && preference.PreferenceKind == CustomerPreferenceKind.Like)
            { var value = await db.PreparationMethods.SingleOrDefaultAsync(x => x.Code == mapping.TargetCode && x.IsActive, ct); if (value is not null && profile.PreferredPreparationMethods.All(x => x.PreparationMethodId != value.Id)) profile.PreferredPreparationMethods.Add(new() { CustomerId = customerId, PreparationMethodId = value.Id, PreparationMethod = value, CreatedAt = now }); }
            else if (mapping.TargetType == "TasteProfile")
            {
                var value = await db.TasteProfiles.SingleOrDefaultAsync(x => x.Code == mapping.TargetCode && x.IsActive, ct); if (value is null) continue;
                if (preference.PreferenceKind == CustomerPreferenceKind.Avoid && profile.AvoidedTasteProfiles.All(x => x.TasteProfileId != value.Id)) profile.AvoidedTasteProfiles.Add(new() { CustomerId = customerId, TasteProfileId = value.Id, TasteProfile = value, CreatedAt = now });
                if (preference.PreferenceKind == CustomerPreferenceKind.Like && profile.PreferredTasteProfiles.All(x => x.TasteProfileId != value.Id) && profile.AvoidedTasteProfiles.All(x => x.TasteProfileId != value.Id)) profile.PreferredTasteProfiles.Add(new() { CustomerId = customerId, TasteProfileId = value.Id, TasteProfile = value, CreatedAt = now });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private Task<CustomerFoodProfile?> LoadProfile(Guid customerId, bool tracking, CancellationToken ct)
    {
        IQueryable<CustomerFoodProfile> query = db.CustomerFoodProfiles.Include(x => x.PreferredIngredients).ThenInclude(x => x.Ingredient).Include(x => x.AvoidedIngredients).ThenInclude(x => x.Ingredient)
            .Include(x => x.DietaryRequirements).ThenInclude(x => x.DietaryAttribute).Include(x => x.PreferredPreparationMethods).ThenInclude(x => x.PreparationMethod)
            .Include(x => x.PreferredTasteProfiles).ThenInclude(x => x.TasteProfile).Include(x => x.AvoidedTasteProfiles).ThenInclude(x => x.TasteProfile).Include(x => x.PreferredCourses);
        if (!tracking) query = query.AsNoTracking(); return query.AsSplitQuery().SingleOrDefaultAsync(x => x.CustomerId == customerId, ct);
    }
    private static CustomerPreferenceTagResponse Map(FoodTag tag) => new() { FoodTagId = tag.Id, Code = tag.Code, DisplayName = string.IsNullOrWhiteSpace(tag.Description) ? tag.Name : tag.Description };
}

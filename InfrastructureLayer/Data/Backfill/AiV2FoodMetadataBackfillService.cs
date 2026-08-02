using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DomainLayer.Entities;
using DomainLayer.Enums;
using InfrastructureLayer.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Data.Backfill;

public sealed class AiV2FoodMetadataBackfillService(SNMDbContext db)
{
    public async Task<AiV2BackfillReport> RunAsync(AiV2BackfillOptions options, CancellationToken cancellationToken = default)
    {
        options.Validate();
        var legacyBefore = await LegacyCounts(cancellationToken);
        var specialRelations = await SpecialRelationCounts(cancellationToken);
        var plan = await BuildPlanAsync(cancellationToken);
        var committed = false;

        if (options.Mode == AiV2BackfillMode.Execute)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await ExecuteAsync(plan, options.BatchSize, cancellationToken);
                var legacyAfter = await LegacyCounts(cancellationToken);
                if (legacyBefore != legacyAfter)
                    throw new InvalidOperationException("Legacy tag/relation counts changed during additive backfill.");
                await transaction.CommitAsync(cancellationToken);
                committed = true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        var finalCounts = options.Mode == AiV2BackfillMode.Execute
            ? await FinalNormalizedCounts(cancellationToken)
            : plan.PredictedFinalCounts();
        var dataQuality = await BuildDataQuality(plan, options.Mode, cancellationToken);
        return new AiV2BackfillReport(
            options.Mode,
            options.BatchSize,
            DateTime.UtcNow,
            legacyBefore.Tags,
            legacyBefore.SystemTags,
            legacyBefore.Tags - legacyBefore.SystemTags,
            legacyBefore.ActiveTags,
            legacyBefore.Tags - legacyBefore.ActiveTags,
            legacyBefore.FoodRelations,
            legacyBefore.PreferenceRelations,
            specialRelations,
            FoodTagMigrationMapCatalog.Entries.GroupBy(value => value.MigrationDisposition.ToString()).ToDictionary(group => group.Key, group => group.Count()),
            plan.RelationDispositionCounts,
            plan.RelationDispositionCounts.Values.Sum(),
            plan.TargetCounts,
            plan.PlannedWrites(),
            finalCounts,
            plan.Exceptions.Count(value => value.Code == "UNMAPPED_TAG"),
            plan.Exceptions.Count(value => value.Code.Contains("MANUAL", StringComparison.Ordinal) || value.Code.Contains("UNSUPPORTED", StringComparison.Ordinal)),
            plan.Exceptions.OrderBy(value => value.Code).ThenBy(value => value.RelationId).ToArray(),
            plan.SoupResolutions.OrderBy(value => value.FoodName).ToArray(),
            dataQuality,
            false,
            committed);
    }

    public static string BuildSearchText(IEnumerable<string?> values)
    {
        return string.Join(" | ", values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Regex.Replace(value!.Normalize(NormalizationForm.FormKC).Trim(), @"\s+", " ").ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal));
    }

    public static string ComputeContentHash(string searchText)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(searchText)));

    private async Task<BackfillPlan> BuildPlanAsync(CancellationToken ct)
    {
        var plan = new BackfillPlan();
        await BuildCatalogs(plan, ct);

        var foodLinks = await db.FoodItemTags.AsNoTracking()
            .Include(value => value.FoodTag)
            .Include(value => value.FoodItem).ThenInclude(value => value.Category)
            .OrderBy(value => value.FoodItemId).ThenBy(value => value.FoodTag.Code)
            .ToListAsync(ct);
        var preferences = await db.CustomerPreferences.AsNoTracking()
            .Include(value => value.FoodTag)
            .OrderBy(value => value.CustomerId).ThenBy(value => value.FoodTag.Code)
            .ToListAsync(ct);
        var foods = await db.FoodItems.AsNoTracking().Include(value => value.Category).OrderBy(value => value.Id).ToListAsync(ct);
        var mappings = FoodTagMigrationMapCatalog.Entries.ToDictionary(value => value.LegacyTagCode, StringComparer.Ordinal);

        var ingredientKeys = (await db.FoodItemIngredients.AsNoTracking().Select(value => new { value.FoodItemId, value.IngredientId }).ToListAsync(ct)).Select(value => (value.FoodItemId, value.IngredientId)).ToHashSet();
        var dietaryKeys = (await db.FoodItemDietaryAttributes.AsNoTracking().Select(value => new { value.FoodItemId, value.DietaryAttributeId }).ToListAsync(ct)).Select(value => (value.FoodItemId, value.DietaryAttributeId)).ToHashSet();
        var preparationKeys = (await db.FoodItemPreparationMethods.AsNoTracking().Select(value => new { value.FoodItemId, value.PreparationMethodId }).ToListAsync(ct)).Select(value => (value.FoodItemId, value.PreparationMethodId)).ToHashSet();
        var tasteKeys = (await db.FoodItemTasteProfiles.AsNoTracking().Select(value => new { value.FoodItemId, value.TasteProfileId }).ToListAsync(ct)).Select(value => (value.FoodItemId, value.TasteProfileId)).ToHashSet();
        var facetKeys = (await db.FoodItemSearchFacets.AsNoTracking().Select(value => new { value.FoodItemId, value.FoodSearchFacetId }).ToListAsync(ct)).Select(value => (value.FoodItemId, value.FoodSearchFacetId)).ToHashSet();
        var courseKeys = (await db.FoodItemCourses.AsNoTracking().Select(value => new { value.FoodItemId, value.Course }).ToListAsync(ct)).Select(value => (value.FoodItemId, value.Course)).ToHashSet();
        var purposeKeys = (await db.FoodItemDiningPurposes.AsNoTracking().Select(value => new { value.FoodItemId, value.Purpose }).ToListAsync(ct)).Select(value => (value.FoodItemId, value.Purpose)).ToHashSet();
        plan.ExistingCounts["FoodItemIngredient"] = ingredientKeys.Count;
        plan.ExistingCounts["FoodItemDietaryAttribute"] = dietaryKeys.Count;
        plan.ExistingCounts["FoodItemPreparationMethod"] = preparationKeys.Count;
        plan.ExistingCounts["FoodItemTasteProfile"] = tasteKeys.Count;
        plan.ExistingCounts["FoodItemSearchFacet"] = facetKeys.Count;
        plan.ExistingCounts["FoodItemCourse"] = courseKeys.Count;
        plan.ExistingCounts["FoodItemDiningPurpose"] = purposeKeys.Count;

        var desiredCourses = new Dictionary<Guid, HashSet<FoodCourse>>();
        var desiredMethods = new Dictionary<Guid, HashSet<Guid>>();
        var semanticTokens = foods.ToDictionary(value => value.Id, value => new List<string?> { value.Name, value.Description, value.Category.Code, value.Category.Name });
        var scalarCandidates = new Dictionary<Guid, FoodScalarCandidates>();

        foreach (var link in foodLinks)
        {
            if (!mappings.TryGetValue(link.FoodTag.Code, out var mapping))
            {
                plan.IncrementDisposition(FoodTagMigrationDisposition.REJECT_WITH_REASON);
                plan.Exceptions.Add(new("UNMAPPED_TAG", "FoodItemTag", link.FoodItemId, $"No reviewed mapping for code {link.FoodTag.Code}."));
                continue;
            }

            if (link.FoodTag.Code == "SOUP")
            {
                plan.IncrementDisposition(FoodTagMigrationDisposition.AMBIGUOUS_REQUIRES_MANUAL_REVIEW);
                var resolution = SoupMigrationResolutionCatalog.Find(link.FoodItemId) ?? new SoupMigrationResolution(
                    link.FoodItemId, link.FoodItem.Name, link.FoodItem.Category.Code,
                    link.FoodItem.FoodItemTags.Select(value => value.FoodTag.Code).Order().ToArray(), null, null, null, 0,
                    SoupResolutionStatus.MANUAL_REVIEW, "Row was not part of the reviewed 12-row dataset.", "Retain legacy relation and review explicitly.");
                plan.SoupResolutions.Add(resolution);
                if (resolution.ResolutionStatus != SoupResolutionStatus.AUTO_APPROVED)
                    plan.Exceptions.Add(new(resolution.ResolutionStatus == SoupResolutionStatus.MANUAL_REVIEW ? "SOUP_MANUAL_REVIEW" : "SOUP_NOT_APPLICABLE", "FoodItemTag", link.FoodItemId, resolution.Evidence));
                continue;
            }
            if (link.FoodTag.Code is "DRINK" or "DESSERT")
            {
                plan.IncrementDisposition(FoodTagMigrationDisposition.MIGRATE);
                var specialCourse = link.FoodTag.Code == "DRINK" ? FoodCourse.DRINK : FoodCourse.DESSERT;
                AddDesired(desiredCourses, link.FoodItemId, specialCourse);
                semanticTokens[link.FoodItemId].Add(specialCourse.ToString());
                plan.IncrementTarget("Course");
                continue;
            }
            plan.IncrementDisposition(mapping.MigrationDisposition);
            if (mapping.MigrationDisposition is FoodTagMigrationDisposition.DERIVE or FoodTagMigrationDisposition.ARCHIVE)
                continue;
            if (mapping.MigrationDisposition != FoodTagMigrationDisposition.MIGRATE || mapping.TargetCode is null)
            {
                plan.Exceptions.Add(new("FOOD_MANUAL_REVIEW", "FoodItemTag", link.FoodItemId, mapping.Notes));
                continue;
            }

            semanticTokens[link.FoodItemId].Add(mapping.TargetCode);
            switch (mapping.TargetType)
            {
                case "Ingredient":
                    var ingredientId = plan.IngredientIds[mapping.TargetCode];
                    if (ingredientKeys.Add((link.FoodItemId, ingredientId))) plan.FoodIngredients.Add(new() { FoodItemId = link.FoodItemId, IngredientId = ingredientId, CreatedAt = link.CreatedAt });
                    plan.IncrementTarget("Ingredient");
                    break;
                case "DietaryRestriction":
                    var dietaryId = plan.DietaryIds[mapping.TargetCode];
                    if (dietaryKeys.Add((link.FoodItemId, dietaryId))) plan.FoodDietary.Add(new() { FoodItemId = link.FoodItemId, DietaryAttributeId = dietaryId, SuitabilityStatus = DietarySuitabilityStatus.UNVERIFIED, IsConfirmed = false, Source = MetadataSource.SYSTEM_MIGRATED, CreatedAt = link.CreatedAt, UpdatedAt = link.CreatedAt });
                    plan.IncrementTarget("Dietary");
                    break;
                case "PreparationMethod":
                    var methodId = plan.PreparationIds[mapping.TargetCode];
                    AddDesired(desiredMethods, link.FoodItemId, methodId);
                    plan.IncrementTarget("Preparation");
                    break;
                case "TasteProfile":
                    var tasteId = plan.TasteIds[mapping.TargetCode];
                    if (tasteKeys.Add((link.FoodItemId, tasteId))) plan.FoodTastes.Add(new() { FoodItemId = link.FoodItemId, TasteProfileId = tasteId, Intensity = null, CreatedAt = link.CreatedAt });
                    plan.IncrementTarget("Taste");
                    break;
                case "FoodCourse":
                    AddDesired(desiredCourses, link.FoodItemId, ParseCourse(mapping.TargetCode));
                    plan.IncrementTarget("Course");
                    break;
                case "DiningPurpose":
                    var purpose = ParsePurpose(mapping.TargetCode);
                    if (purposeKeys.Add((link.FoodItemId, purpose))) plan.FoodPurposes.Add(new() { FoodItemId = link.FoodItemId, Purpose = purpose, CreatedAt = link.CreatedAt });
                    plan.IncrementTarget("Purpose/customer facet");
                    break;
                case "SearchFacet":
                    var facetId = plan.FacetIds[mapping.TargetCode];
                    if (facetKeys.Add((link.FoodItemId, facetId))) plan.FoodFacets.Add(new() { FoodItemId = link.FoodItemId, FoodSearchFacetId = facetId, CreatedAt = link.CreatedAt });
                    plan.IncrementTarget("SearchFacet");
                    break;
                case "SpiceLevel":
                    Candidates(scalarCandidates, link.FoodItemId).SpiceLevels.Add(ParseSpice(mapping.TargetCode));
                    plan.IncrementTarget("Spice");
                    break;
                case "ServingTemperature":
                    Candidates(scalarCandidates, link.FoodItemId).Temperatures.Add(ParseTemperature(mapping.TargetCode));
                    plan.IncrementTarget("Temperature");
                    break;
                case "ServingProfile":
                    Candidates(scalarCandidates, link.FoodItemId).Shareable = true;
                    plan.IncrementTarget("Serving");
                    break;
            }
        }

        var existingPrimaryCourses = (await db.FoodItemCourses.AsNoTracking().Where(value => value.IsPrimary).Select(value => value.FoodItemId).ToListAsync(ct)).ToHashSet();
        foreach (var (foodId, courses) in desiredCourses)
            foreach (var course in courses.Order())
                if (courseKeys.Add((foodId, course))) plan.FoodCourses.Add(new() { FoodItemId = foodId, Course = course, IsPrimary = !existingPrimaryCourses.Contains(foodId) && courses.Count == 1, CreatedAt = DateTime.UtcNow });
        var existingPrimaryMethods = (await db.FoodItemPreparationMethods.AsNoTracking().Where(value => value.IsPrimary).Select(value => value.FoodItemId).ToListAsync(ct)).ToHashSet();
        foreach (var (foodId, methods) in desiredMethods)
            foreach (var methodId in methods.Order())
                if (preparationKeys.Add((foodId, methodId))) plan.FoodPreparations.Add(new() { FoodItemId = foodId, PreparationMethodId = methodId, IsPrimary = !existingPrimaryMethods.Contains(foodId) && methods.Count == 1, CreatedAt = DateTime.UtcNow });

        var foodsById = foods.ToDictionary(value => value.Id);
        foreach (var (foodId, candidates) in scalarCandidates)
        {
            if (candidates.SpiceLevels.Count > 1 || candidates.Temperatures.Count > 1)
                plan.ContradictoryFoods.Add(foodId);
            var update = new FoodScalarUpdate(
                candidates.SpiceLevels.Count == 1 ? candidates.SpiceLevels.Single() : null,
                candidates.Temperatures.Count == 1 ? candidates.Temperatures.Single() : null,
                candidates.Shareable);
            var food = foodsById[foodId];
            if ((update.SpiceLevel.HasValue && food.SpiceLevel == FoodSpiceLevel.UNKNOWN)
                || (update.Temperature.HasValue && food.ServingTemperature is null or ServingTemperature.UNKNOWN)
                || (update.Shareable.HasValue && food.IsShareable is null))
                plan.FoodScalarUpdates[foodId] = update;
        }

        await BuildCustomerPlan(plan, preferences, mappings, ct);
        await BuildProfiles(plan, foods, semanticTokens, ct);
        return plan;
    }

    private async Task BuildCatalogs(BackfillPlan plan, CancellationToken ct)
    {
        var migrate = FoodTagMigrationMapCatalog.Entries.Where(value => value.MigrationDisposition == FoodTagMigrationDisposition.MIGRATE && value.TargetCode is not null).ToArray();
        await Catalog(migrate.Where(value => value.TargetType == "Ingredient"), db.Ingredients, plan.Ingredients, plan.IngredientIds, (id, code, name) => new Ingredient { Id = id, Code = code, Name = name, NormalizedName = NormalizeCatalogName(name), IsSystem = true, IsActive = true });
        await Catalog(migrate.Where(value => value.TargetType == "DietaryRestriction"), db.DietaryAttributes, plan.DietaryAttributes, plan.DietaryIds, (id, code, name) => new DietaryAttribute { Id = id, Code = code, Name = name, IsSystem = true, IsActive = true });
        await Catalog(migrate.Where(value => value.TargetType == "PreparationMethod"), db.PreparationMethods, plan.PreparationMethods, plan.PreparationIds, (id, code, name) => new PreparationMethod { Id = id, Code = code, Name = name, IsSystem = true, IsActive = true });
        await Catalog(migrate.Where(value => value.TargetType == "TasteProfile"), db.TasteProfiles, plan.TasteProfiles, plan.TasteIds, (id, code, name) => new TasteProfile { Id = id, Code = code, Name = name, IsSystem = true, IsActive = true });
        await Catalog(migrate.Where(value => value.TargetType == "SearchFacet"), db.FoodSearchFacets, plan.SearchFacets, plan.FacetIds, (id, code, name) => new FoodSearchFacet { Id = id, Code = code, Name = name, IsSystem = true, IsActive = true });
        plan.ExistingCounts["Ingredient"] = await db.Ingredients.CountAsync(ct);
        plan.ExistingCounts["DietaryAttribute"] = await db.DietaryAttributes.CountAsync(ct);
        plan.ExistingCounts["PreparationMethod"] = await db.PreparationMethods.CountAsync(ct);
        plan.ExistingCounts["TasteProfile"] = await db.TasteProfiles.CountAsync(ct);
        plan.ExistingCounts["FoodSearchFacet"] = await db.FoodSearchFacets.CountAsync(ct);
        plan.ExistingCounts["Allergen"] = await db.Allergens.CountAsync(ct);
        return;

        async Task Catalog<T>(IEnumerable<FoodTagMigrationMap> source, DbSet<T> set, List<T> additions, Dictionary<string, Guid> ids, Func<Guid, string, string, T> factory) where T : SemanticCatalogEntity
        {
            var existing = await set.AsNoTracking().ToDictionaryAsync(value => value.Code, value => value.Id, StringComparer.Ordinal, ct);
            foreach (var mapping in source.GroupBy(value => value.TargetCode!, StringComparer.Ordinal).Select(group => group.First()).OrderBy(value => value.TargetCode))
            {
                var code = mapping.TargetCode!;
                if (!existing.TryGetValue(code, out var id))
                {
                    id = StableId(typeof(T).Name, code);
                    additions.Add(factory(id, code, mapping.LegacyTagName));
                }
                ids[code] = id;
            }
        }
    }

    private async Task BuildCustomerPlan(BackfillPlan plan, IReadOnlyList<CustomerPreference> preferences, IReadOnlyDictionary<string, FoodTagMigrationMap> mappings, CancellationToken ct)
    {
        var profileIds = (await db.CustomerFoodProfiles.AsNoTracking().Select(value => value.CustomerId).ToListAsync(ct)).ToHashSet();
        plan.ExistingCounts["CustomerFoodProfile"] = profileIds.Count;
        var preferredIngredient = (await db.CustomerPreferredIngredients.AsNoTracking().Select(value => new { value.CustomerId, value.IngredientId }).ToListAsync(ct)).Select(value => (value.CustomerId, value.IngredientId)).ToHashSet();
        var avoidedIngredient = (await db.CustomerAvoidedIngredients.AsNoTracking().Select(value => new { value.CustomerId, value.IngredientId }).ToListAsync(ct)).Select(value => (value.CustomerId, value.IngredientId)).ToHashSet();
        var dietary = (await db.CustomerDietaryRequirements.AsNoTracking().Select(value => new { value.CustomerId, value.DietaryAttributeId }).ToListAsync(ct)).Select(value => (value.CustomerId, value.DietaryAttributeId)).ToHashSet();
        var preparation = (await db.CustomerPreferredPreparationMethods.AsNoTracking().Select(value => new { value.CustomerId, value.PreparationMethodId }).ToListAsync(ct)).Select(value => (value.CustomerId, value.PreparationMethodId)).ToHashSet();
        var preferredTaste = (await db.CustomerPreferredTasteProfiles.AsNoTracking().Select(value => new { value.CustomerId, value.TasteProfileId }).ToListAsync(ct)).Select(value => (value.CustomerId, value.TasteProfileId)).ToHashSet();
        var avoidedTaste = (await db.CustomerAvoidedTasteProfiles.AsNoTracking().Select(value => new { value.CustomerId, value.TasteProfileId }).ToListAsync(ct)).Select(value => (value.CustomerId, value.TasteProfileId)).ToHashSet();
        var existingSpice = await db.CustomerFoodProfiles.AsNoTracking().ToDictionaryAsync(value => value.CustomerId, value => value.PreferredSpiceLevel, ct);
        var courses = (await db.CustomerPreferredCourses.AsNoTracking().Select(value => new { value.CustomerId, value.Course }).ToListAsync(ct)).Select(value => (value.CustomerId, value.Course)).ToHashSet();
        var purposes = (await db.CustomerPreferredDiningPurposes.AsNoTracking().Select(value => new { value.CustomerId, value.Purpose }).ToListAsync(ct)).Select(value => (value.CustomerId, value.Purpose)).ToHashSet();
        plan.ExistingCounts["CustomerPreferredIngredient"] = preferredIngredient.Count;
        plan.ExistingCounts["CustomerAvoidedIngredient"] = avoidedIngredient.Count;
        plan.ExistingCounts["CustomerDietaryRequirement"] = dietary.Count;
        plan.ExistingCounts["CustomerPreferredPreparationMethod"] = preparation.Count;
        plan.ExistingCounts["CustomerPreferredTasteProfile"] = preferredTaste.Count;
        plan.ExistingCounts["CustomerAvoidedTasteProfile"] = avoidedTaste.Count;
        plan.ExistingCounts["CustomerPreferredCourse"] = courses.Count;
        plan.ExistingCounts["CustomerPreferredDiningPurpose"] = purposes.Count;

        foreach (var preference in preferences)
        {
            if (profileIds.Add(preference.CustomerId)) plan.CustomerProfiles.Add(new() { CustomerId = preference.CustomerId, CreatedAt = preference.CreatedAt, UpdatedAt = preference.UpdatedAt });
            if (!mappings.TryGetValue(preference.FoodTag.Code, out var mapping))
            {
                plan.IncrementDisposition(FoodTagMigrationDisposition.REJECT_WITH_REASON);
                plan.Exceptions.Add(new("UNMAPPED_TAG", "CustomerPreference", preference.Id, $"No reviewed mapping for code {preference.FoodTag.Code}."));
                continue;
            }
            if (preference.FoodTag.Code is "DRINK" or "DESSERT")
            {
                plan.IncrementDisposition(FoodTagMigrationDisposition.MIGRATE);
                if (preference.PreferenceKind == CustomerPreferenceKind.Like)
                {
                    var course = preference.FoodTag.Code == "DRINK" ? FoodCourse.DRINK : FoodCourse.DESSERT;
                    if (courses.Add((preference.CustomerId, course))) plan.CustomerCourses.Add(new() { CustomerId = preference.CustomerId, Course = course, CreatedAt = preference.CreatedAt });
                    plan.IncrementTarget("Purpose/customer facet");
                }
                else plan.Exceptions.Add(new("UNSUPPORTED_AVOIDED_COURSE", "CustomerPreference", preference.Id, "Legacy avoid-course remains available through dual-read; no positive preference is fabricated."));
                continue;
            }
            plan.IncrementDisposition(mapping.MigrationDisposition);
            if (mapping.MigrationDisposition is FoodTagMigrationDisposition.DERIVE or FoodTagMigrationDisposition.ARCHIVE) continue;
            if (mapping.MigrationDisposition != FoodTagMigrationDisposition.MIGRATE || mapping.TargetCode is null)
            {
                plan.Exceptions.Add(new("CUSTOMER_MANUAL_REVIEW", "CustomerPreference", preference.Id, mapping.Notes));
                continue;
            }

            switch (mapping.TargetType)
            {
                case "Ingredient":
                    var ingredientId = plan.IngredientIds[mapping.TargetCode];
                    if (preference.PreferenceKind == CustomerPreferenceKind.Like && preferredIngredient.Add((preference.CustomerId, ingredientId))) plan.CustomerPreferredIngredients.Add(new() { CustomerId = preference.CustomerId, IngredientId = ingredientId, CreatedAt = preference.CreatedAt });
                    if (preference.PreferenceKind == CustomerPreferenceKind.Avoid && avoidedIngredient.Add((preference.CustomerId, ingredientId))) plan.CustomerAvoidedIngredients.Add(new() { CustomerId = preference.CustomerId, IngredientId = ingredientId, CreatedAt = preference.CreatedAt });
                    break;
                case "DietaryRestriction":
                    var dietaryId = plan.DietaryIds[mapping.TargetCode];
                    if (mapping.IsHardConstraint && preference.PreferenceKind == CustomerPreferenceKind.Like)
                    {
                        if (dietary.Add((preference.CustomerId, dietaryId))) plan.CustomerDietary.Add(new() { CustomerId = preference.CustomerId, DietaryAttributeId = dietaryId, CreatedAt = preference.CreatedAt });
                    }
                    else plan.Exceptions.Add(new("UNSUPPORTED_SOFT_DIETARY", "CustomerPreference", preference.Id, "Only explicit hard required diets migrate to CustomerDietaryRequirement; legacy relation is retained."));
                    break;
                case "PreparationMethod":
                    var methodId = plan.PreparationIds[mapping.TargetCode];
                    if (preference.PreferenceKind == CustomerPreferenceKind.Like && preparation.Add((preference.CustomerId, methodId))) plan.CustomerPreparations.Add(new() { CustomerId = preference.CustomerId, PreparationMethodId = methodId, CreatedAt = preference.CreatedAt });
                    else if (preference.PreferenceKind == CustomerPreferenceKind.Avoid) plan.Exceptions.Add(new("UNSUPPORTED_AVOIDED_PREPARATION", "CustomerPreference", preference.Id, "Avoided preparation has no approved normalized target."));
                    break;
                case "TasteProfile":
                    var tasteId = plan.TasteIds[mapping.TargetCode];
                    if (preference.PreferenceKind == CustomerPreferenceKind.Like && preferredTaste.Add((preference.CustomerId, tasteId))) plan.CustomerPreferredTastes.Add(new() { CustomerId = preference.CustomerId, TasteProfileId = tasteId, CreatedAt = preference.CreatedAt });
                    if (preference.PreferenceKind == CustomerPreferenceKind.Avoid && avoidedTaste.Add((preference.CustomerId, tasteId))) plan.CustomerAvoidedTastes.Add(new() { CustomerId = preference.CustomerId, TasteProfileId = tasteId, CreatedAt = preference.CreatedAt });
                    break;
                case "FoodCourse":
                    var course = ParseCourse(mapping.TargetCode);
                    if (preference.PreferenceKind == CustomerPreferenceKind.Like && courses.Add((preference.CustomerId, course))) plan.CustomerCourses.Add(new() { CustomerId = preference.CustomerId, Course = course, CreatedAt = preference.CreatedAt });
                    else if (preference.PreferenceKind == CustomerPreferenceKind.Avoid) plan.Exceptions.Add(new("UNSUPPORTED_AVOIDED_COURSE", "CustomerPreference", preference.Id, "Avoided course remains legacy-only."));
                    break;
                case "DiningPurpose":
                    var purpose = ParsePurpose(mapping.TargetCode);
                    if (preference.PreferenceKind == CustomerPreferenceKind.Like && purposes.Add((preference.CustomerId, purpose))) plan.CustomerPurposes.Add(new() { CustomerId = preference.CustomerId, Purpose = purpose, CreatedAt = preference.CreatedAt });
                    else if (preference.PreferenceKind == CustomerPreferenceKind.Avoid) plan.Exceptions.Add(new("UNSUPPORTED_AVOIDED_PURPOSE", "CustomerPreference", preference.Id, "Avoided purpose remains legacy-only."));
                    break;
                case "SpiceLevel":
                    if (preference.PreferenceKind == CustomerPreferenceKind.Like
                        && (!existingSpice.TryGetValue(preference.CustomerId, out var currentSpice) || currentSpice is null))
                        plan.CustomerSpiceUpdates[preference.CustomerId] = ParseSpice(mapping.TargetCode);
                    else plan.Exceptions.Add(new("UNSUPPORTED_AVOIDED_SPICE", "CustomerPreference", preference.Id, "Avoided spice is not converted into a positive preferred level."));
                    break;
                case "ServingProfile":
                    if (preference.PreferenceKind == CustomerPreferenceKind.Like && purposes.Add((preference.CustomerId, DiningPurpose.SHARING))) plan.CustomerPurposes.Add(new() { CustomerId = preference.CustomerId, Purpose = DiningPurpose.SHARING, CreatedAt = preference.CreatedAt });
                    break;
                case "SearchFacet":
                    plan.Exceptions.Add(new("UNSUPPORTED_CUSTOMER_SEARCH_FACET", "CustomerPreference", preference.Id, "No authoritative structured customer facet exists; retain legacy relation for dual-read."));
                    break;
            }
        }
        return;
    }

    private async Task BuildProfiles(BackfillPlan plan, IReadOnlyList<FoodItem> foods, IReadOnlyDictionary<Guid, List<string?>> semanticTokens, CancellationToken ct)
    {
        var existing = await db.FoodAiProfiles.AsNoTracking().ToDictionaryAsync(value => value.FoodItemId, ct);
        plan.ExistingCounts["FoodAiProfile"] = existing.Count;
        foreach (var food in foods)
        {
            var tokens = semanticTokens[food.Id];
            if (food.SpiceLevel != FoodSpiceLevel.UNKNOWN) tokens.Add(SpiceCode(food.SpiceLevel));
            if (food.ServingTemperature is not null and not ServingTemperature.UNKNOWN) tokens.Add(TemperatureCode(food.ServingTemperature.Value));
            if (food.IsShareable == true) tokens.Add("SHAREABLE");
            if (food.EstimatedServingCount.HasValue) tokens.Add($"serves {food.EstimatedServingCount.Value}");
            tokens.Add(food.ServingSizeDescription);
            var searchText = BuildSearchText(tokens);
            var hash = ComputeContentHash(searchText);
            if (!existing.TryGetValue(food.Id, out var profile))
                plan.ProfileAdds.Add(new() { FoodItemId = food.Id, SearchText = searchText, ContentHash = hash, Status = FoodAiProfileStatus.PENDING, Version = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            else if (profile.ContentHash != hash || profile.SearchText != searchText)
                plan.ProfileUpdates.Add((food.Id, searchText, hash));
        }
    }

    private async Task ExecuteAsync(BackfillPlan plan, int batchSize, CancellationToken ct)
    {
        db.AddRange(plan.Ingredients);
        db.AddRange(plan.DietaryAttributes);
        db.AddRange(plan.PreparationMethods);
        db.AddRange(plan.TasteProfiles);
        db.AddRange(plan.SearchFacets);
        await db.SaveChangesAsync(ct);

        var relations = new List<object>();
        relations.AddRange(plan.FoodIngredients); relations.AddRange(plan.FoodDietary); relations.AddRange(plan.FoodPreparations);
        relations.AddRange(plan.FoodTastes); relations.AddRange(plan.FoodFacets); relations.AddRange(plan.FoodCourses); relations.AddRange(plan.FoodPurposes);
        relations.AddRange(plan.CustomerProfiles); relations.AddRange(plan.CustomerPreferredIngredients); relations.AddRange(plan.CustomerAvoidedIngredients);
        relations.AddRange(plan.CustomerDietary); relations.AddRange(plan.CustomerPreparations); relations.AddRange(plan.CustomerPreferredTastes);
        relations.AddRange(plan.CustomerAvoidedTastes); relations.AddRange(plan.CustomerCourses); relations.AddRange(plan.CustomerPurposes);
        foreach (var batch in relations.Chunk(batchSize)) { db.AddRange(batch); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); }

        foreach (var batch in plan.FoodScalarUpdates.Chunk(batchSize))
        {
            var ids = batch.Select(value => value.Key).ToArray();
            var foods = await db.FoodItems.Where(value => ids.Contains(value.Id)).ToListAsync(ct);
            foreach (var food in foods)
            {
                var update = plan.FoodScalarUpdates[food.Id];
                if (update.SpiceLevel.HasValue && food.SpiceLevel == FoodSpiceLevel.UNKNOWN) food.SpiceLevel = update.SpiceLevel.Value;
                if (update.Temperature.HasValue && food.ServingTemperature is null or ServingTemperature.UNKNOWN) food.ServingTemperature = update.Temperature;
                if (update.Shareable.HasValue && food.IsShareable is null) food.IsShareable = update.Shareable;
                food.SemanticProfileVersion++;
                food.SemanticProfileUpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        }

        foreach (var batch in plan.CustomerSpiceUpdates.Chunk(batchSize))
        {
            var ids = batch.Select(value => value.Key).ToArray();
            var profiles = await db.CustomerFoodProfiles.Where(value => ids.Contains(value.CustomerId)).ToListAsync(ct);
            foreach (var profile in profiles) if (profile.PreferredSpiceLevel is null) profile.PreferredSpiceLevel = plan.CustomerSpiceUpdates[profile.CustomerId];
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        }

        foreach (var batch in plan.ProfileAdds.Chunk(batchSize)) { db.FoodAiProfiles.AddRange(batch); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); }
        foreach (var batch in plan.ProfileUpdates.Chunk(batchSize))
        {
            var ids = batch.Select(value => value.FoodId).ToArray();
            var profiles = await db.FoodAiProfiles.Where(value => ids.Contains(value.FoodItemId)).ToListAsync(ct);
            foreach (var profile in profiles)
            {
                var update = batch.Single(value => value.FoodId == profile.FoodItemId);
                profile.SearchText = update.SearchText; profile.ContentHash = update.Hash;
                profile.Status = profile.Status == FoodAiProfileStatus.DISABLED ? FoodAiProfileStatus.DISABLED : FoodAiProfileStatus.STALE;
                profile.Version++; profile.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        }
    }

    private async Task<AiV2DataQualityReport> BuildDataQuality(BackfillPlan plan, AiV2BackfillMode mode, CancellationToken ct)
    {
        var foodsWithoutCourse = mode == AiV2BackfillMode.Execute
            ? await db.FoodItems.CountAsync(food => !db.FoodItemCourses.Any(course => course.FoodItemId == food.Id), ct)
            : await db.FoodItems.CountAsync(ct) - (await db.FoodItemCourses.Select(value => value.FoodItemId).Distinct().CountAsync(ct) + plan.FoodCourses.Select(value => value.FoodItemId).Distinct().Count(id => !db.FoodItemCourses.Any(value => value.FoodItemId == id)));
        var hardSoft = plan.CustomerPreferredIngredients.Select(value => (value.CustomerId, value.IngredientId)).Intersect(plan.CustomerAvoidedIngredients.Select(value => (value.CustomerId, value.IngredientId))).Count()
                     + plan.CustomerPreferredTastes.Select(value => (value.CustomerId, value.TasteProfileId)).Intersect(plan.CustomerAvoidedTastes.Select(value => (value.CustomerId, value.TasteProfileId))).Count();
        return new(
            Math.Max(0, foodsWithoutCourse),
            await db.FoodItems.CountAsync(value => value.EstimatedServingCount == null, ct),
            await db.FoodItems.CountAsync(value => value.Description == null || value.Description == "", ct),
            await db.FoodItemCourses.Where(value => value.IsPrimary).GroupBy(value => value.FoodItemId).CountAsync(group => group.Count() > 1, ct),
            hardSoft,
            plan.ContradictoryFoods.Order().ToArray());
    }

    private async Task<LegacyCountSnapshot> LegacyCounts(CancellationToken ct)
        => new(await db.FoodTags.IgnoreQueryFilters().CountAsync(ct), await db.FoodTags.IgnoreQueryFilters().CountAsync(value => value.IsSystem, ct),
            await db.FoodTags.IgnoreQueryFilters().CountAsync(value => !value.IsDeleted && value.Status == FoodTagStatus.Active, ct),
            await db.FoodItemTags.CountAsync(ct), await db.CustomerPreferences.CountAsync(ct));

    private async Task<IReadOnlyDictionary<string, int>> SpecialRelationCounts(CancellationToken ct)
    {
        var codes = new[] { "DRINK", "DESSERT", "SOUP", "OTHER_QUICK_SERVE" };
        var food = await db.FoodItemTags.AsNoTracking().Where(x => codes.Contains(x.FoodTag.Code))
            .GroupBy(x => x.FoodTag.Code).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var customer = await db.CustomerPreferences.AsNoTracking().Where(x => codes.Contains(x.FoodTag.Code))
            .GroupBy(x => x.FoodTag.Code).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return codes.SelectMany(code => new[]
        {
            new KeyValuePair<string, int>($"{code}_FOOD", food.GetValueOrDefault(code)),
            new KeyValuePair<string, int>($"{code}_CUSTOMER", customer.GetValueOrDefault(code))
        }).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
    }

    private async Task<IReadOnlyDictionary<string, int>> FinalNormalizedCounts(CancellationToken ct) => new Dictionary<string, int>
    {
        ["Ingredient"] = await db.Ingredients.CountAsync(ct), ["DietaryAttribute"] = await db.DietaryAttributes.CountAsync(ct),
        ["PreparationMethod"] = await db.PreparationMethods.CountAsync(ct), ["TasteProfile"] = await db.TasteProfiles.CountAsync(ct),
        ["FoodSearchFacet"] = await db.FoodSearchFacets.CountAsync(ct), ["Allergen"] = await db.Allergens.CountAsync(ct),
        ["FoodItemIngredient"] = await db.FoodItemIngredients.CountAsync(ct), ["FoodItemDietaryAttribute"] = await db.FoodItemDietaryAttributes.CountAsync(ct),
        ["FoodItemPreparationMethod"] = await db.FoodItemPreparationMethods.CountAsync(ct), ["FoodItemTasteProfile"] = await db.FoodItemTasteProfiles.CountAsync(ct),
        ["FoodItemSearchFacet"] = await db.FoodItemSearchFacets.CountAsync(ct), ["FoodItemCourse"] = await db.FoodItemCourses.CountAsync(ct),
        ["FoodItemDiningPurpose"] = await db.FoodItemDiningPurposes.CountAsync(ct), ["CustomerFoodProfile"] = await db.CustomerFoodProfiles.CountAsync(ct),
        ["CustomerNormalizedRelations"] = await db.CustomerPreferredIngredients.CountAsync(ct) + await db.CustomerAvoidedIngredients.CountAsync(ct) + await db.CustomerDietaryRequirements.CountAsync(ct) + await db.CustomerPreferredPreparationMethods.CountAsync(ct) + await db.CustomerPreferredTasteProfiles.CountAsync(ct) + await db.CustomerAvoidedTasteProfiles.CountAsync(ct) + await db.CustomerPreferredCourses.CountAsync(ct) + await db.CustomerPreferredDiningPurposes.CountAsync(ct),
        ["FoodAiProfile"] = await db.FoodAiProfiles.CountAsync(ct)
    };

    private static string NormalizeCatalogName(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(value => CharUnicodeInfo.GetUnicodeCategory(value) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC).Trim().ToUpperInvariant();
    }
    private static Guid StableId(string target, string code) { var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"AI_V2:{target}:{code}")); return new Guid(bytes.AsSpan(0, 16)); }
    private static FoodCourse ParseCourse(string code) => Enum.Parse<FoodCourse>(code.Replace("COURSE_", "", StringComparison.Ordinal));
    private static DiningPurpose ParsePurpose(string code) => Enum.Parse<DiningPurpose>(code.Replace("PURPOSE_", "", StringComparison.Ordinal));
    private static FoodSpiceLevel ParseSpice(string code) => code switch { "TASTE_MILD_SPICY" => FoodSpiceLevel.MILD, "TASTE_SPICY" => FoodSpiceLevel.SPICY, "TASTE_VERY_SPICY" => FoodSpiceLevel.VERY_SPICY, _ => FoodSpiceLevel.UNKNOWN };
    private static ServingTemperature ParseTemperature(string code) => code switch { "TEMP_HOT" => ServingTemperature.HOT, "TEMP_COLD" => ServingTemperature.COLD, "TEMP_ROOM" => ServingTemperature.ROOM, _ => ServingTemperature.UNKNOWN };
    private static string SpiceCode(FoodSpiceLevel value) => value switch { FoodSpiceLevel.MILD => "TASTE_MILD_SPICY", FoodSpiceLevel.SPICY => "TASTE_SPICY", FoodSpiceLevel.VERY_SPICY => "TASTE_VERY_SPICY", FoodSpiceLevel.NON_SPICY => "DIET_NON_SPICY", _ => "" };
    private static string TemperatureCode(ServingTemperature value) => value switch { ServingTemperature.HOT => "TEMP_HOT", ServingTemperature.COLD => "TEMP_COLD", ServingTemperature.ROOM => "TEMP_ROOM", _ => "" };
    private static FoodScalarCandidates Candidates(Dictionary<Guid, FoodScalarCandidates> values, Guid id) { if (!values.TryGetValue(id, out var result)) values[id] = result = new(); return result; }
    private static void AddDesired<T>(Dictionary<Guid, HashSet<T>> values, Guid id, T value) where T : notnull { if (!values.TryGetValue(id, out var set)) values[id] = set = []; set.Add(value); }

    private sealed record LegacyCountSnapshot(int Tags, int SystemTags, int ActiveTags, int FoodRelations, int PreferenceRelations);
    private sealed class FoodScalarCandidates { public HashSet<FoodSpiceLevel> SpiceLevels { get; } = []; public HashSet<ServingTemperature> Temperatures { get; } = []; public bool? Shareable { get; set; } }
    private sealed record FoodScalarUpdate(FoodSpiceLevel? SpiceLevel, ServingTemperature? Temperature, bool? Shareable);

    private sealed class BackfillPlan
    {
        public List<Ingredient> Ingredients { get; } = []; public List<DietaryAttribute> DietaryAttributes { get; } = []; public List<PreparationMethod> PreparationMethods { get; } = []; public List<TasteProfile> TasteProfiles { get; } = []; public List<FoodSearchFacet> SearchFacets { get; } = [];
        public Dictionary<string, Guid> IngredientIds { get; } = []; public Dictionary<string, Guid> DietaryIds { get; } = []; public Dictionary<string, Guid> PreparationIds { get; } = []; public Dictionary<string, Guid> TasteIds { get; } = []; public Dictionary<string, Guid> FacetIds { get; } = [];
        public List<FoodItemIngredient> FoodIngredients { get; } = []; public List<FoodItemDietaryAttribute> FoodDietary { get; } = []; public List<FoodItemPreparationMethod> FoodPreparations { get; } = []; public List<FoodItemTasteProfile> FoodTastes { get; } = []; public List<FoodItemSearchFacet> FoodFacets { get; } = []; public List<FoodItemCourse> FoodCourses { get; } = []; public List<FoodItemDiningPurpose> FoodPurposes { get; } = [];
        public List<CustomerFoodProfile> CustomerProfiles { get; } = []; public List<CustomerPreferredIngredient> CustomerPreferredIngredients { get; } = []; public List<CustomerAvoidedIngredient> CustomerAvoidedIngredients { get; } = []; public List<CustomerDietaryRequirement> CustomerDietary { get; } = []; public List<CustomerPreferredPreparationMethod> CustomerPreparations { get; } = []; public List<CustomerPreferredTasteProfile> CustomerPreferredTastes { get; } = []; public List<CustomerAvoidedTasteProfile> CustomerAvoidedTastes { get; } = []; public List<CustomerPreferredCourse> CustomerCourses { get; } = []; public List<CustomerPreferredDiningPurpose> CustomerPurposes { get; } = [];
        public Dictionary<Guid, FoodScalarUpdate> FoodScalarUpdates { get; } = []; public Dictionary<Guid, FoodSpiceLevel> CustomerSpiceUpdates { get; } = [];
        public List<FoodAiProfile> ProfileAdds { get; } = []; public List<(Guid FoodId, string SearchText, string Hash)> ProfileUpdates { get; } = [];
        public List<AiV2BackfillException> Exceptions { get; } = []; public List<SoupMigrationResolution> SoupResolutions { get; } = []; public HashSet<Guid> ContradictoryFoods { get; } = [];
        public Dictionary<string, int> ExistingCounts { get; } = []; public Dictionary<string, int> TargetCounts { get; } = []; public Dictionary<string, int> RelationDispositionCounts { get; } = [];
        public void IncrementTarget(string key) => TargetCounts[key] = TargetCounts.GetValueOrDefault(key) + 1;
        public void IncrementDisposition(FoodTagMigrationDisposition disposition) => RelationDispositionCounts[disposition.ToString()] = RelationDispositionCounts.GetValueOrDefault(disposition.ToString()) + 1;
        public IReadOnlyDictionary<string, int> PlannedWrites() => new Dictionary<string, int>
        {
            ["Catalogs"] = Ingredients.Count + DietaryAttributes.Count + PreparationMethods.Count + TasteProfiles.Count + SearchFacets.Count,
            ["FoodMetadataRelations"] = FoodIngredients.Count + FoodDietary.Count + FoodPreparations.Count + FoodTastes.Count + FoodFacets.Count + FoodCourses.Count + FoodPurposes.Count,
            ["FoodScalarUpdates"] = FoodScalarUpdates.Count,
            ["CustomerProfiles"] = CustomerProfiles.Count,
            ["CustomerRelations"] = CustomerPreferredIngredients.Count + CustomerAvoidedIngredients.Count + CustomerDietary.Count + CustomerPreparations.Count + CustomerPreferredTastes.Count + CustomerAvoidedTastes.Count + CustomerCourses.Count + CustomerPurposes.Count,
            ["CustomerScalarUpdates"] = CustomerSpiceUpdates.Count,
            ["FoodAiProfileCreates"] = ProfileAdds.Count,
            ["FoodAiProfileUpdates"] = ProfileUpdates.Count
        };
        public IReadOnlyDictionary<string, int> PredictedFinalCounts()
        {
            var result = ExistingCounts.ToDictionary(value => value.Key, value => value.Value);
            void Add(string key, int count) => result[key] = result.GetValueOrDefault(key) + count;
            Add("Ingredient", Ingredients.Count); Add("DietaryAttribute", DietaryAttributes.Count); Add("PreparationMethod", PreparationMethods.Count); Add("TasteProfile", TasteProfiles.Count); Add("FoodSearchFacet", SearchFacets.Count);
            Add("FoodItemIngredient", FoodIngredients.Count); Add("FoodItemDietaryAttribute", FoodDietary.Count); Add("FoodItemPreparationMethod", FoodPreparations.Count); Add("FoodItemTasteProfile", FoodTastes.Count); Add("FoodItemSearchFacet", FoodFacets.Count); Add("FoodItemCourse", FoodCourses.Count); Add("FoodItemDiningPurpose", FoodPurposes.Count); Add("CustomerFoodProfile", CustomerProfiles.Count); Add("FoodAiProfile", ProfileAdds.Count);
            result["CustomerNormalizedRelations"] = CustomerPreferredIngredients.Count + CustomerAvoidedIngredients.Count + CustomerDietary.Count + CustomerPreparations.Count + CustomerPreferredTastes.Count + CustomerAvoidedTastes.Count + CustomerCourses.Count + CustomerPurposes.Count + ExistingCounts.Where(value => value.Key.StartsWith("Customer", StringComparison.Ordinal) && value.Key != "CustomerFoodProfile").Sum(value => value.Value);
            return result;
        }
    }
}

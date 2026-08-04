using InfrastructureLayer.Data.Migrations;
using InfrastructureLayer.Data.Seeders;
using DomainLayer.Enums;

namespace TestingLayer;

public class FoodTagMigrationMapTests
{
    [Fact]
    public void Catalog_CoversEveryReviewedSystemAndKnownNonSystemCodeExactlyOnce()
    {
        var entries = FoodTagMigrationMapCatalog.Entries;
        var systemCodes = SystemFoodTaxonomyCatalog.Tags.Select(tag => tag.Code).ToHashSet(StringComparer.Ordinal);
        var mappedSystemCodes = entries
            .Where(entry => systemCodes.Contains(entry.LegacyTagCode))
            .Select(entry => entry.LegacyTagCode)
            .ToList();

        Assert.Equal(102, systemCodes.Count);
        Assert.Equal(systemCodes.Order(), mappedSystemCodes.Order());
        Assert.Equal(34, FoodTagMigrationMapCatalog.KnownNonSystemCodes.Count);
        Assert.Equal(136, entries.Count);
        Assert.Equal(entries.Count, entries.Select(entry => entry.LegacyTagCode).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Catalog_KnownNonSystemSetMatchesTheReadOnlyDatabaseInspection()
    {
        var expected = new HashSet<string>(
        [
            "BEEF", "BUDGETFRIENDLY", "CAMBODIAN", "CHICKEN", "COLD", "DALATSPECIALTY", "DESSERT", "DRINK",
            "EGG", "FRIED", "FRUIT", "FULLMEAL", "GRILLED", "HOT", "JAPANESE", "KOREAN", "MIDRANGE", "MILD",
            "NOODLE", "NOSEAFOOD", "PORK", "PREMIUM", "RICE", "SEAFOOD", "SHAREABLE", "SNACK", "SOUP",
            "SPICY", "STREETFOOD", "SWEET", "THAI", "VEGETARIAN", "VIETNAMESE", "WESTERN"
        ], StringComparer.Ordinal);

        Assert.Equal(expected.Order(), FoodTagMigrationMapCatalog.KnownNonSystemCodes.Order());
    }

    [Fact]
    public void Catalog_NeverInfersAllergenDeclarationsFromDietaryTags()
    {
        var dietary = FoodTagMigrationMapCatalog.Entries
            .Where(entry => entry.LegacyTagCode.StartsWith("DIET_", StringComparison.Ordinal)
                            || entry.LegacyTagCode is "NOSEAFOOD" or "VEGETARIAN")
            .ToList();

        Assert.NotEmpty(dietary);
        Assert.All(dietary, entry => Assert.Equal("DietaryRestriction", entry.TargetType));
        Assert.DoesNotContain(FoodTagMigrationMapCatalog.Entries,
            entry => entry.TargetType.Equals("Allergen", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Catalog_BudgetMetadataIsAlwaysDerivedFromEffectivePrice()
    {
        var budgetEntries = FoodTagMigrationMapCatalog.Entries
            .Where(entry => entry.LegacyTagCode.StartsWith("BUDGET_", StringComparison.Ordinal)
                            || entry.LegacyTagCode is "BUDGETFRIENDLY" or "MIDRANGE" or "PREMIUM")
            .ToList();

        Assert.Equal(8, budgetEntries.Count);
        Assert.All(budgetEntries, entry =>
        {
            Assert.Equal(FoodTagMigrationDisposition.DERIVE, entry.MigrationDisposition);
            Assert.Equal("EffectivePrice", entry.TargetType);
            Assert.Null(entry.TargetCode);
        });
    }

    [Fact]
    public void Catalog_AutomaticMigrationsHaveTargetsAndManualReviewIsExplicit()
    {
        var automatic = FoodTagMigrationMapCatalog.Entries
            .Where(entry => entry.MigrationDisposition == FoodTagMigrationDisposition.MIGRATE);
        Assert.All(automatic, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.TargetType));
            Assert.False(string.IsNullOrWhiteSpace(entry.TargetCode));
        });

        var manualReviewCodes = FoodTagMigrationMapCatalog.Entries
            .Where(entry => entry.MigrationDisposition == FoodTagMigrationDisposition.AMBIGUOUS_REQUIRES_MANUAL_REVIEW)
            .Select(entry => entry.LegacyTagCode)
            .Order()
            .ToArray();

        Assert.Equal(new[] { "DESSERT", "DRINK", "SOUP" }, manualReviewCodes);
        Assert.All(FoodTagMigrationMapCatalog.Entries, entry =>
        {
            Assert.Equal(entry.LegacyTagCode.Trim().ToUpperInvariant(), entry.LegacyTagCode);
            Assert.False(string.IsNullOrWhiteSpace(entry.LegacyTagName));
            Assert.False(string.IsNullOrWhiteSpace(entry.Notes));
        });
    }

    [Fact]
    public void Catalog_DrinkAndDessertRequireRelationSpecificResolution()
    {
        var entries = FoodTagMigrationMapCatalog.Entries.ToDictionary(value => value.LegacyTagCode);
        Assert.Contains("FoodItemTag", entries["DRINK"].Notes);
        Assert.Contains("CustomerPreference", entries["DRINK"].Notes);
        Assert.Contains("FoodItemTag", entries["DESSERT"].Notes);
        Assert.Contains("CustomerPreference", entries["DESSERT"].Notes);
    }

    [Fact]
    public void Catalog_OtherQuickServeIsArchiveOnlyAndNeverSearchable()
    {
        var quickServe = FoodTagMigrationMapCatalog.Entries.Single(value => value.LegacyTagCode == "OTHER_QUICK_SERVE");
        Assert.Equal(FoodTagMigrationDisposition.ARCHIVE, quickServe.MigrationDisposition);
        Assert.False(quickServe.IsSearchable);
        Assert.Null(quickServe.TargetCode);
        Assert.DoesNotContain("time", quickServe.TargetType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SoupResolutionCatalog_CoversTheExactTwelveReviewedRowsWithoutUnsafeAutoMigration()
    {
        var rows = SoupMigrationResolutionCatalog.ReviewedRows;
        Assert.Equal(12, rows.Count);
        Assert.Equal(12, rows.Select(value => value.FoodItemId).Distinct().Count());
        Assert.Equal(2, rows.Count(value => value.ResolutionStatus == SoupResolutionStatus.MANUAL_REVIEW));
        Assert.Equal(10, rows.Count(value => value.ResolutionStatus == SoupResolutionStatus.NOT_APPLICABLE));
        Assert.DoesNotContain(rows, value => value.ResolutionStatus == SoupResolutionStatus.AUTO_APPROVED);
        Assert.All(rows, value =>
        {
            Assert.NotEqual(Guid.Empty, value.FoodItemId);
            Assert.NotEmpty(value.FoodName);
            Assert.NotEmpty(value.ExistingCategoryCode);
            Assert.Contains("SOUP", value.ExistingTagCodes);
            Assert.InRange(value.Confidence, 0, 1);
            Assert.NotEmpty(value.Evidence);
        });
    }

    [Fact]
    public void Catalog_AutomaticStableEnumTargetsExistInTheNewTaxonomy()
    {
        foreach (var mapping in FoodTagMigrationMapCatalog.Entries.Where(value => value.MigrationDisposition == FoodTagMigrationDisposition.MIGRATE && value.TargetCode is not null))
        {
            var target = mapping.TargetCode!;
            if (mapping.TargetType == "FoodCourse")
                Assert.True(Enum.TryParse<FoodCourse>(target.Replace("COURSE_", ""), out _), target);
            if (mapping.TargetType == "DiningPurpose")
                Assert.True(Enum.TryParse<DiningPurpose>(target.Replace("PURPOSE_", ""), out _), target);
            if (mapping.TargetType == "SpiceLevel")
                Assert.Contains(target, new[] { "TASTE_MILD_SPICY", "TASTE_SPICY", "TASTE_VERY_SPICY" });
            if (mapping.TargetType == "ServingTemperature")
                Assert.Contains(target, new[] { "TEMP_HOT", "TEMP_COLD", "TEMP_ROOM" });
        }
    }

    [Fact]
    public void Catalog_PreservesSystemCustomerSelectabilityMetadata()
    {
        var entries = FoodTagMigrationMapCatalog.Entries.ToDictionary(entry => entry.LegacyTagCode, StringComparer.Ordinal);

        foreach (var definition in SystemFoodTaxonomyCatalog.Tags)
            Assert.Equal(definition.IsPreferenceSelectable, entries[definition.Code].IsCustomerSelectable);
    }
}

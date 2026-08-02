using static InfrastructureLayer.Data.Seeders.SystemFoodTaxonomyCatalog;

namespace InfrastructureLayer.Data.Migrations;

public enum FoodTagMigrationDisposition
{
    MIGRATE,
    DERIVE,
    ARCHIVE,
    REJECT_WITH_REASON,
    AMBIGUOUS_REQUIRES_MANUAL_REVIEW
}

public sealed record FoodTagMigrationMap(
    string LegacyTagCode,
    string LegacyTagName,
    string TargetType,
    string? TargetCode,
    FoodTagMigrationDisposition MigrationDisposition,
    bool IsHardConstraint,
    bool IsSearchable,
    bool IsCustomerSelectable,
    string Notes);

/// <summary>
/// Reviewed specification for migrating the legacy FoodTag taxonomy. This is
/// intentionally explicit: a newly introduced system code must fail the
/// completeness tests until it receives a reviewed disposition.
/// </summary>
public static class FoodTagMigrationMapCatalog
{
    private const string FoodCourse = "FoodCourse";
    private const string Ingredient = "Ingredient";
    private const string DietaryRestriction = "DietaryRestriction";
    private const string PreparationMethod = "PreparationMethod";
    private const string TasteProfile = "TasteProfile";
    private const string SpiceLevel = "SpiceLevel";
    private const string ServingTemperature = "ServingTemperature";
    private const string DiningPurpose = "DiningPurpose";
    private const string SearchFacet = "SearchFacet";
    private const string ServingProfile = "ServingProfile";
    private const string EffectivePrice = "EffectivePrice";

    public static IReadOnlyList<FoodTagMigrationMap> Entries => EntriesHolder.Value;

    public static IReadOnlySet<string> KnownNonSystemCodes => KnownNonSystemCodesHolder.Value;

    private static readonly IReadOnlyList<FoodTagMigrationMap> NonSystemEntries =
    [
        Legacy("SPICY", "Spicy", SpiceLevel, "TASTE_SPICY", true, true, true,
            "Legacy spicy preference; migrate to the reviewed spicy level."),
        Legacy("MILD", "Mild", TasteProfile, "TASTE_LIGHT", false, true, true,
            "Legacy description means a light taste, not a proven spice intensity."),
        Legacy("GRILLED", "Grilled", PreparationMethod, "METHOD_GRILLED", false, true, false,
            "Direct preparation-method replacement."),
        Legacy("FRIED", "Fried", PreparationMethod, "METHOD_FRIED", false, true, false,
            "Direct preparation-method replacement."),
        Review("SOUP", "Soup",
            "Existing links may mean a soup/noodle dish, a preparation method, or a meal course; inspect each linked food."),
        Legacy("HOT", "Hot", ServingTemperature, "TEMP_HOT", false, true, false,
            "Direct serving-temperature replacement."),
        Legacy("FULLMEAL", "FullMeal", DiningPurpose, "PURPOSE_FULL_MEAL", false, true, true,
            "Direct dining-purpose replacement."),
        Legacy("SNACK", "Snack", DiningPurpose, "PURPOSE_SNACKING", false, true, true,
            "Direct dining-purpose replacement."),
        Review("DRINK", "Drink",
            "Relation-specific meaning: FoodItemTag links indicate COURSE_DRINK while CustomerPreference links indicate PURPOSE_REFRESHMENT."),
        Review("DESSERT", "Dessert",
            "Relation-specific meaning: FoodItemTag links indicate COURSE_DESSERT while CustomerPreference links indicate PURPOSE_DESSERT."),
        Legacy("SHAREABLE", "Shareable", ServingProfile, "SHAREABLE", false, true, true,
            "Serving attribute; it is not a meal course."),
        Legacy("CHICKEN", "Chicken", Ingredient, "ING_CHICKEN", false, true, true, "Direct ingredient replacement."),
        Legacy("BEEF", "Beef", Ingredient, "ING_BEEF", false, true, true, "Direct ingredient replacement."),
        Legacy("PORK", "Pork", Ingredient, "ING_PORK", false, true, true, "Direct ingredient replacement."),
        Legacy("SEAFOOD", "Seafood", Ingredient, "ING_SEAFOOD", false, true, true, "Direct ingredient replacement."),
        Legacy("NOODLE", "Noodle", Ingredient, "ING_NOODLE", false, true, true, "Direct ingredient replacement."),
        Legacy("RICE", "Rice", Ingredient, "ING_RICE", false, true, true, "Direct ingredient replacement."),
        Derived("BUDGETFRIENDLY", "BudgetFriendly", "Derive from the current effective price; never copy as static food metadata."),
        Derived("MIDRANGE", "MidRange", "Derive from the current effective price; never copy as static food metadata."),
        Legacy("VIETNAMESE", "Vietnamese", SearchFacet, "CUISINE_VIETNAMESE", false, true, true,
            "Cuisine search facet retained for existing linked foods."),

        Legacy("CAMBODIAN", "Cambodian", SearchFacet, "CUISINE_CAMBODIAN", false, true, true, "Cuisine search facet."),
        Legacy("COLD", "Cold", ServingTemperature, "TEMP_COLD", false, true, false, "Direct serving-temperature replacement."),
        Legacy("DALATSPECIALTY", "DalatSpecialty", SearchFacet, "ORIGIN_DA_LAT_SPECIALTY", false, true, true,
            "Origin/specialty search facet; not promoted to a category automatically."),
        Legacy("EGG", "Egg", Ingredient, "ING_EGG", false, true, true, "Direct ingredient replacement."),
        Legacy("FRUIT", "Fruit", Ingredient, "ING_FRUIT", false, true, true, "Direct ingredient replacement."),
        Legacy("JAPANESE", "Japanese", SearchFacet, "CUISINE_JAPANESE", false, true, true, "Cuisine search facet."),
        Legacy("KOREAN", "Korean", SearchFacet, "CUISINE_KOREAN", false, true, true, "Cuisine search facet."),
        Legacy("NOSEAFOOD", "NoSeafood", DietaryRestriction, "DIET_NO_SEAFOOD", true, true, true,
            "Dietary restriction only; do not create a confirmed allergen declaration."),
        Derived("PREMIUM", "Premium", "Derive from the current effective price; never copy as static food metadata."),
        Legacy("STREETFOOD", "StreetFood", SearchFacet, "FOOD_CONTEXT_STREET_FOOD", false, true, true,
            "Search context; not promoted to a category automatically."),
        Legacy("SWEET", "Sweet", TasteProfile, "TASTE_SWEET", false, true, true, "Direct taste-profile replacement."),
        Legacy("THAI", "Thai", SearchFacet, "CUISINE_THAI", false, true, true, "Cuisine search facet."),
        Legacy("VEGETARIAN", "Vegetarian", DietaryRestriction, "DIET_VEGETARIAN", true, true, true,
            "Dietary suitability only; migration must not infer ingredient absence or allergen safety."),
        Legacy("WESTERN", "Western", SearchFacet, "CUISINE_WESTERN", false, true, true, "Cuisine search facet.")
    ];

    private static readonly Lazy<IReadOnlyList<FoodTagMigrationMap>> EntriesHolder = new(Build);
    private static readonly Lazy<IReadOnlySet<string>> KnownNonSystemCodesHolder = new(() =>
        new HashSet<string>(NonSystemEntries.Select(entry => entry.LegacyTagCode), StringComparer.Ordinal));

    private static IReadOnlyList<FoodTagMigrationMap> Build()
    {
        var systemTargets = BuildSystemTargets();
        var definitions = Tags.ToDictionary(tag => tag.Code, StringComparer.Ordinal);
        var entries = new List<FoodTagMigrationMap>(definitions.Count + NonSystemEntries.Count);

        foreach (var target in systemTargets)
        {
            if (!definitions.TryGetValue(target.Code, out var definition))
                throw new InvalidOperationException($"Reviewed mapping references unknown system FoodTag code '{target.Code}'.");

            entries.Add(new FoodTagMigrationMap(
                definition.Code,
                definition.Name,
                target.TargetType,
                target.TargetCode,
                target.Disposition,
                target.IsHardConstraint,
                target.IsSearchable,
                definition.IsPreferenceSelectable,
                target.Notes));
        }

        entries.AddRange(NonSystemEntries);
        return entries;
    }

    private static IReadOnlyList<SystemTarget> BuildSystemTargets()
    {
        var entries = new List<SystemTarget>();

        Add(entries, TasteProfile,
            ["TASTE_SWEET", "TASTE_SOUR", "TASTE_SALTY", "TASTE_SAVORY", "TASTE_LIGHT", "TASTE_RICH", "TASTE_SWEET_SOUR"]);
        Add(entries, SpiceLevel, ["TASTE_MILD_SPICY", "TASTE_SPICY", "TASTE_VERY_SPICY"]);
        Add(entries, ServingTemperature, ["TEMP_HOT", "TEMP_COLD", "TEMP_ROOM"]);
        Add(entries, DiningPurpose,
            ["PURPOSE_FULL_MEAL", "PURPOSE_LIGHT_MEAL", "PURPOSE_SNACKING", "PURPOSE_FOOD_TOUR", "PURPOSE_QUICK_MEAL",
             "PURPOSE_LATE_NIGHT", "PURPOSE_DESSERT", "PURPOSE_REFRESHMENT", "PURPOSE_SHARING", "PURPOSE_TAKEAWAY"]);
        Add(entries, PreparationMethod,
            ["METHOD_GRILLED", "METHOD_CHARCOAL_GRILLED", "METHOD_FRIED", "METHOD_STIR_FRIED", "METHOD_STEAMED",
             "METHOD_BOILED", "METHOD_BRAISED", "METHOD_SIMMERED", "METHOD_PAN_FRIED", "METHOD_ROASTED", "METHOD_MIXED",
             "METHOD_ROLLED", "METHOD_SOUP_COOKED", "METHOD_HOTPOT", "METHOD_RAW"]);
        Add(entries, Ingredient,
            ["ING_BEEF", "ING_PORK", "ING_CHICKEN", "ING_DUCK", "ING_SAUSAGE", "ING_SEAFOOD", "ING_FISH", "ING_SHRIMP",
             "ING_CRAB", "ING_SQUID", "ING_OCTOPUS", "ING_SHELLFISH", "ING_EGG", "ING_MILK", "ING_CHEESE", "ING_TOFU",
             "ING_MUSHROOM", "ING_VEGETABLE", "ING_RICE", "ING_STICKY_RICE", "ING_NOODLE", "ING_BREAD", "ING_POTATO",
             "ING_CORN", "ING_FRUIT", "ING_COCONUT", "ING_CHOCOLATE", "ING_MATCHA", "ING_COFFEE", "ING_PEANUT"]);
        Add(entries, DietaryRestriction,
            ["DIET_VEGETARIAN", "DIET_VEGAN", "DIET_NO_PORK", "DIET_NO_SEAFOOD", "DIET_NON_SPICY",
             "DIET_SUGAR_FREE", "DIET_DAIRY_FREE", "DIET_EGG_FREE", "DIET_PEANUT_FREE", "DIET_GLUTEN_FREE"],
            hardConstraint: true,
            notes: "Migrate as dietary suitability only. Existing FoodTag data is not proof of allergen safety; backfill must remain unconfirmed.");
        Add(entries, DietaryRestriction,
            ["DIET_LOW_SPICY", "DIET_LOW_SUGAR", "DIET_CHILD_FRIENDLY"],
            notes: "Migrate as a soft suitability preference. Existing FoodTag data is not proof of allergen safety; backfill must remain unconfirmed.");

        foreach (var code in new[] { "BUDGET_UNDER_30000", "BUDGET_30000_50000", "BUDGET_50000_100000", "BUDGET_100000_200000", "BUDGET_FROM_200000" })
            entries.Add(new SystemTarget(code, EffectivePrice, null, FoodTagMigrationDisposition.DERIVE, true, true,
                "Derive at query time from the current effective price; do not persist as static metadata."));

        entries.AddRange(
        [
            Map("OTHER_SIGNATURE", SearchFacet, "SIGNATURE", "Search facet backed by the existing explicit declaration."),
            Map("OTHER_LOCAL_SPECIALTY", SearchFacet, "LOCAL_SPECIALTY", "Search facet backed by the existing explicit declaration."),
            Map("OTHER_SEASONAL", SearchFacet, "SEASONAL", "Search facet backed by the existing explicit declaration."),
            Map("OTHER_SHARING", ServingProfile, "SHAREABLE", "Serving attribute; not a meal course."),
            Map("OTHER_CUSTOMIZABLE", SearchFacet, "CUSTOMIZABLE", "Search facet backed by the existing explicit declaration."),
            DeriveSystem("OTHER_BEST_SELLER", "Derive from authoritative completed-sales data."),
            DeriveSystem("OTHER_NEW_ITEM", "Derive from FoodItem.CreatedAt and a configured time window."),
            new SystemTarget("OTHER_QUICK_SERVE", SearchFacet, null,
                FoodTagMigrationDisposition.ARCHIVE, false, false,
                "No authoritative service-time field exists. Retain only in the legacy archive/report; never score or index it.")
        ]);
        Add(entries, FoodCourse,
            ["COURSE_APPETIZER", "COURSE_MAIN_COURSE", "COURSE_SIDE_DISH", "COURSE_SOUP", "COURSE_SHARED_DISH",
             "COURSE_DRINK", "COURSE_DESSERT", "COURSE_EXTRA"]);

        return entries;
    }

    private static void Add(
        ICollection<SystemTarget> target,
        string targetType,
        IReadOnlyCollection<string> codes,
        bool hardConstraint = false,
        string notes = "Direct stable-code migration from the reviewed system taxonomy.")
    {
        foreach (var code in codes)
            target.Add(Map(code, targetType, code, notes, hardConstraint));
    }

    private static SystemTarget Map(string code, string targetType, string targetCode, string notes, bool hardConstraint = false)
        => new(code, targetType, targetCode, FoodTagMigrationDisposition.MIGRATE, hardConstraint, true, notes);

    private static SystemTarget DeriveSystem(string code, string notes)
        => new(code, SearchFacet, null, FoodTagMigrationDisposition.DERIVE, false, true, notes);

    private static FoodTagMigrationMap Legacy(
        string code,
        string name,
        string targetType,
        string targetCode,
        bool hardConstraint,
        bool searchable,
        bool customerSelectable,
        string notes)
        => new(code, name, targetType, targetCode, FoodTagMigrationDisposition.MIGRATE,
            hardConstraint, searchable, customerSelectable, notes);

    private static FoodTagMigrationMap Derived(string code, string name, string notes)
        => new(code, name, EffectivePrice, null, FoodTagMigrationDisposition.DERIVE,
            true, true, true, notes);

    private static FoodTagMigrationMap Review(string code, string name, string notes)
        => new(code, name, "ManualReview", null,
            FoodTagMigrationDisposition.AMBIGUOUS_REQUIRES_MANUAL_REVIEW, false, false, false, notes);

    private sealed record SystemTarget(
        string Code,
        string TargetType,
        string? TargetCode,
        FoodTagMigrationDisposition Disposition,
        bool IsHardConstraint,
        bool IsSearchable,
        string Notes);
}

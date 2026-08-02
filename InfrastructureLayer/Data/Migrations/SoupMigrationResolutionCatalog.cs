namespace InfrastructureLayer.Data.Migrations;

public enum SoupResolutionStatus { AUTO_APPROVED, MANUAL_REVIEW, NOT_APPLICABLE }

public sealed record SoupMigrationResolution(
    Guid FoodItemId,
    string FoodName,
    string ExistingCategoryCode,
    IReadOnlyList<string> ExistingTagCodes,
    string? ProposedCourse,
    string? ProposedPreparationMethod,
    string? ProposedCategoryChange,
    decimal Confidence,
    SoupResolutionStatus ResolutionStatus,
    string Evidence,
    string Notes);

public static class SoupMigrationResolutionCatalog
{
    public static IReadOnlyList<SoupMigrationResolution> ReviewedRows { get; } =
    [
        Manual("99999999-9999-9999-9999-999999990803", "Bò viên nóng", "LEGACY_88888888888888888888888888888008",
            ["BEEF","BUDGETFRIENDLY","DRINK","SOUP","VIETNAMESE"], "SOUP", "METHOD_SOUP_COOKED", 0.55m,
            "Description says 'món nước nóng', but the same row also has the contradictory DRINK tag and a generic demo category.",
            "Owner/admin confirmation is required before assigning soup course or preparation."),
        Manual("99999999-9999-9999-9999-999999990401", "Bún num bò chóc", "LEGACY_88888888888888888888888888888004",
            ["BEEF","FULLMEAL","HOT","MIDRANGE","NOODLE","SOUP","SPICY","VIETNAMESE"], "SOUP", "METHOD_SOUP_COOKED", 0.50m,
            "Name and noodle tag may indicate a broth dish, but description does not state broth/soup and category is generic demo data.",
            "Do not infer preparation from culinary knowledge alone."),
        NotApplicable("99999999-9999-9999-9999-999999990202", "Bún thịt nướng chả giò", "LEGACY_88888888888888888888888888888002",
            ["FULLMEAL","GRILLED","HOT","MIDRANGE","NOODLE","PORK","SOUP","VIETNAMESE"], "MAIN_COURSE", 0.99m,
            "Name and description explicitly describe grilled pork, raw vegetables and crispy spring rolls; no soup/broth evidence."),
        NotApplicable("99999999-9999-9999-9999-999999990301", "Chè ba màu", "LEGACY_88888888888888888888888888888003",
            ["BUDGETFRIENDLY","DESSERT","NOODLE","SOUP","VIETNAMESE"], "DESSERT", 0.99m,
            "Description explicitly identifies a cold dessert and the existing DESSERT tag corroborates it."),
        NotApplicable("99999999-9999-9999-9999-999999990403", "Gà nướng sả ớt", "LEGACY_88888888888888888888888888888004",
            ["CHICKEN","DRINK","GRILLED","MIDRANGE","SOUP","SPICY","VIETNAMESE"], "MAIN_COURSE", 0.99m,
            "Name and description explicitly identify grilled chicken; SOUP and DRINK are contradictory legacy inferences."),
        NotApplicable("99999999-9999-9999-9999-999999990904", "Nước ép dâu", "LEGACY_88888888888888888888888888888009",
            ["BUDGETFRIENDLY","DRINK","SOUP","VIETNAMESE"], "DRINK", 1.00m, "Name and description explicitly identify a cold strawberry drink."),
        NotApplicable("99999999-9999-9999-9999-999999990604", "Nước mía tắc", "LEGACY_88888888888888888888888888888006",
            ["BUDGETFRIENDLY","DRINK","SOUP","VIETNAMESE"], "DRINK", 1.00m, "Description explicitly identifies a street beverage."),
        NotApplicable("99999999-9999-9999-9999-999999990104", "Nước sâm lạnh", "LEGACY_88888888888888888888888888888001",
            ["BUDGETFRIENDLY","DRINK","GRILLED","SOUP","VIETNAMESE"], "DRINK", 1.00m, "Name, description and DRINK tag explicitly identify a cold beverage."),
        NotApplicable("99999999-9999-9999-9999-999999990303", "Nước sâm rong biển", "LEGACY_88888888888888888888888888888003",
            ["BUDGETFRIENDLY","DRINK","SOUP","VIETNAMESE"], "DRINK", 1.00m, "Description explicitly identifies a beverage."),
        NotApplicable("99999999-9999-9999-9999-999999990603", "Sữa chua nếp cẩm", "LEGACY_88888888888888888888888888888006",
            ["BUDGETFRIENDLY","HOT","NOODLE","SNACK","SOUP","VIETNAMESE"], "DESSERT", 0.99m, "Description explicitly identifies a cold, lightly sweet dessert."),
        NotApplicable("99999999-9999-9999-9999-999999990903", "Sữa chua phô mai", "LEGACY_88888888888888888888888888888009",
            ["BUDGETFRIENDLY","HOT","NOODLE","SNACK","SOUP","VIETNAMESE"], "DESSERT", 0.99m, "Description explicitly identifies a sweet, rich cold dessert."),
        NotApplicable("99999999-9999-9999-9999-999999990304", "Trái cây dầm", "LEGACY_88888888888888888888888888888003",
            ["BUDGETFRIENDLY","HOT","NOODLE","SNACK","SOUP","VIETNAMESE"], "DESSERT", 0.99m, "Description explicitly identifies a cold fruit dessert.")
    ];

    public static SoupMigrationResolution? Find(Guid foodItemId) => ReviewedRows.SingleOrDefault(value => value.FoodItemId == foodItemId);

    private static SoupMigrationResolution Manual(string id, string name, string category, string[] tags, string course, string method, decimal confidence, string evidence, string notes)
        => new(Guid.Parse(id), name, category, tags, course, method, null, confidence, SoupResolutionStatus.MANUAL_REVIEW, evidence, notes);
    private static SoupMigrationResolution NotApplicable(string id, string name, string category, string[] tags, string course, decimal confidence, string evidence)
        => new(Guid.Parse(id), name, category, tags, course, null, null, confidence, SoupResolutionStatus.NOT_APPLICABLE, evidence, "Legacy SOUP relation is retained but creates no normalized soup metadata.");
}

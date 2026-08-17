namespace InfrastructureLayer.Data.Seeders;

public static class SystemFoodTaxonomyCatalog
{
    public static IReadOnlyList<FoodCategoryDefinition> Categories { get; } = BuildCategories();

    private static IReadOnlyList<FoodCategoryDefinition> BuildCategories()
    {
        (string Code, string Name)[] values =
        [
            ("GRILLED_FOOD", "Đồ nướng"), ("SEAFOOD", "Hải sản"), ("SKEWERS", "Xiên que"),
            ("STREET_SNACKS", "Ăn vặt đường phố"), ("FRIED_FOOD", "Đồ chiên"), ("RICE_DISHES", "Cơm"),
            ("NOODLES", "Mì, bún và phở"), ("SOUP_DISHES", "Món nước"), ("HOTPOT", "Lẩu"),
            ("BREAD_AND_ROLLS", "Bánh mì và món cuốn"), ("SAVORY_CAKES", "Bánh mặn"),
            ("FAST_FOOD", "Đồ ăn nhanh"), ("VEGETARIAN", "Món chay"),
            ("LOCAL_SPECIALTIES", "Đặc sản địa phương"), ("DESSERTS", "Tráng miệng"),
            ("SWEET_SOUP_AND_ICE_CREAM", "Chè và kem"), ("BEVERAGES", "Đồ uống"),
            ("COMBO", "Combo và phần ăn")
        ];
        return values.Select((value, index) => new FoodCategoryDefinition(
            StableId("10000000", index + 1), value.Code, value.Name, index + 1)).ToArray();
    }

    private static Guid StableId(string prefix, int value)
        => Guid.Parse($"{prefix}-0000-0000-0000-{value:000000000000}");
}

public sealed record FoodCategoryDefinition(Guid Id, string Code, string Name, int DisplayOrder);

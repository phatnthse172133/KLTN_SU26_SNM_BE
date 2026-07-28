using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Data.Seeders;

public static class SystemFoodTaxonomyCatalog
{
    public static IReadOnlyList<FoodCategoryDefinition> Categories { get; } = BuildCategories();
    public static IReadOnlyList<FoodTagDefinition> Tags { get; } = BuildTags();

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

    private static IReadOnlyList<FoodTagDefinition> BuildTags()
    {
        var result = new List<FoodTagDefinition>();
        Add(FoodTagGroup.Taste, true, false,
            ("TASTE_MILD_SPICY", "Cay nhẹ"), ("TASTE_SPICY", "Cay"), ("TASTE_VERY_SPICY", "Rất cay"),
            ("TASTE_SWEET", "Ngọt"), ("TASTE_SOUR", "Chua"), ("TASTE_SALTY", "Mặn"),
            ("TASTE_SAVORY", "Đậm đà"), ("TASTE_LIGHT", "Thanh nhẹ"), ("TASTE_RICH", "Béo"),
            ("TASTE_SWEET_SOUR", "Chua ngọt"));
        Add(FoodTagGroup.Temperature, false, false,
            ("TEMP_HOT", "Dùng nóng"), ("TEMP_COLD", "Dùng lạnh"), ("TEMP_ROOM", "Nhiệt độ thường"));
        Add(FoodTagGroup.MealPurpose, true, false,
            ("PURPOSE_FULL_MEAL", "Ăn no"), ("PURPOSE_LIGHT_MEAL", "Ăn nhẹ"), ("PURPOSE_SNACKING", "Ăn vặt"),
            ("PURPOSE_FOOD_TOUR", "Food tour"), ("PURPOSE_QUICK_MEAL", "Ăn nhanh"), ("PURPOSE_LATE_NIGHT", "Ăn khuya"),
            ("PURPOSE_DESSERT", "Ăn tráng miệng"), ("PURPOSE_REFRESHMENT", "Uống giải khát"),
            ("PURPOSE_SHARING", "Phù hợp ăn chung"), ("PURPOSE_TAKEAWAY", "Mua mang đi"));
        Add(FoodTagGroup.CookingMethod, false, false,
            ("METHOD_GRILLED", "Nướng"), ("METHOD_CHARCOAL_GRILLED", "Nướng than"), ("METHOD_FRIED", "Chiên"),
            ("METHOD_STIR_FRIED", "Xào"), ("METHOD_STEAMED", "Hấp"), ("METHOD_BOILED", "Luộc"),
            ("METHOD_BRAISED", "Kho"), ("METHOD_SIMMERED", "Hầm"), ("METHOD_PAN_FRIED", "Áp chảo"),
            ("METHOD_ROASTED", "Quay"), ("METHOD_MIXED", "Trộn"), ("METHOD_ROLLED", "Cuốn"),
            ("METHOD_SOUP_COOKED", "Nấu nước"), ("METHOD_HOTPOT", "Nhúng lẩu"),
            ("METHOD_RAW", "Không qua chế biến nhiệt"));
        Add(FoodTagGroup.Ingredient, true, false,
            ("ING_BEEF", "Thịt bò"), ("ING_PORK", "Thịt heo"), ("ING_CHICKEN", "Thịt gà"), ("ING_DUCK", "Thịt vịt"),
            ("ING_SAUSAGE", "Xúc xích"), ("ING_SEAFOOD", "Hải sản"), ("ING_FISH", "Cá"), ("ING_SHRIMP", "Tôm"),
            ("ING_CRAB", "Cua"), ("ING_SQUID", "Mực"), ("ING_OCTOPUS", "Bạch tuộc"),
            ("ING_SHELLFISH", "Nghêu, sò và ốc"), ("ING_EGG", "Trứng"), ("ING_MILK", "Sữa"),
            ("ING_CHEESE", "Phô mai"), ("ING_TOFU", "Đậu hũ"), ("ING_MUSHROOM", "Nấm"),
            ("ING_VEGETABLE", "Rau củ"), ("ING_RICE", "Cơm"), ("ING_STICKY_RICE", "Nếp"),
            ("ING_NOODLE", "Mì và bún"), ("ING_BREAD", "Bánh mì"), ("ING_POTATO", "Khoai"), ("ING_CORN", "Bắp"),
            ("ING_FRUIT", "Trái cây"), ("ING_COCONUT", "Dừa"), ("ING_CHOCOLATE", "Sô-cô-la"),
            ("ING_MATCHA", "Matcha"), ("ING_COFFEE", "Cà phê"), ("ING_PEANUT", "Đậu phộng"));
        Add(FoodTagGroup.Dietary, true, false,
            ("DIET_VEGETARIAN", "Ăn chay"), ("DIET_VEGAN", "Thuần chay"), ("DIET_NO_PORK", "Không thịt heo"),
            ("DIET_NO_SEAFOOD", "Không hải sản"), ("DIET_NON_SPICY", "Không cay"), ("DIET_LOW_SPICY", "Ít cay"),
            ("DIET_LOW_SUGAR", "Ít đường"), ("DIET_SUGAR_FREE", "Không đường"), ("DIET_DAIRY_FREE", "Không sữa"),
            ("DIET_EGG_FREE", "Không trứng"), ("DIET_PEANUT_FREE", "Không đậu phộng"),
            ("DIET_GLUTEN_FREE", "Không gluten"), ("DIET_CHILD_FRIENDLY", "Phù hợp trẻ em"));
        Add(FoodTagGroup.Budget, true, true,
            ("BUDGET_UNDER_30000", "Dưới 30.000đ"), ("BUDGET_30000_50000", "30.000đ đến dưới 50.000đ"),
            ("BUDGET_50000_100000", "50.000đ đến dưới 100.000đ"),
            ("BUDGET_100000_200000", "100.000đ đến dưới 200.000đ"),
            ("BUDGET_FROM_200000", "Từ 200.000đ"));
        Add(FoodTagGroup.Other, false, false,
            ("OTHER_SIGNATURE", "Món đặc trưng"), ("OTHER_LOCAL_SPECIALTY", "Đặc sản địa phương"),
            ("OTHER_SEASONAL", "Theo mùa"), ("OTHER_SHARING", "Phù hợp chia sẻ"),
            ("OTHER_CUSTOMIZABLE", "Có thể tùy chỉnh"));
        Add(FoodTagGroup.Other, false, true,
            ("OTHER_BEST_SELLER", "Bán chạy"), ("OTHER_NEW_ITEM", "Món mới"), ("OTHER_QUICK_SERVE", "Phục vụ nhanh"));
        return result;

        void Add(FoodTagGroup group, bool preferenceSelectable, bool autoAssigned, params (string Code, string Name)[] values)
        {
            var groupOrder = result.Count(tag => tag.Group == group);
            foreach (var value in values)
            {
                var sequence = result.Count + 1;
                result.Add(new FoodTagDefinition(StableId("20000000", sequence), value.Code, value.Name, group,
                    ++groupOrder, !autoAssigned, preferenceSelectable, autoAssigned));
            }
        }
    }

    private static Guid StableId(string prefix, int value)
        => Guid.Parse($"{prefix}-0000-0000-0000-{value:000000000000}");
}

public sealed record FoodCategoryDefinition(Guid Id, string Code, string Name, int DisplayOrder);
public sealed record FoodTagDefinition(Guid Id, string Code, string Name, FoodTagGroup Group, int DisplayOrder,
    bool IsSelectable, bool IsPreferenceSelectable, bool IsAutoAssigned);

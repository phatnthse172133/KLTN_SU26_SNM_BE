using ApplicationLayer.AI.V2.Models;
using ApplicationLayer.AI.V2.Services;
using DomainLayer.Enums;
using Xunit;

namespace TestingLayer;

public sealed class NaturalLanguageRecommendationIntentTests
{
    private readonly DeterministicFoodIntentParser _parser = new();

    [Fact] public void Tired_hot_easy_and_not_oily_keeps_semantic_soft_signals()
    {
        var value = Parse("Hôm nay tôi mệt, muốn món nóng, dễ ăn và không nhiều dầu mỡ.");
        Assert.Contains(ServingTemperature.HOT, value.PreferredServingTemperatures);
        Assert.Contains("easy to eat", value.ContextualTerms); Assert.Contains("not oily", value.ContextualTerms);
    }

    [Fact] public void Refreshing_not_too_sweet_maps_temperature_and_avoidance()
    {
        var value = Parse("Trời nóng quá, cho tôi món gì thanh mát, không quá ngọt.");
        Assert.Contains(ServingTemperature.COLD, value.PreferredServingTemperatures);
        Assert.Contains("TASTE_SWEET", value.AvoidedTasteCodes);
    }

    [Fact] public void Three_friends_means_party_of_four_and_shareable_budget()
    {
        var value = Parse("Tôi đi với ba người bạn, muốn món chia sẻ, tổng khoảng 400 nghìn.");
        Assert.Equal(4, value.PartySize); Assert.True(value.IsShareablePreferred); Assert.Equal(400_000, value.MaximumPrice);
        Assert.Contains("SHARING", value.MealPurposeCodes);
    }

    [Fact] public void Hurry_takeaway_filling_and_budget_are_independent_signals()
    {
        var value = Parse("Tôi đang vội, cần món ăn no, dễ mang đi, dưới 70 nghìn.");
        Assert.True(value.TakeawayPreferred); Assert.True(value.QuickServicePreferred); Assert.Equal("FULL", value.DesiredFullness);
        Assert.Equal(70_000, value.MaximumPrice);
    }

    [Fact] public void Explicit_seafood_exclusion_is_hard_while_spice_and_vegetables_are_soft()
    {
        var value = Parse("Tôi không ăn hải sản, muốn món cay nhẹ và nhiều rau.");
        Assert.Contains("ING_SEAFOOD", value.ExcludedIngredientCodes); Assert.Equal(FoodSpiceLevel.MILD, value.PreferredSpiceLevel);
        Assert.True(value.HealthyPreference);
    }

    [Fact] public void Broad_popular_query_does_not_require_clarification()
    {
        var value = Parse("Không biết ăn gì, chọn món phổ biến, giá vừa phải.");
        Assert.True(value.PopularityPreference); Assert.False(value.ClarificationNeeded);
    }

    [Fact] public void Vegetarian_and_not_fried_are_separate_hard_and_soft_constraints()
    {
        var value = Parse("Cho món chay nhưng không thích đồ chiên.");
        Assert.Contains("DIET_VEGETARIAN", value.DietaryRequirementCodes); Assert.Contains("METHOD_FRIED", value.AvoidedPreparationMethodCodes);
    }

    [Fact] public void English_refreshing_budget_query_maps_without_translation_step()
    {
        var value = Parse("Something refreshing, not too sweet, under 60k.");
        Assert.Equal("en", value.DetectedLanguage); Assert.Equal(60_000, value.MaximumPrice);
        Assert.Contains(ServingTemperature.COLD, value.PreferredServingTemperatures); Assert.Contains("TASTE_SWEET", value.AvoidedTasteCodes);
    }

    [Fact] public void Seafood_exclusion_and_shrimp_request_requires_clarification()
    {
        var value = Parse("Không ăn hải sản nhưng muốn tôm nướng.");
        Assert.True(value.ClarificationNeeded); Assert.Contains("SEAFOOD_EXCLUSION_CONFLICT", value.Ambiguities);
    }

    [Theory]
    [InlineData("asdfghjkl.")]
    [InlineData("Viết code cho tôi.")]
    public void Insufficient_or_non_food_text_is_rejected(string query)
        => Assert.False(_parser.Parse(Request(query)).IsSuccess);

    private FoodRecommendationIntent Parse(string query)
    {
        var result = _parser.Parse(Request(query)); Assert.True(result.IsSuccess); return result.ParsedResult!;
    }

    private static FoodRecommendationIntentRequest Request(string query) => new(query, new(
        ["ING_BEEF", "ING_CHICKEN", "ING_PORK", "ING_SEAFOOD", "ING_SHRIMP", "ING_FISH", "ING_SQUID", "ING_EGG", "ING_VEGETABLE", "ING_TOFU", "ING_PEANUT"],
        [], ["DIET_VEGETARIAN"], ["METHOD_GRILLED", "METHOD_FRIED", "METHOD_PAN_FRIED", "METHOD_STEAMED", "METHOD_BOILED", "METHOD_STIR_FRIED", "METHOD_ROASTED", "METHOD_SIMMERED", "METHOD_MIXED"],
        ["TASTE_SWEET", "TASTE_SOUR", "TASTE_SALTY", "TASTE_RICH", "TASTE_LIGHT", "TASTE_SPICY", "TASTE_MILD_SPICY", "TASTE_VERY_SPICY"],
        Enum.GetNames<FoodCourse>(), Enum.GetNames<DiningPurpose>()));
}

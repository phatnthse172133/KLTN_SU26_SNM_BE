using System.Text.Json;
using ApplicationLayer.AI.V2.Services;
using DomainLayer.Entities;
using DomainLayer.Enums;

namespace TestingLayer;

public sealed class MealPlanV2Tests
{
    [Theory]
    [InlineData(MealPlanDiningStyle.FULL_MEAL)]
    [InlineData(MealPlanDiningStyle.LIGHT_MEAL)]
    [InlineData(MealPlanDiningStyle.FOOD_TOUR)]
    [InlineData(MealPlanDiningStyle.FAMILY)]
    [InlineData(MealPlanDiningStyle.DATE)]
    [InlineData(MealPlanDiningStyle.FRIEND_GROUP)]
    [InlineData(MealPlanDiningStyle.BUDGET_FRIENDLY)]
    [InlineData(MealPlanDiningStyle.LOCAL_SPECIALTY)]
    public void Every_supported_style_has_a_bounded_explicit_policy(MealPlanDiningStyle style)
    {
        var policy = new MealPlanPolicyResolver().Resolve(style);
        Assert.Equal(style, policy.Style); Assert.NotEmpty(policy.RequiredCourses);
        Assert.InRange(policy.MinimumFoods, 1, policy.MaximumFoods); Assert.InRange(policy.MaximumFoods, 1, 6);
        Assert.True(policy.RequireServingData); Assert.InRange(policy.BudgetTarget, .5m, 1m);
    }

    [Fact]
    public void Authoritative_recalculation_excludes_removed_drink_from_main_serving_and_increments_version()
    {
        var market = Guid.NewGuid(); var now = DateTime.UtcNow;
        var plan = new AiMealPlan { Id = Guid.NewGuid(), MarketId = market, DistanceMeters = 1000 };
        plan.AddItem(Item(FoodCourse.MAIN_COURSE, 100_000, 2, 2), market, 500_000, false);
        var drink = Item(FoodCourse.DRINK, 20_000, 1, 20); plan.AddItem(drink, market, 500_000, false);
        drink.MarkRemoved(now);
        var policy = new MealPlanPolicyResolver().Resolve(MealPlanDiningStyle.LIGHT_MEAL);
        var result = new MealPlanRecalculationService().Recalculate(plan, policy, 2, 500_000, now);
        Assert.Equal(200_000, plan.TotalPrice); Assert.Equal(4, plan.EstimatedServingCount);
        Assert.True(plan.IsComplete); Assert.Equal(1, plan.Version); Assert.InRange(result.Total, 0, 100);
    }

    [Fact]
    public void Missing_serving_data_cannot_become_a_fake_complete_plan()
    {
        var market = Guid.NewGuid(); var plan = new AiMealPlan { Id = Guid.NewGuid(), MarketId = market };
        plan.AddItem(Item(FoodCourse.MAIN_COURSE, 50_000, 1, null), market, 200_000, false);
        new MealPlanRecalculationService().Recalculate(plan,
            new MealPlanPolicyResolver().Resolve(MealPlanDiningStyle.FULL_MEAL), 1, 200_000, DateTime.UtcNow);
        Assert.False(plan.IsComplete); Assert.Null(plan.EstimatedServingCount);
        Assert.Contains("SERVING_DATA_INSUFFICIENT", JsonSerializer.Deserialize<string[]>(plan.WarningsJson!)!);
    }

    [Fact]
    public void Domain_rejects_cross_market_duplicate_and_invalid_quantity()
    {
        var market = Guid.NewGuid(); var plan = new AiMealPlan { Id = Guid.NewGuid(), MarketId = market };
        var food = Guid.NewGuid(); plan.AddItem(Item(FoodCourse.MAIN_COURSE, 10, 1, 1, food), market, 100);
        Assert.Throws<InvalidOperationException>(() => plan.AddItem(Item(FoodCourse.DRINK, 10, 1, 1), Guid.NewGuid(), 100));
        Assert.Throws<InvalidOperationException>(() => plan.AddItem(Item(FoodCourse.MAIN_COURSE, 10, 1, 1, food), market, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.AddItem(Item(FoodCourse.MAIN_COURSE, 10, 0, 1), market, 100));
    }

    [Fact]
    public void Session_expiration_is_boundary_exact()
    {
        var now = DateTime.UtcNow; var session = AiMealPlanSession.Create(2, 100_000, now, now.AddHours(2));
        Assert.False(session.IsExpired(now.AddHours(2).AddTicks(-1))); Assert.True(session.IsExpired(now.AddHours(2)));
    }

    private static AiMealPlanItem Item(FoodCourse course, decimal price, int quantity, int? serving, Guid? food = null) => new()
    {
        Id = Guid.NewGuid(), FoodItemId = food ?? Guid.NewGuid(), BoothId = Guid.NewGuid(), FoodNameSnapshot = "Food",
        BoothNameSnapshot = "Booth", Course = course, Quantity = quantity, UnitPriceSnapshot = price,
        ServingCountSnapshot = serving.HasValue ? serving * quantity : null, CompatibilityScore = 80, Reason = "Grounded"
    };
}

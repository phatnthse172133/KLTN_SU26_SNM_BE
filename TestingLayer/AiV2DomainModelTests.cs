using DomainLayer.Entities;
using DomainLayer.Enums;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace TestingLayer;

public class AiV2DomainModelTests
{
    [Fact]
    public void FoodItem_RejectsNonPositiveServingCount_AndAcceptsUnknownSemantics()
    {
        var food = new FoodItem { SpiceLevel = FoodSpiceLevel.UNKNOWN, ServingTemperature = null };
        Assert.Throws<ArgumentOutOfRangeException>(() => food.EstimatedServingCount = 0);
        food.EstimatedServingCount = null;
        Assert.Equal(FoodSpiceLevel.UNKNOWN, food.SpiceLevel);
        Assert.Null(food.ServingTemperature);
    }

    [Fact]
    public void MealPlan_EnforcesMarketQuantityScoreDuplicateBudgetAndRemovalInvariants()
    {
        var market = Guid.NewGuid();
        var budget = 200_000m;
        var plan = new AiMealPlan { Id = Guid.NewGuid(), MarketId = market };
        var item = Item(50_000m, 2, 85m);

        Assert.Throws<InvalidOperationException>(() => plan.AddItem(item, Guid.NewGuid(), budget));
        plan.AddItem(item, market, budget);
        Assert.Equal(100_000m, plan.TotalPrice);
        Assert.Equal(100_000m, plan.RemainingBudget);
        Assert.Equal(1, plan.Version);
        Assert.Throws<InvalidOperationException>(() => plan.AddItem(Item(1, 1, 1, item.FoodItemId), market, budget));
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.AddItem(Item(1, 0, 1), market, budget));
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.AddItem(Item(1, 1, 101), market, budget));
        Assert.Throws<InvalidOperationException>(() => plan.AddItem(Item(200_001, 1, 50), market, budget));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AiMealPlan { CompatibilityScore = 101 });

        plan.RemoveItem(item.Id, budget);
        Assert.Equal(0m, plan.TotalPrice);
        Assert.Equal(budget, plan.RemainingBudget);
        Assert.Equal(2, plan.Version);
        Assert.Throws<InvalidOperationException>(() => plan.MarkComplete());
    }

    [Fact]
    public void MealPlanSession_ValidatesInputsAndExpiration()
    {
        var now = DateTime.UtcNow;
        Assert.Throws<ArgumentOutOfRangeException>(() => AiMealPlanSession.Create(0, 1, now, now.AddHours(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => AiMealPlanSession.Create(1, 0, now, now.AddHours(1)));
        var session = AiMealPlanSession.Create(2, 100_000, now, now.AddHours(1));
        Assert.False(session.IsExpired(now));
        Assert.True(session.IsExpired(now.AddHours(1)));
    }

    [Fact]
    public void CustomerProfile_HardAvoidedIngredientWins_AndTasteConflictIsVisible()
    {
        var ingredient = Guid.NewGuid();
        var taste = Guid.NewGuid();
        var profile = new CustomerFoodProfile();
        profile.PreferredIngredients.Add(new CustomerPreferredIngredient { IngredientId = ingredient });
        profile.AvoidedIngredients.Add(new CustomerAvoidedIngredient { IngredientId = ingredient });
        profile.PreferredTasteProfiles.Add(new CustomerPreferredTasteProfile { TasteProfileId = taste });
        profile.AvoidedTasteProfiles.Add(new CustomerAvoidedTasteProfile { TasteProfileId = taste });
        Assert.False(profile.IsIngredientAllowed(ingredient));
        Assert.True(profile.HasTasteConflict(taste));
    }

    [Fact]
    public void EfModel_DeclaresUniqueCatalogCodesCompositeRelationsAndSinglePrimaryCourse()
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new SNMDbContext(options);
        var ingredient = db.Model.FindEntityType(typeof(Ingredient))!;
        Assert.Contains(ingredient.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Ingredient.Code));
        var relation = db.Model.FindEntityType(typeof(FoodItemIngredient))!;
        Assert.Equal(new[] { nameof(FoodItemIngredient.FoodItemId), nameof(FoodItemIngredient.IngredientId) }, relation.FindPrimaryKey()!.Properties.Select(value => value.Name));
        var course = db.Model.FindEntityType(typeof(FoodItemCourse))!;
        Assert.Contains(course.GetIndexes(), index => index.IsUnique && index.GetFilter() == "\"IsPrimary\" = true");
    }

    private static AiMealPlanItem Item(decimal price, int quantity, decimal score, Guid? foodId = null) => new()
    {
        Id = Guid.NewGuid(),
        FoodItemId = foodId ?? Guid.NewGuid(),
        Quantity = quantity,
        UnitPriceSnapshot = price,
        CompatibilityScore = score,
        FoodNameSnapshot = "Food",
        BoothNameSnapshot = "Booth",
        Reason = "Reason"
    };
}

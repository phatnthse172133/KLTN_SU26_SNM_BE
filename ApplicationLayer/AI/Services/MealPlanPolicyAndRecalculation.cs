using System.Text.Json;
using ApplicationLayer.AI.V2.MealPlans;
using DomainLayer.Entities;
using DomainLayer.Enums;

namespace ApplicationLayer.AI.V2.Services;

public sealed class MealPlanPolicyResolver : IMealPlanPolicyResolver
{
    public MealPlanStylePolicy Resolve(MealPlanDiningStyle style) => style switch
    {
        MealPlanDiningStyle.LIGHT_MEAL => Policy(style, 1, 3, 1, .75m, .65m, [FoodCourse.MAIN_COURSE], [FoodCourse.DRINK, FoodCourse.SIDE_DISH]),
        MealPlanDiningStyle.FOOD_TOUR => Policy(style, 3, 6, 2, 1m, .90m, [FoodCourse.MAIN_COURSE], [FoodCourse.APPETIZER, FoodCourse.SHARED_DISH, FoodCourse.SIDE_DISH, FoodCourse.DRINK, FoodCourse.DESSERT]),
        MealPlanDiningStyle.DATE => Policy(style, 2, 5, 2, 1m, .85m, [FoodCourse.MAIN_COURSE], [FoodCourse.APPETIZER, FoodCourse.DRINK, FoodCourse.DESSERT]),
        MealPlanDiningStyle.FAMILY => Policy(style, 2, 6, 2, 1m, .90m, [FoodCourse.MAIN_COURSE], [FoodCourse.SHARED_DISH, FoodCourse.SIDE_DISH, FoodCourse.DRINK]),
        MealPlanDiningStyle.FRIEND_GROUP => Policy(style, 2, 6, 2, 1m, .90m, [FoodCourse.MAIN_COURSE], [FoodCourse.SHARED_DISH, FoodCourse.SIDE_DISH, FoodCourse.DRINK]),
        MealPlanDiningStyle.BUDGET_FRIENDLY => Policy(style, 1, 4, 1, 1m, .70m, [FoodCourse.MAIN_COURSE], [FoodCourse.SIDE_DISH, FoodCourse.DRINK]),
        MealPlanDiningStyle.LOCAL_SPECIALTY => Policy(style, 2, 5, 2, 1m, .90m, [FoodCourse.MAIN_COURSE], [FoodCourse.APPETIZER, FoodCourse.SHARED_DISH, FoodCourse.DRINK]),
        _ => Policy(style, 1, 5, 1, 1m, .90m, [FoodCourse.MAIN_COURSE], [FoodCourse.APPETIZER, FoodCourse.SHARED_DISH, FoodCourse.SIDE_DISH, FoodCourse.DRINK, FoodCourse.DESSERT])
    };

    private static MealPlanStylePolicy Policy(MealPlanDiningStyle style, int min, int max, int booths,
        decimal serving, decimal budget, FoodCourse[] required, FoodCourse[] optional)
        => new(style, required, optional, min, max, booths, serving, budget, true);
}

public sealed class MealPlanRecalculationService : IMealPlanRecalculationService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public MealPlanScoreBreakdown Recalculate(AiMealPlan plan, MealPlanStylePolicy policy, int partySize,
        decimal budget, DateTime utcNow, bool incrementVersion = true)
    {
        var items = plan.Items.Where(item => !item.IsRemoved).OrderBy(item => item.SortOrder).ToArray();
        var warnings = new List<string>();
        var courseComplete = policy.RequiredCourses.All(course => items.Any(item => item.Course == course));
        var mainItems = items.Where(item => item.Course is FoodCourse.MAIN_COURSE or FoodCourse.SHARED_DISH).ToArray();
        var servingKnown = mainItems.Length > 0 && mainItems.All(item => item.ServingCountSnapshot.HasValue);
        var serving = servingKnown ? mainItems.Sum(item => item.ServingCountSnapshot!.Value) : (int?)null;
        var requiredServing = (int)Math.Ceiling(partySize * policy.ServingMultiplier);
        var servingComplete = servingKnown && serving >= requiredServing;
        var foodCountComplete = items.Length >= policy.MinimumFoods && items.Length <= policy.MaximumFoods;
        var boothComplete = items.Select(item => item.BoothId).Where(id => id.HasValue).Distinct().Count() >= policy.MinimumBooths;
        if (!courseComplete) warnings.Add("REQUIRED_COURSE_MISSING");
        if (!servingKnown) warnings.Add("SERVING_DATA_INSUFFICIENT");
        else if (!servingComplete) warnings.Add("SERVING_INSUFFICIENT");
        if (!foodCountComplete) warnings.Add("FOOD_COUNT_INCOMPLETE");
        if (!boothComplete) warnings.Add("BOOTH_COMPOSITION_INCOMPLETE");

        var total = items.Sum(item => item.UnitPriceSnapshot * item.Quantity);
        if (total > budget) throw new InvalidOperationException("The plan exceeds its budget.");
        var compatibility = items.Length == 0 ? 0 : Math.Clamp(items.Average(item => item.CompatibilityScore) / 100m * 45m, 0, 45);
        var completeness = courseComplete && foodCountComplete ? 20m : courseComplete ? 10m : 0m;
        var servingPoints = servingComplete ? 15m : servingKnown && serving > 0 ? Math.Min(14m, 15m * serving.Value / requiredServing) : 0m;
        var target = budget * policy.BudgetTarget;
        var budgetPoints = target <= 0 ? 0 : Math.Clamp(10m - Math.Abs(target - total) / target * 10m, 0, 10);
        var rated = items.Where(item => item.RatingSnapshot.HasValue && item.ReviewCountSnapshot > 0).ToArray();
        var rating = rated.Length == 0 ? 0 : Math.Clamp(rated.Average(item => item.RatingSnapshot!.Value) / 5m * 5m, 0, 5);
        var booths = boothComplete ? 5m : items.Select(item => item.BoothId).Distinct().Count() > 0 ? 2m : 0m;
        var score = Round(compatibility + completeness + servingPoints + budgetPoints + rating + booths);
        var complete = items.Length > 0 && courseComplete && servingComplete && foodCountComplete && boothComplete;
        plan.ApplyAuthoritativeCalculation(budget, serving, complete, score,
            JsonSerializer.Serialize(warnings.Distinct(StringComparer.Ordinal).OrderBy(value => value), Json), utcNow, incrementVersion);
        return new() { FoodCompatibility = Round(compatibility), Completeness = completeness, ServingAdequacy = Round(servingPoints),
            BudgetUtilization = Round(budgetPoints), Distance = 0, RatingQuality = Round(rating),
            BoothComposition = Round(booths), Total = score };
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

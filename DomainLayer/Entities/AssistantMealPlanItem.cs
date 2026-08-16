namespace DomainLayer.Entities;

public sealed class AssistantMealPlanItem
{
    public Guid Id { get; set; }
    public Guid MealPlanId { get; set; }
    public Guid FoodItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public int DisplayOrder { get; set; }

    public AssistantMealPlan MealPlan { get; set; } = null!;
    public FoodItem FoodItem { get; set; } = null!;
}

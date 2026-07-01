namespace DomainLayer.Entities;

public class PromotionFoodItem
{
    public Guid PromotionId { get; set; }

    public Guid FoodItemId { get; set; }

    public virtual Promotion Promotion { get; set; } = null!;

    public virtual FoodItem FoodItem { get; set; } = null!;
}

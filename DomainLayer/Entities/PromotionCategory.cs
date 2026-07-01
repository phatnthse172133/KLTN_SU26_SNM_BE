namespace DomainLayer.Entities;

public class PromotionCategory
{
    public Guid PromotionId { get; set; }

    public Guid CategoryId { get; set; }

    public virtual Promotion Promotion { get; set; } = null!;

    public virtual FoodCategory Category { get; set; } = null!;
}

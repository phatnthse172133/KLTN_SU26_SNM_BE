namespace DomainLayer.Entities;

// Bảng nối gắn các tag ngữ nghĩa vào từng món ăn.
public partial class FoodItemTag
{
    public Guid FoodItemId { get; set; }

    public Guid FoodTagId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual FoodItem FoodItem { get; set; } = null!;

    public virtual FoodTag FoodTag { get; set; } = null!;
}

using DomainLayer.Common;

namespace DomainLayer.Entities;

// Món trong giỏ hàng. Giá không được snapshot tại đây mà luôn lấy theo giá hiện tại.
public class CartItem : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid CartId { get; set; }

    public Guid FoodItemId { get; set; }

    public int Quantity { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Cart Cart { get; set; } = null!;

    public virtual FoodItem FoodItem { get; set; } = null!;
}

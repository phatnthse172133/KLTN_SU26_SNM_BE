using System;

namespace DomainLayer.Entities;

// Customer food-item review attached to a specific completed order line.
public partial class FoodReview
{
    public Guid Id { get; set; }

    public Guid OrderDetailId { get; set; }

    public Guid OrderId { get; set; }

    public Guid FoodItemId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid BoothId { get; set; }

    public short Rating { get; set; }

    public string? Content { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsVisible { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual OrderDetail OrderDetail { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;

    public virtual FoodItem FoodItem { get; set; } = null!;

    public virtual User Customer { get; set; } = null!;

    public virtual Booth Booth { get; set; } = null!;
}

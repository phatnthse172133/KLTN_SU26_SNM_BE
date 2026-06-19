using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;


// Bảng giá theo ngày trong tuần - override giá mặc định của FoodItem
public partial class FoodPrice
{
    public Guid Id { get; set; }

    public Guid FoodItemId { get; set; }

    public DateTime DayApply { get; set; }

    public decimal Price { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual FoodItem FoodItem { get; set; } = null!;
}

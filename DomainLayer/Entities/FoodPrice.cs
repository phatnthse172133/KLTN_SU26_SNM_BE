using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Bảng giá theo ngày trong tuần - override giá mặc định của FoodItem
/// </summary>
public partial class FoodPrice
{
    public Guid Id { get; set; }

    public Guid FoodItemId { get; set; }

    /// <summary>
    /// Monday/Tuesday/.../Sunday hoặc Weekday/Weekend
    /// </summary>
    public string DayApply { get; set; } = null!;

    public decimal Price { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual FoodItem FoodItem { get; set; } = null!;
}

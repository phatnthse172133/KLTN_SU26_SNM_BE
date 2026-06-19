using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;


// Danh mục món ăn dùng chung toàn hệ thống
public partial class FoodCategory
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<FoodItem> FoodItems { get; set; } = new List<FoodItem>();
}

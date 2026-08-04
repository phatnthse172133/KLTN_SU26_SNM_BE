using System;
using System.Collections.Generic;

using DomainLayer.Common;

namespace DomainLayer.Entities;


// Bảng giá theo thời điểm - override giá mặc định của FoodItem
public partial class FoodPrice : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid FoodItemId { get; set; }

    public decimal Price { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual FoodItem FoodItem { get; set; } = null!;
}

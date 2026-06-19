using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;


// Thư viện ảnh (gallery) cho từng món ăn
public partial class FoodImage
{
    public Guid Id { get; set; }

    public Guid FoodItemId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual FoodItem FoodItem { get; set; } = null!;
}

using System;
using System.Collections.Generic;
using DomainLayer.Common;

namespace DomainLayer.Entities;


// Danh mục món ăn của từng gian hàng
public partial class FoodCategory : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual ICollection<FoodItem> FoodItems { get; set; } = new List<FoodItem>();

    public virtual ICollection<PromotionCategory> PromotionCategories { get; set; } = new List<PromotionCategory>();
}

using System;
using System.Collections.Generic;
using DomainLayer.Common;

namespace DomainLayer.Entities;


// Danh mục món ăn của từng gian hàng
public partial class FoodCategory : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid? BoothId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsSelectable { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth? Booth { get; set; }

    public virtual ICollection<FoodItem> FoodItems { get; set; } = new List<FoodItem>();

    public virtual ICollection<PromotionCategory> PromotionCategories { get; set; } = new List<PromotionCategory>();
}

using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Món ăn của từng gian hàng
/// </summary>
public partial class FoodItem
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public Guid CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// Giá mặc định. Nếu có FoodPrice theo ngày hiện tại thì giá đó được ưu tiên (override)
    /// </summary>
    public decimal Price { get; set; }

    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// false khi món hết nguyên liệu hoặc chủ quán tạm ẩn
    /// </summary>
    public bool IsAvailable { get; set; }

    public bool IsFeatured { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual FoodCategory Category { get; set; } = null!;

    public virtual ICollection<FoodImage> FoodImages { get; set; } = new List<FoodImage>();

    public virtual ICollection<FoodPrice> FoodPrices { get; set; } = new List<FoodPrice>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}

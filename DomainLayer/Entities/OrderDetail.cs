using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Chi tiết món ăn trong từng đơn hàng
/// </summary>
public partial class OrderDetail
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid FoodItemId { get; set; }

    public int Quantity { get; set; }

    /// <summary>
    /// SNAPSHOT giá tại thời điểm đặt hàng - KHÔNG tính lại từ FoodItem.Price
    /// </summary>
    public decimal UnitPrice { get; set; }

    public decimal TotalPrice { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual FoodItem FoodItem { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}

using System;
using System.Collections.Generic;
using DomainLayer.Enums;

namespace DomainLayer.Entities;

/// <summary>
/// Đơn hàng của khách
/// </summary>
public partial class Order
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public string OrderCode { get; set; } = null!;

    /// <summary>
    /// Pending | Confirmed | Preparing | Completed | Cancelled
    /// </summary>
    public OrderStatus Status { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// TotalAmount - DiscountAmount
    /// </summary>
    public decimal FinalAmount { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public virtual User Customer { get; set; } = null!;

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<PromotionUsage> PromotionUsages { get; set; } = new List<PromotionUsage>();

    public virtual Review? Review { get; set; }
}

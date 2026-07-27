using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Đơn hàng của khách
public partial class Order
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Guid BoothOwnerId { get; set; }

    public long OrderCode { get; set; }

    // Nullable only for legacy rows created before checkout idempotency existed.
    public Guid? CheckoutRequestId { get; set; }

    public OrderStatus Status { get; set; } //Trạng thái làm món

    //public PayOrderStatus PayStatus { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    // FinalAmount = TotalAmount - DiscountAmount
    public decimal FinalAmount { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public virtual User Customer { get; set; } = null!;

    public virtual User BoothOwner { get; set; } = null!;

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<PromotionUsage> PromotionUsages { get; set; } = new List<PromotionUsage>();

    public virtual Review? Review { get; set; }
}

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

    public Guid BoothId { get; set; }

    public long OrderCode { get; set; }

    // Nullable only for legacy rows created before checkout idempotency existed.
    public Guid? CheckoutRequestId { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? RequestHash { get; set; }

    public string? CheckoutCartItemIds { get; set; }

    public OrderStatus Status { get; set; } //Trạng thái làm món

    public PaymentType PaymentMethod { get; set; }

    //public PayOrderStatus PayStatus { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    // FinalAmount = TotalAmount - DiscountAmount
    public decimal FinalAmount { get; set; }

    public string? Note { get; set; }

    public Guid? PromotionId { get; set; }

    public string? PromotionSnapshot { get; set; }

    public string? CancellationReason { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public virtual User Customer { get; set; } = null!;

    public virtual User BoothOwner { get; set; } = null!;

    public virtual Booth Booth { get; set; } = null!;

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<PromotionUsage> PromotionUsages { get; set; } = new List<PromotionUsage>();

    public virtual Review? Review { get; set; }

    public void MarkPaymentPending(DateTime now)
    {
        Ensure(Status is OrderStatus.Placed or OrderStatus.PaymentFailed, "Order cannot enter pending payment from its current state.");
        Status = OrderStatus.PendingPayment;
        UpdatedAt = now;
    }

    public void MarkPaid(DateTime now)
    {
        Ensure(Status is OrderStatus.PendingPayment or OrderStatus.PaymentFailed or OrderStatus.Placed, "Only an unpaid PayOS order can be marked paid.");
        Status = OrderStatus.Placed;
        UpdatedAt = now;
    }

    public void PlaceCashOrder(DateTime now)
    {
        Ensure(Status is OrderStatus.PendingPayment or OrderStatus.Placed, "Cash order cannot be placed from its current state.");
        Status = OrderStatus.Placed;
        UpdatedAt = now;
    }

    public void StartPreparing(DateTime now)
    {
        Ensure(Status == OrderStatus.Placed, "Only a placed order can start preparing.");
        Status = OrderStatus.Preparing;
        UpdatedAt = now;
    }

    public void MarkReadyForPickup(DateTime now)
    {
        Ensure(Status == OrderStatus.Preparing, "Only a preparing order can become ready for pickup.");
        Status = OrderStatus.ReadyForPickup;
        UpdatedAt = now;
    }

    public void Complete(DateTime now)
    {
        Ensure(Status == OrderStatus.ReadyForPickup, "Only an order ready for pickup can be completed.");
        Status = OrderStatus.Completed;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void Cancel(string? reason, DateTime now)
    {
        Ensure(Status is OrderStatus.Placed or OrderStatus.PendingPayment or OrderStatus.PaymentFailed, "Order can no longer be cancelled.");
        Status = OrderStatus.Cancelled;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        CancelledAt = now;
        UpdatedAt = now;
    }

    public void MarkPaymentFailed(DateTime now)
    {
        Ensure(Status == OrderStatus.PendingPayment, "Only a pending payment order can fail payment.");
        Status = OrderStatus.PaymentFailed;
        UpdatedAt = now;
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

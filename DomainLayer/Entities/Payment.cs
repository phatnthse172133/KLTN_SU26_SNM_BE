using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Lịch sử giao dịch thanh toán/hoàn tiền - tích hợp đa cổng VNPay/ZaloPay/MoMo/Payos
public partial class Payment
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    // FK tới User – chủ gian hàng nhận tiền
    public Guid BoothOwnerId { get; set; }

    public PaymentType Type { get; set; } //Cash hoặc PayOS

    public PaymentGateway Gateway { get; set; }

    public decimal Amount { get; set; }

    //public string Currency { get; set; } = null!;

    public PaymentStatus Status { get; set; }

    // Cổng PayOS cần các trường này để lưu link thanh toán
    public string? CheckoutUrl { get; set; }
    public string? PaymentLinkId { get; set; }
    public string? GatewayRef { get; set; }

    // PayOS order code for this specific payment transaction (used for supplemental payments)
    public long? PayOSOrderCode { get; set; }

    public string? RefundReason { get; set; }

    // Authoritative refund snapshot and PayOS payout identifiers. Destination
    // bank data is deliberately not persisted here.
    public decimal? RefundAmount { get; set; }
    public string? RefundReference { get; set; }
    public string? PayoutId { get; set; }
    public DateTime? PayoutCreateClaimedAt { get; set; }
    public DateTime? RefundRequestedAt { get; set; }
    public DateTime? RefundedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation: chủ gian hàng nhận tiền
    public virtual User BoothOwner { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}

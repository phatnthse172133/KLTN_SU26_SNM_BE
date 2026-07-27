using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Lá»‹ch sá»­ giao dá»‹ch thanh toÃ¡n/hoÃ n tiá»n - tÃ­ch há»£p Ä‘a cá»•ng VNPay/ZaloPay/MoMo/Payos
public partial class Payment
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    // FK tá»›i User â€“ chá»§ gian hÃ ng nháº­n tiá»n
    public Guid BoothOwnerId { get; set; }

    public PaymentType Type { get; set; } //Cash hoáº·c PayOS

    public PaymentGateway Gateway { get; set; }

    public decimal Amount { get; set; }

    //public string Currency { get; set; } = null!;

    public PaymentStatus Status { get; set; }

    // Cá»•ng PayOS cáº§n cÃ¡c trÆ°á»ng nÃ y Ä‘á»ƒ lÆ°u link thanh toÃ¡n
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

    // Navigation: chá»§ gian hÃ ng nháº­n tiá»n
    public virtual User BoothOwner { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}

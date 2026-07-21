using DomainLayer.Enums;
using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Lá»‹ch sá»­ Ä‘Äƒng kÃ½ gÃ³i dá»‹ch vá»¥ cá»§a gian hÃ ng
public partial class BoothSubscription
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public Guid PackageId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    // Active | Expired | Cancelled | PendingPayment
    public SubscriptionStatus Status { get; set; }

    public string? PaymentEvidenceUrl { get; set; }

    public string? AdminNotes { get; set; }

    public decimal PaidAmount { get; set; }

    public long? PayOSOrderCode { get; set; }

    public string? PayOSPaymentLinkId { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? PaymentExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? PolicyVersion { get; set; }
    public DateTime? PolicyAcceptedAt { get; set; }
    public string? PolicySnapshotJson { get; set; }
    public string? ChangeType { get; set; }
    public Guid? PreviousSubscriptionId { get; set; }
    public decimal CreditAmount { get; set; }
    public int? PausedRemainingDays { get; set; }
    public DateTime? PausedAt { get; set; }

    public string? BuyerName { get; set; }
    public string? BuyerEmail { get; set; }
    public string? BuyerPhone { get; set; }


    public virtual Booth Booth { get; set; } = null!;

    public virtual Package Package { get; set; } = null!;
}
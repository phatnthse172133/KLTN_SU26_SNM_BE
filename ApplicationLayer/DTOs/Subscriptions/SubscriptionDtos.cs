using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Subscriptions
{
    public class PurchaseSubscriptionRequest
    {
        public Guid PackageId { get; set; }
        public int? DurationDays { get; set; }
        public bool AcceptedPolicy { get; set; }
        public string? AcceptedPolicyVersion { get; set; }
    }

    public class RenewSubscriptionRequest
    {
        public int? DurationDays { get; set; }
        public bool AcceptedPolicy { get; set; }
        public string? AcceptedPolicyVersion { get; set; }
    }

    public class CurrentSubscriptionResponse
    {
        public Guid SubscriptionId { get; set; }
        public string? PackageCode { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int DaysRemaining { get; set; }
        public string? Entitlements { get; set; }
        public decimal PaidAmount { get; set; }
        public bool HasPendingRequest { get; set; }
        public long? PayOSOrderCode { get; set; }
    }

    public class SubscriptionHistoryItem
    {
        public Guid Id { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string? PackageCode { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal PaidAmount { get; set; }
        public long? PayOSOrderCode { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PurchaseSubscriptionResponse
    {
        public Guid SubscriptionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal PaidAmount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PayOSPaymentResponseDto
    {
        public Guid SubscriptionId { get; set; }
        public long OrderCode { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public int DurationDays { get; set; }
        public decimal Amount { get; set; }
        public string QrCode { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTimeOffset? ExpiresAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class PaymentStatusResponse
    {
        public Guid SubscriptionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal PaidAmount { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class CancelPaymentResponse
    {
        public Guid SubscriptionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}

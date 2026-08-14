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
        public Guid? PackageId { get; set; }
        public string? PackageCode { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string? PackageImageUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int DaysRemaining { get; set; }
        public string? Entitlements { get; set; }
        public decimal PaidAmount { get; set; }
        public bool HasPendingRequest { get; set; }
        public long? PayOSOrderCode { get; set; }
        public Guid? PendingSubscriptionId { get; set; }
        public string? PendingPackageCode { get; set; }
        public string? PendingPackageName { get; set; }
        public string? PendingStatus { get; set; }
        public DateTime? PendingExpiresAt { get; set; }
        // Kept during the client transition; new clients use PendingExpiresAt.
        public DateTime? PendingPaymentExpiresAt { get; set; }
    }

    public class SubscriptionQuoteRequest
    {
        public Guid PackageId { get; set; }
        public int? DurationDays { get; set; }
    }

    public class SubscriptionQuoteResponse
    {
        public string CurrentPackageName { get; set; } = string.Empty;
        public string TargetPackageName { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty;
        public decimal BaseAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal AmountDue { get; set; }
        public string Currency { get; set; } = "VND";
        public DateTime? CurrentPlanEndDate { get; set; }
        public string ActivationMode { get; set; } = "PayNow";
        public string PendingAction { get; set; } = "None";
        public Guid? PendingSubscriptionId { get; set; }
        public string? PendingPackageName { get; set; }
        public DateTime? PendingPaymentExpiresAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class SubscriptionHistoryItem
    {
        public Guid Id { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string? PackageCode { get; set; }
        public string? PackageImageUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string? ChangeType { get; set; }
        public string? PreviousPackageName { get; set; }
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
        public decimal BaseAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal Amount { get; set; }
        public string ChangeType { get; set; } = string.Empty;
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
        public string PackageName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal BaseAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string? ChangeType { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        /// <summary>Raw status last observed from PayOS (PAID, PENDING, CANCELLED, EXPIRED), or null if not queried.</summary>
        public string? ProviderStatus { get; set; }

        /// <summary>True when Status will not change without a brand-new purchase (Active, Cancelled, Expired).</summary>
        public bool IsFinal { get; set; }

        /// <summary>True when the owner can still complete or retry this specific pending payment.</summary>
        public bool CanResumePayment { get; set; }

        /// <summary>Checkout URL for the still-valid pending payment link, if one was (re)issued.</summary>
        public string? CheckoutUrl { get; set; }

        /// <summary>Human-readable explanation of the current status for the FE to display verbatim.</summary>
        public string Message { get; set; } = string.Empty;
    }

    public class CancelPaymentResponse
    {
        public Guid SubscriptionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}

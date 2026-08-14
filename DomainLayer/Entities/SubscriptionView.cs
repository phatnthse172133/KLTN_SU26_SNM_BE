using System;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities
{
    public class SubscriptionView
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public PackageType PackageType { get; set; }
        public Guid PackageId { get; set; }
        public string PackageCode { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string? PackageImageUrl { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public SubscriptionStatus Status { get; set; }
        public string? AdminNotes { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string? ChangeType { get; set; }
        public string? PreviousPackageName { get; set; }
        public long? PayOSOrderCode { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? BuyerName { get; set; }
        public string? BuyerEmail { get; set; }
        public string? BuyerPhone { get; set; }
    }
}

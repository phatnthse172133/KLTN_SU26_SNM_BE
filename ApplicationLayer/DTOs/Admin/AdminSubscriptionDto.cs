using System;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Admin
{
    public class AdminSubscriptionDto
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public PackageType PackageType { get; set; } // Booth = 0, Market = 1
        public Guid PackageId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public SubscriptionStatus Status { get; set; }
        public string? AdminNotes { get; set; }
        public decimal PaidAmount { get; set; }
        public long? PayOSOrderCode { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? BuyerName { get; set; }
        public string? BuyerEmail { get; set; }
        public string? BuyerPhone { get; set; }
    }

    public class VerifySubscriptionRequest
    {
        public bool IsApproved { get; set; }
        public string? AdminNotes { get; set; }
    }
}

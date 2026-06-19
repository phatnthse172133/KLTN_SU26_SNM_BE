using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

// Lịch sử đăng ký gói dịch vụ của gian hàng
public partial class BoothSubscription
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public Guid PackageId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    // Active | Expired | Cancelled
    public BoothSubscriptionStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual SubscriptionPackage Package { get; set; } = null!;
}

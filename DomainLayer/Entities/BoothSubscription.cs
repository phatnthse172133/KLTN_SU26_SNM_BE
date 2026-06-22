using System;
using DomainLayer.Enums;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Lịch sử đăng ký gói dịch vụ của gian hàng
/// </summary>
public partial class BoothSubscription
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public Guid PackageId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    /// <summary>
    /// Active | Expired | Cancelled
    /// </summary>
    public SubscriptionStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual Package Package { get; set; } = null!;
}

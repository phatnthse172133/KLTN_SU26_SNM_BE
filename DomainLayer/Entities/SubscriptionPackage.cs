using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Danh sách các gói thuê bao dịch vụ mà gian hàng có thể mua
/// </summary>
public partial class SubscriptionPackage
{
    public Guid Id { get; set; }

    public string PackageName { get; set; } = null!;

    public decimal Price { get; set; }

    public int DurationDays { get; set; }

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<BoothSubscription> BoothSubscriptions { get; set; } = new List<BoothSubscription>();
}

using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Danh sách các gói thuê bao dịch vụ mà gian hàng có thể mua
public partial class SubscriptionPackage
{
    public Guid Id { get; set; }

    public string PackageName { get; set; } = null!;

    public decimal Price { get; set; }

    public int DurationDays { get; set; }

    public string? Description { get; set; }

    public SubscriptionPackageStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<BoothSubscription> BoothSubscriptions { get; set; } = new List<BoothSubscription>();
}

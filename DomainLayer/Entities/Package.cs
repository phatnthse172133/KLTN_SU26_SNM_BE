using System;
using System.Collections.Generic;
using DomainLayer.Common;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

public partial class Package : ISoftDelete
{
    public Guid Id { get; set; }
    public string PackageName { get; set; } = null!;
    public string? Code { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public PackageType Type { get; set; }
    public string? Description { get; set; }
    public string? Entitlements { get; set; }
    public PackageStatus Status { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<PackagePrice> PackagePrices { get; set; } = new List<PackagePrice>();
    public virtual ICollection<PackagePolicy> Policies { get; set; } = new List<PackagePolicy>();
    public virtual ICollection<BoothSubscription> BoothSubscriptions { get; set; } = new List<BoothSubscription>();
    public virtual ICollection<MarketSubscription> MarketSubscriptions { get; set; } = new List<MarketSubscription>();
}

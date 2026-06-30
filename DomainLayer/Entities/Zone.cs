using System;
using System.Collections.Generic;
using DomainLayer.Common;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

public partial class Zone : ISoftDelete
{
    public Guid Id { get; set; }
    public Guid NightMarketId { get; set; }
    public string ZoneName { get; set; } = null!;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public ZoneStatus Status { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual NightMarket NightMarket { get; set; } = null!;
    public virtual ICollection<BoothRegistration> BoothRegistrations { get; set; } = new List<BoothRegistration>();
    public virtual ICollection<BoothLocation> BoothLocations { get; set; } = new List<BoothLocation>();
    public virtual ICollection<LayoutNode> LayoutNodes { get; set; } = new List<LayoutNode>();
}

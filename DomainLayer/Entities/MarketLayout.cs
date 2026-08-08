using System;
using System.Collections.Generic;
using DomainLayer.Common;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Sơ đồ mặt bằng của một chợ đêm - dùng làm nền để đặt các điểm (LayoutNodes) và gian hàng (BoothLocations)
public partial class MarketLayout : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid NightMarketId { get; set; }

    public string LayoutName { get; set; } = null!;

    public int Version { get; set; }

    public string? LayoutImageUrl { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    // Optional physical dimensions used by the auto-layout generator.  Existing
    // pixel-based layouts remain valid while these values are null.
    public double? MarketWidthMeters { get; set; }

    public double? MarketLengthMeters { get; set; }

    public double? PixelsPerMeter { get; set; }

    public LayoutCoordinateUnit CoordinateUnit { get; set; }

    public decimal? MetersPerLayoutUnit { get; set; }

    public DistanceCalibrationStatus DistanceCalibrationStatus { get; set; }

    public int GraphRevision { get; set; }
    public MarketLayoutStatus Status { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<BoothLocation> BoothLocations { get; set; } = new List<BoothLocation>();

    public virtual ICollection<LayoutNode> LayoutNodes { get; set; } = new List<LayoutNode>();

    public virtual ICollection<LayoutEdge> LayoutEdges { get; set; } = new List<LayoutEdge>();

    public virtual ICollection<LayoutNavigationAnchor> NavigationAnchors { get; set; } = new List<LayoutNavigationAnchor>();

    public virtual NightMarket NightMarket { get; set; } = null!;
}

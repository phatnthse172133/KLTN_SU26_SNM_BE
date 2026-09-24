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

    // Transitional aggregate ownership. NightMarketId is intentionally kept for
    // compatibility and must equal MarketMap.NightMarketId.
    public Guid MarketMapId { get; set; }

    public string LayoutName { get; set; } = null!;

    // Layouts sharing SectionCode are versions of the same physical map section.
    public string SectionCode { get; set; } = "MAIN";
    public string SectionName { get; set; } = "Main Area";
    public string? Description { get; set; }
    public double OffsetXMeters { get; set; }
    public double OffsetYMeters { get; set; }
    public bool IsDefaultView { get; set; }
    public int DisplayOrder { get; set; }
    public Guid? BasedOnLayoutId { get; set; }

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

    public virtual MarketMap MarketMap { get; set; } = null!;

    public virtual NightMarket NightMarket { get; set; } = null!;
}

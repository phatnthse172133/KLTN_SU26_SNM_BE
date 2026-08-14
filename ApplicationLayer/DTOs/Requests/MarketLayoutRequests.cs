using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class MarketLayoutListRequest : PaginationReq
{
    [StringLength(200)]
    public string? Keyword { get; set; }

    public MarketLayoutStatus? Status { get; set; }

    [RegularExpression("(?i)^(name|version|status|createdAt|updatedAt)$")]
    public string SortBy { get; set; } = "createdAt";

    [RegularExpression("(?i)^(asc|desc)$")]
    public string SortDirection { get; set; } = "desc";
}

public class CreateMarketLayoutRequest
{
    [Required, StringLength(150)]
    public string LayoutName { get; set; } = string.Empty;
}

public class UpdateMarketLayoutRequest : CreateMarketLayoutRequest { }

public class UpdateMarketLayoutImageRequest
{
    [Required, StringLength(1000)]
    public string LayoutImageUrl { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Width { get; set; }

    [Range(1, int.MaxValue)]
    public int Height { get; set; }
}

public class CloneMarketLayoutDraftRequest
{
    [StringLength(150)]
    public string? LayoutName { get; set; }
}

public class UpdateMarketLayoutDimensionsRequest
{
    [Range(1, int.MaxValue)]
    public int Width { get; set; }

    [Range(1, int.MaxValue)]
    public int Height { get; set; }
}

public class SaveGraphNode
{
    public Guid Id { get; set; }
    public Guid? ZoneId { get; set; }
    [StringLength(100)] public string? NodeName { get; set; }
    [Required] public LayoutNodeType NodeType { get; set; }
    [Range(0, double.MaxValue)] public decimal XCoordinate { get; set; }
    [Range(0, double.MaxValue)] public decimal YCoordinate { get; set; }
    public bool IsAccessible { get; set; } = true;
    public bool IsStartingPoint { get; set; }

    // Cinema-layout BoothSlot metadata
    [StringLength(50)] public string? SlotCode { get; set; }
    public int? RowIndex { get; set; }
    public int? ColumnIndex { get; set; }
    public Guid? LayoutBlockId { get; set; }
}

public class SaveGraphEdge
{
    public Guid Id { get; set; }
    [Required] public Guid FromNodeId { get; set; }
    [Required] public Guid ToNodeId { get; set; }
    [Range(0.01, double.MaxValue)] public decimal? Distance { get; set; }
    public bool IsBidirectional { get; set; } = true;
    public bool IsAccessible { get; set; } = true;
}

/// <summary>Block position update — only X/Y can be moved by FE drag-drop.</summary>
public class SaveGraphBlock
{
    public Guid Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

public class SaveGraphRequest
{
    [Required]
    public DateTime ExpectedUpdatedAt { get; set; }

    [Required]
    public List<SaveGraphNode> Nodes { get; set; } = new();

    [Required]
    public List<SaveGraphEdge> Edges { get; set; } = new();

    /// <summary>Optional: updated positions of Zone blocks after drag-drop.</summary>
    public List<SaveGraphBlock> Blocks { get; set; } = new();
}

// ──────────────────────────────────────────────
// Grid Generation Request/Response DTOs
// ──────────────────────────────────────────────

/// <summary>Per-Zone configuration for the grid generator.</summary>
public class ZoneGenerationConfig
{
    /// <summary>Existing zone to regenerate. null (or Guid.Empty) = create a new zone at apply time.</summary>
    public Guid? ZoneId { get; set; }

    /// <summary>Zone name — required for new zones; renames an existing zone when provided.</summary>
    [StringLength(150)]
    public string? ZoneName { get; set; }

    /// <summary>Zone type (e.g. Food/Drink/Dessert) — stored as the zone description.</summary>
    [StringLength(100)]
    public string? ZoneType { get; set; }

    /// <summary>Capacity — required (&gt; 0) for new zones; updates an existing zone when provided.</summary>
    [Range(1, 1000)]
    public int? Capacity { get; set; }

    /// <summary>Width of each booth slot in canvas pixels.</summary>
    [Range(10, 500)]
    public double BoothWidth { get; set; } = 80;

    /// <summary>Height of each booth slot in canvas pixels.</summary>
    [Range(10, 500)]
    public double BoothHeight { get; set; } = 60;

    /// <summary>Gap between adjacent booth slots (pixels).</summary>
    [Range(0, 200)]
    public double Gap { get; set; } = 20;

    /// <summary>Width of the aisle between row groups (pixels).</summary>
    [Range(0, 500)]
    public double AisleWidth { get; set; } = 40;

    /// <summary>Fixed number of columns. null = auto (ceil(sqrt(capacity))).</summary>
    public int? Columns { get; set; }

    /// <summary>Add aisle nodes between every N rows (0 = no aisles).</summary>
    public int AisleEveryNRows { get; set; } = 0;

    [Range(1, 1000)]
    public double? ZoneWidthMeters { get; set; }

    [Range(1, 1000)]
    public double? ZoneLengthMeters { get; set; }

    [Range(0.5, 100)]
    public double? BoothWidthMeters { get; set; }

    [Range(0.5, 100)]
    public double? BoothLengthMeters { get; set; }

    [Range(0, 50)]
    public double? HorizontalGapMeters { get; set; }

    [Range(0, 50)]
    public double? VerticalGapMeters { get; set; }
}
public class GenerateLayoutRequest
{
    [Required]
    public DateTime ExpectedUpdatedAt { get; set; }

    public List<ZoneGenerationConfig> ZoneConfigs { get; set; } = new();

    /// <summary>
    /// Total number of booth slots requested by the market owner. During physical
    /// generation the value is safely reduced to the number that fits inside the
    /// declared market boundary; it is never increased beyond this request.
    /// </summary>
    [Range(1, 1000)]
    public int? RequestedBoothCount { get; set; }

    /// <summary>Capacity of the single default block generated for markets without zone management.</summary>
    [Range(1, 1000)]
    public int? DefaultZoneCapacity { get; set; }

    /// <summary>Booth slot width for the default block (markets without zone management).</summary>
    [Range(10, 500)]
    public double? DefaultBoothWidth { get; set; }

    /// <summary>Booth slot height for the default block (markets without zone management).</summary>
    [Range(10, 500)]
    public double? DefaultBoothHeight { get; set; }

    /// <summary>Gap between adjacent booth slots in the default block (pixels).</summary>
    [Range(0, 200)]
    public double? DefaultGap { get; set; }

    /// <summary>Aisle width for the default block (pixels).</summary>
    [Range(0, 500)]
    public double? DefaultAisleWidth { get; set; }

    /// <summary>Fixed number of columns for the default block. null = auto.</summary>
    public int? DefaultColumns { get; set; }

    /// <summary>Add aisle nodes between every N rows in the default block (0 = no aisles).</summary>
    public int? DefaultAisleEveryNRows { get; set; }

    /// <summary>Starting X offset for the first Zone block.</summary>
    [Range(0, 5000)]
    public double StartX { get; set; } = 20;

    /// <summary>Starting Y offset for the first Zone block.</summary>
    [Range(0, 5000)]
    public double StartY { get; set; } = 20;

    /// <summary>Margin between adjacent Zone blocks (pixels).</summary>
    [Range(0, 500)]
    public double ZoneMargin { get; set; } = 50;

    /// <summary>How many Zone blocks per row. null = auto (ceil(sqrt(zoneCount))).</summary>
    public int? ZonesPerRow { get; set; }

    /// <summary>Auto expand canvas Width/Height to fit all zones.</summary>
    public bool AutoExpandCanvas { get; set; } = true;

    /// <summary>Automatically derives zone dimensions and placement from the market boundary.</summary>
    public bool AutoFitZones { get; set; }

    [Range(1, 5000)]
    public double? MarketWidthMeters { get; set; }

    [Range(1, 5000)]
    public double? MarketLengthMeters { get; set; }

    [Range(2, 50)]
    public double PixelsPerMeter { get; set; } = 10;

    [Range(0, 100)]
    public double StartXMeters { get; set; } = 1;

    [Range(0, 100)]
    public double StartYMeters { get; set; } = 1;

    [Range(0, 100)]
    public double ZoneMarginMeters { get; set; } = 2;

    [Range(1, 5000)]
    public double? DefaultZoneWidthMeters { get; set; }

    [Range(1, 5000)]
    public double? DefaultZoneLengthMeters { get; set; }

    [Range(0.5, 100)]
    public double? DefaultBoothWidthMeters { get; set; }

    [Range(0.5, 100)]
    public double? DefaultBoothLengthMeters { get; set; }

    [Range(0, 50)]
    public double? DefaultHorizontalGapMeters { get; set; }

    [Range(0, 50)]
    public double? DefaultVerticalGapMeters { get; set; }
}

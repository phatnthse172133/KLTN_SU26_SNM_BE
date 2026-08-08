using DomainLayer.Entities;

namespace ApplicationLayer.DTOs.Responses;

public class MarketLayoutResponse
{
    public Guid Id { get; set; }
    public Guid NightMarketId { get; set; }
    public string LayoutName { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? LayoutImageUrl { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string CoordinateUnit { get; set; } = string.Empty;
    public decimal? MetersPerLayoutUnit { get; set; }
    public string DistanceCalibrationStatus { get; set; } = string.Empty;
    public int GraphRevision { get; set; }
    public double? MarketWidthMeters { get; set; }
    public double? MarketLengthMeters { get; set; }
    public double? PixelsPerMeter { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class LayoutBlockResponse
{
    public Guid Id { get; set; }
    public Guid LayoutId { get; set; }
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public string? ZoneCode { get; set; }
    public string? ZoneColor { get; set; }
    public string? ZoneType { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }
    public int Capacity { get; set; }
    public int SlotCount { get; set; }
    public int DisplayOrder { get; set; }
    public string? ConfigJson { get; set; }
}

public class MarketLayoutEditorDataResponse
{
    public MarketLayoutResponse Layout { get; set; } = new();
    public IReadOnlyCollection<ZoneResponse> Zones { get; set; } = [];
    public IReadOnlyCollection<LayoutBlockResponse> Blocks { get; set; } = [];
    public IReadOnlyCollection<LayoutNodeResponse> Nodes { get; set; } = [];
    public IReadOnlyCollection<LayoutEdgeResponse> Edges { get; set; } = [];
    public IReadOnlyCollection<BoothLocationResponse> BoothLocations { get; set; } = [];
}

public class LayoutNodeResponse
{
    public Guid Id { get; set; }
    public Guid LayoutId { get; set; }
    public Guid? ZoneId { get; set; }
    public string? NodeName { get; set; }
    public string NodeType { get; set; } = string.Empty;
    public decimal XCoordinate { get; set; }
    public decimal YCoordinate { get; set; }
    public bool IsAccessible { get; set; }
    public bool IsStartingPoint { get; set; }

    // Cinema-layout BoothSlot metadata
    public string? SlotCode { get; set; }
    public int? RowIndex { get; set; }
    public int? ColumnIndex { get; set; }
    public Guid? LayoutBlockId { get; set; }
}

public class LayoutEdgeResponse
{
    public Guid Id { get; set; }
    public Guid LayoutId { get; set; }
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public decimal Distance { get; set; }
    public decimal DistanceMeters { get; set; }
    public bool IsBidirectional { get; set; }
    public bool IsAccessible { get; set; }
}

public class BoothLocationResponse
{
    public Guid Id { get; set; }
    public Guid BoothId { get; set; }
    public Guid LayoutId { get; set; }
    public Guid LayoutNodeId { get; set; }
    public Guid? ZoneId { get; set; }
    public string? SlotNumber { get; set; }
    public decimal XCoordinate { get; set; }
    public decimal YCoordinate { get; set; }
    public DateTime? ReleasedAt { get; set; }
}

public class MarketLayoutValidationResponse
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyCollection<string> Errors { get; init; } = [];
    public IReadOnlyCollection<string> Warnings { get; init; } = [];
}

// ──────────────────────────────────────────────
// Grid Generation Preview Response DTOs
// ──────────────────────────────────────────────

public class PreviewSlot
{
    public string SlotCode { get; set; } = string.Empty;
    public int RowIndex { get; set; }
    public int ColumnIndex { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool HasAssignedBooth { get; set; }
}

public class GenerationPreviewZoneResult
{
    public Guid? ZoneId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public string? ZoneCode { get; set; }
    public string? Color { get; set; }
    public string? ZoneType { get; set; }
    public int Capacity { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int Columns { get; set; }
    public int Rows { get; set; }
    public double? ZoneWidthMeters { get; set; }
    public double? ZoneLengthMeters { get; set; }
    public double? BoothWidthMeters { get; set; }
    public double? BoothLengthMeters { get; set; }
    public double? HorizontalGapMeters { get; set; }
    public double? VerticalGapMeters { get; set; }
    public List<PreviewSlot> Slots { get; set; } = new();
}

public class ConflictingSlot
{
    public string SlotCode { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public string BoothName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class GenerationPreviewResponse
{
    public bool CanApply { get; set; }
    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }
    public List<GenerationPreviewZoneResult> Zones { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<ConflictingSlot> ConflictingSlots { get; set; } = new();
    public List<LayoutNodeResponse> Nodes { get; set; } = new();
    public List<LayoutEdgeResponse> Edges { get; set; } = new();
    public List<LayoutBlockResponse> Blocks { get; set; } = new();
}

namespace ApplicationLayer.DTOs.Responses;

public sealed class CustomerMarketMapResponse
{
    public Guid MarketId { get; set; }
    public Guid MarketMapId { get; set; }
    public string MarketMapName { get; set; } = string.Empty;
    public int MarketMapVersion { get; set; }
    public DateTime PublishedAt { get; set; }
    public int LayoutCount { get; set; }
    public int ZoneCount { get; set; }
    public int BoothSlotCount { get; set; }
    public Guid? DefaultLayoutId { get; set; }
    public MarketMapOverallBoundsResponse OverallBounds { get; set; } = new();
    public IReadOnlyCollection<CustomerMarketMapLayoutResponse> Layouts { get; set; } = [];
}

public sealed class CustomerMarketMapLayoutResponse
{
    public Guid LayoutId { get; set; }
    public string LayoutName { get; set; } = string.Empty;
    public string SectionCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; }
    public int GraphRevision { get; set; }
    public double OffsetXMeters { get; set; }
    public double OffsetYMeters { get; set; }
    public double PhysicalWidthMeters { get; set; }
    public double PhysicalHeightMeters { get; set; }
    public double ScaleX { get; set; }
    public double ScaleY { get; set; }
    public string ScaleSource { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsDefaultView { get; set; }
    public string? ImageUrl { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string CoordinateUnit { get; set; } = string.Empty;
    public decimal? MetersPerLayoutUnit { get; set; }
    public string DistanceCalibrationStatus { get; set; } = string.Empty;
    public double? PixelsPerMeter { get; set; }
    public IReadOnlyCollection<CustomerMapZoneResponse> Zones { get; set; } = [];
    public IReadOnlyCollection<CustomerOverallMapBlockResponse> Blocks { get; set; } = [];
    public IReadOnlyCollection<CustomerOverallMapNodeResponse> Nodes { get; set; } = [];
    public IReadOnlyCollection<LayoutEdgeResponse> Edges { get; set; } = [];
    public IReadOnlyCollection<CustomerOverallMapNodeResponse> StartingPoints { get; set; } = [];
    public IReadOnlyCollection<NavigationEntranceResponse> Anchors { get; set; } = [];
    public IReadOnlyCollection<CustomerOverallMapBoothResponse> Booths { get; set; } = [];
}

public sealed class CustomerMapZoneResponse
{
    public Guid Id { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? ZoneCode { get; set; }
}

public sealed class CustomerOverallMapBlockResponse
{
    public Guid Id { get; set; }
    public Guid LayoutId { get; set; }
    public Guid? ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Color { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }
    public double GlobalXMeters { get; set; }
    public double GlobalYMeters { get; set; }
    public double PhysicalWidthMeters { get; set; }
    public double PhysicalHeightMeters { get; set; }
}

public sealed class CustomerOverallMapNodeResponse
{
    public Guid Id { get; set; }
    public Guid LayoutId { get; set; }
    public Guid? ZoneId { get; set; }
    public string? NodeName { get; set; }
    public string NodeType { get; set; } = string.Empty;
    public decimal XCoordinate { get; set; }
    public decimal YCoordinate { get; set; }
    public double GlobalXMeters { get; set; }
    public double GlobalYMeters { get; set; }
    public bool IsAccessible { get; set; }
    public bool IsStartingPoint { get; set; }
    public string? SlotCode { get; set; }
    public int? RowIndex { get; set; }
    public int? ColumnIndex { get; set; }
    public Guid? LayoutBlockId { get; set; }
}

public sealed class CustomerOverallMapBoothResponse
{
    public Guid MarketMapId { get; set; }
    public Guid LayoutId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public Guid NodeId { get; set; }
    public Guid? ZoneId { get; set; }
    public string? SlotNumber { get; set; }
    public string? SlotCode { get; set; }
    public string? ZoneName { get; set; }
    public decimal XCoordinate { get; set; }
    public decimal YCoordinate { get; set; }
    public double GlobalXMeters { get; set; }
    public double GlobalYMeters { get; set; }
}

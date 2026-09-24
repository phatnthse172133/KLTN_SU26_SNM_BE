namespace ApplicationLayer.DTOs.Responses;

public sealed class MarketMapSummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int LayoutCount { get; set; }
}

public sealed class MarketMapLayoutSummaryResponse
{
    public Guid Id { get; set; }
    public string LayoutName { get; set; } = string.Empty;
    public string SectionCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double? PhysicalWidthMeters { get; set; }
    public double? PhysicalHeightMeters { get; set; }
    public int GraphRevision { get; set; }
    public double OffsetXMeters { get; set; }
    public double OffsetYMeters { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsDefaultView { get; set; }
    public int SlotCount { get; set; }
}

public sealed class EligibleMarketLayoutResponse
{
    public Guid LayoutId { get; set; }
    public string LayoutName { get; set; } = string.Empty;
    public string SectionCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int Version { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double? PhysicalWidthMeters { get; set; }
    public double? PhysicalHeightMeters { get; set; }
    public int GraphRevision { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public int SlotCount { get; set; }
}

public sealed class MarketMapDetailResponse
{
    public Guid Id { get; set; }
    public Guid NightMarketId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyCollection<MarketMapLayoutSummaryResponse> Layouts { get; set; } = [];
}

public sealed class MarketMapOverallBoundsResponse
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double OverallWidthMeters { get; set; }
    public double OverallHeightMeters { get; set; }
}

public sealed class MarketMapLayoutPreviewResponse
{
    public Guid LayoutId { get; set; }
    public string LayoutName { get; set; } = string.Empty;
    public string SectionCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public double OffsetXMeters { get; set; }
    public double OffsetYMeters { get; set; }
    public double? PhysicalWidthMeters { get; set; }
    public double? PhysicalHeightMeters { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsDefaultView { get; set; }
    public int ZoneCount { get; set; }
    public int BoothSlotCount { get; set; }
}

public sealed class MarketMapValidationIssueResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public Guid? LayoutId { get; set; }
    public string? SectionCode { get; set; }
    public Guid? NodeId { get; set; }
    public Guid? ZoneId { get; set; }
}

public sealed class MarketMapValidationResponse
{
    public bool IsValid => Errors.Count == 0;
    public bool CanActivate => IsValid;
    public IReadOnlyCollection<MarketMapValidationIssueResponse> Errors { get; set; } = [];
    public IReadOnlyCollection<MarketMapValidationIssueResponse> Warnings { get; set; } = [];
}

public sealed class MarketMapValidationSummaryResponse
{
    public bool IsValid { get; set; }
    public bool CanActivate { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}

public sealed class MarketMapPreviewResponse
{
    public Guid MarketMapId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public MarketMapOverallBoundsResponse? OverallBounds { get; set; }
    public int LayoutCount { get; set; }
    public int ZoneCount { get; set; }
    public int BoothSlotCount { get; set; }
    public IReadOnlyCollection<MarketMapLayoutPreviewResponse> Layouts { get; set; } = [];
    public MarketMapValidationSummaryResponse ValidationSummary { get; set; } = new();
}

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
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MarketLayoutEditorDataResponse
{
    public MarketLayoutResponse Layout { get; set; } = new();
    public IReadOnlyCollection<ZoneResponse> Zones { get; set; } = [];
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
}

public class LayoutEdgeResponse
{
    public Guid Id { get; set; }
    public Guid LayoutId { get; set; }
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public decimal Distance { get; set; }
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

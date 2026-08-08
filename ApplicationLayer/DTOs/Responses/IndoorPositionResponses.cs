namespace ApplicationLayer.DTOs.Responses;

public class IndoorPositionEstimateResponse
{
    public Guid LayoutId { get; set; }
    public int LayoutVersion { get; set; }
    public int GraphRevision { get; set; }
    public string Source { get; set; } = string.Empty;
    public decimal LayoutX { get; set; }
    public decimal LayoutY { get; set; }
    public decimal SnappedX { get; set; }
    public decimal SnappedY { get; set; }
    public Guid? SnappedNodeId { get; set; }
    public Guid? SnappedEdgeId { get; set; }
    public Guid? FromNodeId { get; set; }
    public Guid? ToNodeId { get; set; }
    public decimal? EdgeProgress { get; set; }
    public decimal DistanceFromGraphLayoutUnits { get; set; }
    public decimal? DistanceFromGraphMeters { get; set; }
    public string Confidence { get; set; } = string.Empty;
    public decimal? AccuracyMeters { get; set; }
    public Guid? AnchorId { get; set; }
    public DateTime CapturedAt { get; set; }
}

public class VirtualOriginRouteResponse
{
    public Guid LayoutId { get; set; }
    public int LayoutVersion { get; set; }
    public int GraphRevision { get; set; }
    public Guid OriginEdgeId { get; set; }
    public decimal OriginEdgeProgress { get; set; }
    public RouteDestinationResponse Destination { get; set; } = new();
    public decimal TotalDistanceMeters { get; set; }
    public int? EstimatedWalkingMinutes { get; set; }
    public bool IsDistanceCalibrated { get; set; }
    public string DistanceCalibrationStatus { get; set; } = string.Empty;
    public IReadOnlyCollection<VirtualRoutePointResponse> Path { get; set; } = [];
    public IReadOnlyCollection<Guid> TraversedEdgeIds { get; set; } = [];
}

public class VirtualRoutePointResponse
{
    public int Sequence { get; set; }
    public Guid? NodeId { get; set; }
    public decimal XCoordinate { get; set; }
    public decimal YCoordinate { get; set; }
    public bool IsVirtualOrigin { get; set; }
}

using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class MapListRequest : PaginationReq
{
    [StringLength(200)] public string? Keyword { get; set; }
}

public class CreateLayoutNodeRequest
{
    public Guid? ZoneId { get; set; }
    [StringLength(100)] public string? NodeName { get; set; }
    [Required] public LayoutNodeType NodeType { get; set; }
    [Range(0, double.MaxValue)] public decimal XCoordinate { get; set; }
    [Range(0, double.MaxValue)] public decimal YCoordinate { get; set; }
    public bool IsAccessible { get; set; } = true;
    public bool IsStartingPoint { get; set; }

    // Cinema-layout metadata. SlotCode is required by the service for BoothSlot nodes.
    [StringLength(50)] public string? SlotCode { get; set; }
    [Range(0, int.MaxValue)] public int? RowIndex { get; set; }
    [Range(0, int.MaxValue)] public int? ColumnIndex { get; set; }
    public Guid? LayoutBlockId { get; set; }
}

public class UpdateLayoutNodeRequest : CreateLayoutNodeRequest { }

public class UpdateLayoutNodePositionRequest
{
    [Range(0, double.MaxValue)] public decimal XCoordinate { get; set; }
    [Range(0, double.MaxValue)] public decimal YCoordinate { get; set; }
    /// <summary>
    /// When the requested grid cell is occupied, swap with this booth slot in
    /// one transaction instead of creating an overlap.
    /// </summary>
    public Guid? SwapWithNodeId { get; set; }
}

public class UpdateAccessibilityRequest
{
    public bool IsAccessible { get; set; }
}

public class CreateLayoutEdgeRequest
{
    [Required] public Guid FromNodeId { get; set; }
    [Required] public Guid ToNodeId { get; set; }
    [Range(0.01, double.MaxValue)] public decimal? Distance { get; set; }
    [Range(0.01, double.MaxValue)] public decimal? DistanceMeters { get; set; }
    public bool IsBidirectional { get; set; } = true;
    public bool IsAccessible { get; set; } = true;
}

public class UpdateLayoutCalibrationRequest
{
    [Range(0.000001, double.MaxValue)]
    public decimal MetersPerLayoutUnit { get; set; }
}

public class UpdateLayoutEdgeRequest : CreateLayoutEdgeRequest { }

public class AssignBoothLocationRequest
{
    [Required] public Guid LayoutId { get; set; }
    [Required] public Guid LayoutNodeId { get; set; }
    public Guid? ZoneId { get; set; }
    [StringLength(50)] public string? SlotNumber { get; set; }
}

public class NearestNodeRequest
{
    [Range(0, double.MaxValue)] public decimal XCoordinate { get; set; }
    [Range(0, double.MaxValue)] public decimal YCoordinate { get; set; }
}

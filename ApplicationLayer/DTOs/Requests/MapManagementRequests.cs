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
}

public class UpdateLayoutNodeRequest : CreateLayoutNodeRequest { }

public class UpdateLayoutNodePositionRequest
{
    [Range(0, double.MaxValue)] public decimal XCoordinate { get; set; }
    [Range(0, double.MaxValue)] public decimal YCoordinate { get; set; }
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
    public bool IsBidirectional { get; set; } = true;
    public bool IsAccessible { get; set; } = true;
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

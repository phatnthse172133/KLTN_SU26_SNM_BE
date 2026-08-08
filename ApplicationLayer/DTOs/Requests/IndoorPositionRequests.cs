using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class SnapIndoorPositionRequest
{
    [Range(0, double.MaxValue)] public decimal X { get; set; }
    [Range(0, double.MaxValue)] public decimal Y { get; set; }
    [Required] public IndoorPositionSource Source { get; set; } = IndoorPositionSource.MapTap;
    [Range(1, int.MaxValue)] public int ExpectedLayoutVersion { get; set; }
    [Range(1, int.MaxValue)] public int ExpectedGraphRevision { get; set; }
    [Range(0.01, 100)] public decimal? MaximumSnapDistanceMeters { get; set; }
    [Range(0.01, 1000)] public decimal? MaximumSnapDistanceLayoutUnits { get; set; }
}

public class RouteFromSnappedPositionRequest
{
    [Required] public Guid SnappedEdgeId { get; set; }
    [Range(0, 1)] public decimal EdgeProgress { get; set; }
    [Range(0, double.MaxValue)] public decimal SnappedX { get; set; }
    [Range(0, double.MaxValue)] public decimal SnappedY { get; set; }
    [Required] public Guid BoothId { get; set; }
    [Range(1, int.MaxValue)] public int ExpectedLayoutVersion { get; set; }
    [Range(1, int.MaxValue)] public int ExpectedGraphRevision { get; set; }
}

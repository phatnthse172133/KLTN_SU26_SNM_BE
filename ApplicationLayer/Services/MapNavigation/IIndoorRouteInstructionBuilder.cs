using ApplicationLayer.DTOs.Responses;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.MapNavigation;

public interface IIndoorRouteInstructionBuilder
{
    IReadOnlyCollection<RouteInstructionResponse> Build(
        IReadOnlyList<Guid> orderedNodeIds, IReadOnlyList<Guid> traversedEdgeIds,
        IReadOnlyDictionary<Guid, LayoutNode> nodes, IReadOnlyDictionary<Guid, LayoutEdge> edges,
        decimal minimumSegmentMeters = 1m, LayoutNode? destinationBooth = null);
}

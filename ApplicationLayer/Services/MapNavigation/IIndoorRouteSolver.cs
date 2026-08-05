using DomainLayer.Entities;

namespace ApplicationLayer.Services.MapNavigation;

public sealed record IndoorRoutePolicy(bool AccessibleOnly = true);

public sealed record IndoorRouteSolution(
    IReadOnlyList<Guid> NodeIds,
    IReadOnlyList<Guid> EdgeIds,
    decimal TotalDistanceMeters)
{
    public static IndoorRouteSolution NotFound { get; } = new([], [], 0);
    public bool Found => NodeIds.Count > 0;
}

public interface IIndoorRouteSolver
{
    IndoorRouteSolution Solve(
        IReadOnlyCollection<LayoutNode> nodes,
        IReadOnlyCollection<LayoutEdge> edges,
        Guid sourceNodeId,
        Guid destinationNodeId,
        IndoorRoutePolicy policy);
}

public static class IndoorGraphPolicy
{
    public static bool CanUseNode(LayoutNode node, IndoorRoutePolicy policy)
        => !node.IsDeleted && (!policy.AccessibleOnly || node.IsAccessible);

    public static bool CanUseEdge(LayoutEdge edge, IndoorRoutePolicy policy)
        => !edge.IsDeleted && edge.Distance > 0 && (!policy.AccessibleOnly || edge.IsAccessible);
}

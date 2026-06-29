using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketLayouts;

public class LayoutGraphValidationService : ILayoutGraphValidationService
{
    private readonly IMarketLayoutRepository _layouts;
    private readonly ILayoutNodeRepository _nodes;
    private readonly ILayoutEdgeRepository _edges;
    private readonly IBoothLocationRepository _locations;
    public LayoutGraphValidationService(IMarketLayoutRepository layouts, ILayoutNodeRepository nodes,
        ILayoutEdgeRepository edges, IBoothLocationRepository locations)
        => (_layouts, _nodes, _edges, _locations) = (layouts, nodes, edges, locations);

    public async Task<MarketLayoutValidationResponse> ValidateAsync(Guid layoutId, CancellationToken cancellationToken = default)
    {
        var layout = await _layouts.GetActiveByIdAsync(layoutId, cancellationToken)
            ?? throw AppException.NotFound("Market layout was not found.");
        var nodes = await _nodes.GetByLayoutAsync(layoutId, cancellationToken: cancellationToken);

        var edges = await _edges.GetByLayoutAsync(layoutId, cancellationToken: cancellationToken);

        var locations = await _locations.GetCurrentByLayoutAsync(layoutId, cancellationToken);

        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(layout.LayoutImageUrl)) 
            errors.Add("Layout image is required.");

        if (layout.Width <= 0 || layout.Height <= 0) 
            errors.Add("Layout width and height must be greater than zero.");

        if (!nodes.Any(x => x.NodeType == LayoutNodeType.Entrance)) 
            errors.Add("Layout must have at least one entrance.");

        if (!nodes.Any(x => x.NodeType == LayoutNodeType.BoothAccess)) 
            errors.Add("Layout must have at least one booth access node.");

        if (!nodes.Any(x => x.IsStartingPoint || x.NodeType is LayoutNodeType.Entrance or LayoutNodeType.Exit or LayoutNodeType.Landmark))
            errors.Add("Layout must have at least one starting point.");

        if (nodes.Any(x => x.Xcoordinate < 0 || x.Xcoordinate > layout.Width || x.Ycoordinate < 0 || x.Ycoordinate > layout.Height))
            errors.Add("All nodes must be inside the layout dimensions.");

        if (edges.Any(x => x.FromNodeId == x.ToNodeId)) 
            errors.Add("Edges cannot connect a node to itself.");

        if (edges.GroupBy(x => new { x.FromNodeId, x.ToNodeId }).Any(x => x.Count() > 1)) 
            errors.Add("Layout contains duplicate edges.");

        var nodeIds = nodes.Select(x => x.Id).ToHashSet();

        if (edges.Any(x => !nodeIds.Contains(x.FromNodeId) || !nodeIds.Contains(x.ToNodeId))) 
            errors.Add("All edge endpoints must belong to this layout.");

        var connectedIds = edges.SelectMany(x => new[] { x.FromNodeId, x.ToNodeId }).ToHashSet();

        if (nodes.Any(x => x.NodeType is LayoutNodeType.Entrance or LayoutNodeType.BoothAccess && !connectedIds.Contains(x.Id)))
            errors.Add("Required entrance and booth access nodes cannot be isolated.");

        var reachable = ReachableFromEntrances(nodes.Where(x => x.NodeType == LayoutNodeType.Entrance).Select(x => x.Id), edges);

        if (nodes.Any(x => x.NodeType == LayoutNodeType.BoothAccess && !reachable.Contains(x.Id)))
            errors.Add("Every booth access node must be reachable from an entrance.");

        if (locations.Any(x => !nodeIds.Contains(x.LayoutNodeId))) 
            errors.Add("A booth location references an invalid node.");

        if (nodes.Count == 0) 
            errors.Add("Layout must have at least one node.");

        if (edges.Count == 0) 
            warnings.Add("Layout has no paths.");

        return new() { Errors = errors.Distinct().ToList(), Warnings = warnings };
    }

    private static HashSet<Guid> ReachableFromEntrances(IEnumerable<Guid> starts, IEnumerable<DomainLayer.Entities.LayoutEdge> edges)
    {
        var graph = new Dictionary<Guid, List<Guid>>();
        foreach (var edge in edges.Where(x => x.IsAccessible))
        {
            graph.TryAdd(edge.FromNodeId, []); graph[edge.FromNodeId].Add(edge.ToNodeId);
            if (edge.IsBidirectional) { graph.TryAdd(edge.ToNodeId, []); graph[edge.ToNodeId].Add(edge.FromNodeId); }
        }
        var visited = starts.ToHashSet();
        var queue = new Queue<Guid>(visited);
        while (queue.TryDequeue(out var current))
            if (graph.TryGetValue(current, out var adjacent))
                foreach (var next in adjacent)
                    if (visited.Add(next)) queue.Enqueue(next);
        return visited;
    }
}

using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.MapNavigation;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketLayouts;

public class LayoutGraphValidationService : ILayoutGraphValidationService
{
    private readonly IMarketLayoutRepository _layouts;
    private readonly ILayoutNodeRepository _nodes;
    private readonly ILayoutEdgeRepository _edges;
    private readonly IBoothLocationRepository _locations;
    private readonly ILayoutNavigationAnchorRepository? _anchors;
    public LayoutGraphValidationService(IMarketLayoutRepository layouts, ILayoutNodeRepository nodes,
        ILayoutEdgeRepository edges, IBoothLocationRepository locations, ILayoutNavigationAnchorRepository? anchors = null)
        => (_layouts, _nodes, _edges, _locations, _anchors) = (layouts, nodes, edges, locations, anchors);

    public async Task<MarketLayoutValidationResponse> ValidateAsync(Guid layoutId, CancellationToken cancellationToken = default)
    {
        var layout = await _layouts.GetActiveByIdAsync(layoutId, cancellationToken)
            ?? throw AppException.NotFound("Market layout was not found.");
        var nodes = await _nodes.GetByLayoutAsync(layoutId, cancellationToken: cancellationToken);

        var edges = await _edges.GetByLayoutAsync(layoutId, cancellationToken: cancellationToken);

        var locations = await _locations.GetCurrentByLayoutAsync(layoutId, cancellationToken);
        var anchors = _anchors is null ? [] : await _anchors.GetByLayoutAsync(layoutId, cancellationToken);

        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(layout.LayoutImageUrl)) 
            errors.Add("Layout image is required.");

        if (layout.Width <= 0 || layout.Height <= 0) 
            errors.Add("Layout width and height must be greater than zero.");

        if (layout.DistanceCalibrationStatus == DistanceCalibrationStatus.Calibrated &&
            (!layout.MetersPerLayoutUnit.HasValue || layout.MetersPerLayoutUnit.Value <= 0))
            errors.Add("A calibrated layout must have a positive metres-per-layout-unit scale.");

        if (layout.DistanceCalibrationStatus == DistanceCalibrationStatus.Uncalibrated)
            warnings.Add("Layout distance is uncalibrated; live ETA and tracking must remain disabled.");

        if (!nodes.Any(x => x.NodeType == LayoutNodeType.Entrance && x.IsAccessible))
            errors.Add("Layout must have at least one accessible entrance.");

        if (!nodes.Any(x => x.NodeType == LayoutNodeType.BoothAccess)) 
            errors.Add("Layout must have at least one booth access node.");

        if (!nodes.Any(x => x.IsStartingPoint && x.IsAccessible))
            errors.Add("Layout must have at least one accessible starting point.");

        if (nodes.Any(x => x.NodeType == LayoutNodeType.Entrance && !x.IsAccessible))
            errors.Add("All entrance nodes must be accessible.");

        if (nodes.Any(x => x.IsStartingPoint && !x.IsAccessible))
            errors.Add("All starting-point nodes must be accessible.");

        if (nodes.Any(x => x.Xcoordinate < 0 || x.Xcoordinate > layout.Width || x.Ycoordinate < 0 || x.Ycoordinate > layout.Height))
            errors.Add("All nodes must be inside the layout dimensions.");

        if (edges.Any(x => x.FromNodeId == x.ToNodeId)) 
            errors.Add("Edges cannot connect a node to itself.");

        if (edges.Any(x => x.Distance <= 0))
            errors.Add("All edge distances must be positive physical metres.");

        if (HasDuplicateDirectedArcs(edges))
            errors.Add("Layout contains duplicate directed paths, including reversed bidirectional edges.");

        var nodeIds = nodes.Select(x => x.Id).ToHashSet();

        if (edges.Any(x => x.LayoutId != layoutId || !nodeIds.Contains(x.FromNodeId) || !nodeIds.Contains(x.ToNodeId)))
            errors.Add("All edge endpoints must belong to this layout.");

        var usableNodes = nodes.Where(x => IndoorGraphPolicy.CanUseNode(x, new IndoorRoutePolicy())).ToList();
        var usableNodeIds = usableNodes.Select(x => x.Id).ToHashSet();
        var usableEdges = edges.Where(x => IndoorGraphPolicy.CanUseEdge(x, new IndoorRoutePolicy()) &&
                                            usableNodeIds.Contains(x.FromNodeId) && usableNodeIds.Contains(x.ToNodeId)).ToList();
        var connectedIds = usableEdges.SelectMany(x => new[] { x.FromNodeId, x.ToNodeId }).ToHashSet();

        if (nodes.Any(x => (x.NodeType is LayoutNodeType.Entrance or LayoutNodeType.BoothAccess || x.IsStartingPoint) &&
                           (!x.IsAccessible || !connectedIds.Contains(x.Id))))
            errors.Add("Accessible entrances, starting points and booth access nodes cannot be isolated.");

        var reachable = ReachableFromEntrances(
            usableNodes.Where(x => x.NodeType == LayoutNodeType.Entrance).Select(x => x.Id), usableEdges, usableNodeIds);

        if (nodes.Any(x => x.NodeType == LayoutNodeType.BoothAccess && (!x.IsAccessible || !reachable.Contains(x.Id))))
            errors.Add("Every booth access node must be reachable from an entrance.");

        if (locations.Any(x => x.LayoutId != layoutId || !nodeIds.Contains(x.LayoutNodeId)))
            errors.Add("A booth location references a node outside this layout.");

        var nodesById = nodes.ToDictionary(x => x.Id);
        if (locations.Any(x => nodesById.TryGetValue(x.LayoutNodeId, out var node) &&
                               (node.NodeType != LayoutNodeType.BoothAccess || !node.IsAccessible)))
            errors.Add("Every booth location must use an accessible BoothAccess node.");

        if (anchors.Count == 0)
            warnings.Add("Layout has no geolocated navigation entrance; customers must select an indoor starting point manually.");
        if (anchors.Any(x => x.LayoutId != layoutId || !nodesById.ContainsKey(x.LayoutNodeId)))
            errors.Add("A navigation anchor references a node outside this layout.");
        if (anchors.Any(x => x.Latitude is < -90 or > 90 || x.Longitude is < -180 or > 180))
            errors.Add("Navigation anchor coordinates must be valid latitude and longitude values.");
        if (anchors.Any(x => x.AnchorType is NavigationAnchorType.Entrance or NavigationAnchorType.Both &&
                             nodesById.TryGetValue(x.LayoutNodeId, out var node) &&
                             (node.NodeType != LayoutNodeType.Entrance || !node.IsAccessible)))
            errors.Add("Customer entrance anchors must use accessible Entrance nodes.");

        if (nodes.Count == 0) 
            errors.Add("Layout must have at least one node.");

        if (edges.Count == 0) 
            warnings.Add("Layout has no paths.");

        return new() { Errors = errors.Distinct().ToList(), Warnings = warnings };
    }

    private static bool HasDuplicateDirectedArcs(IEnumerable<LayoutEdge> edges)
    {
        var arcs = new HashSet<(Guid From, Guid To)>();
        foreach (var edge in edges)
        {
            if (!arcs.Add((edge.FromNodeId, edge.ToNodeId))) return true;
            if (edge.IsBidirectional && !arcs.Add((edge.ToNodeId, edge.FromNodeId))) return true;
        }
        return false;
    }

    private static HashSet<Guid> ReachableFromEntrances(
        IEnumerable<Guid> starts, IEnumerable<LayoutEdge> edges, IReadOnlySet<Guid> usableNodeIds)
    {
        var graph = new Dictionary<Guid, List<Guid>>();
        foreach (var edge in edges.Where(x => IndoorGraphPolicy.CanUseEdge(x, new IndoorRoutePolicy())))
        {
            if (!usableNodeIds.Contains(edge.FromNodeId) || !usableNodeIds.Contains(edge.ToNodeId)) continue;
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

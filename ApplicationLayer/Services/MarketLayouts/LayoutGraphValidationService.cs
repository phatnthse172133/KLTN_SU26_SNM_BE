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
        var blocks = await _layouts.GetBlocksByLayoutIdAsync(layoutId, cancellationToken);

        var locations = await _locations.GetCurrentByLayoutAsync(layoutId, cancellationToken: cancellationToken);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Layout image is optional — canvas-based editors may not need a background image
        if (string.IsNullOrWhiteSpace(layout.LayoutImageUrl))
            warnings.Add("Layout has no background image. Canvas-based layouts can still be activated without one.");

        if (layout.Width <= 0 || layout.Height <= 0)
            errors.Add("Layout width and height must be greater than zero.");

        if (!nodes.Any(x => x.NodeType == LayoutNodeType.Entrance))
            errors.Add("Layout must have at least one entrance.");

        if (!nodes.Any(x => x.NodeType == LayoutNodeType.Exit))
            errors.Add("Layout must have at least one exit.");

        // BoothSlot support: require at least one Junction when booth slots exist.
        // Junctions are auto-generated per Zone by the layout generator; if the market owner
        // manually placed BoothSlots without the generator they must add a Junction manually.
        var boothSlots = nodes.Where(x => x.NodeType == LayoutNodeType.BoothSlot).ToList();
        if (boothSlots.Count > 0 && !nodes.Any(x => x.NodeType == LayoutNodeType.Junction))
            errors.Add("Layout must have at least one path point (junction). Use the Generate Layout feature or add one manually.");

        // Legacy BoothAccess nodes are not required for new layouts but are still validated if present

        if (nodes.Any(x => x.Xcoordinate < 0 || x.Xcoordinate > layout.Width || x.Ycoordinate < 0 || x.Ycoordinate > layout.Height))
            errors.Add("All nodes must be inside the layout dimensions.");

        if (blocks.Any(b => b.X < 0 || b.Y < 0 || b.X + b.Width > layout.Width || b.Y + b.Height > layout.Height))
            errors.Add("All blocks must be inside the layout dimensions.");

        if (edges.Any(x => x.FromNodeId == x.ToNodeId))
            errors.Add("Edges cannot connect a node to itself.");

        if (edges.GroupBy(x => x.FromNodeId.CompareTo(x.ToNodeId) < 0
                ? (x.FromNodeId, x.ToNodeId)
                : (x.ToNodeId, x.FromNodeId))
            .Any(x => x.Count() > 1))
            errors.Add("Layout contains duplicate edges.");

        var nodeIds = nodes.Select(x => x.Id).ToHashSet();

        if (edges.Any(x => !nodeIds.Contains(x.FromNodeId) || !nodeIds.Contains(x.ToNodeId)))
            errors.Add("All edge endpoints must belong to this layout.");

        var connectedIds = edges.SelectMany(x => new[] { x.FromNodeId, x.ToNodeId }).ToHashSet();

        // Entrance, BoothSlot and legacy BoothAccess nodes cannot be isolated
        if (nodes.Any(x => x.NodeType is LayoutNodeType.Entrance or LayoutNodeType.BoothSlot or LayoutNodeType.BoothAccess && !connectedIds.Contains(x.Id)))
            errors.Add("Required entrance, booth slot, and booth access nodes cannot be isolated.");

        var reachable = ReachableFromEntrances(nodes.Where(x => x.NodeType == LayoutNodeType.Entrance).Select(x => x.Id), edges);

        // Every BoothSlot must be reachable from an entrance
        if (boothSlots.Any(bs => !reachable.Contains(bs.Id)))
            errors.Add("Every booth slot must be reachable from an entrance.");

        // Legacy: BoothAccess nodes should also be reachable if present
        if (nodes.Any(x => x.NodeType == LayoutNodeType.BoothAccess && !reachable.Contains(x.Id)))
            errors.Add("Every booth access node must be reachable from an entrance.");

        // SlotCode validation for BoothSlots
        var slotsWithEmptyCode = boothSlots.Where(x => string.IsNullOrWhiteSpace(x.SlotCode)).ToList();
        if (slotsWithEmptyCode.Count > 0)
            errors.Add("Every booth slot must have a slot code.");

        var duplicateCodes = boothSlots
            .Where(x => !string.IsNullOrWhiteSpace(x.SlotCode))
            .GroupBy(x => x.SlotCode!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateCodes.Count > 0)
            errors.Add($"Duplicate slot codes: {string.Join(", ", duplicateCodes)}.");

        if (locations.Any(x => !nodeIds.Contains(x.LayoutNodeId)))
            errors.Add("A booth location references an invalid node.");

        if (nodes.Count == 0)
            errors.Add("Layout must have at least one node.");

        if (edges.Count == 0)
            warnings.Add("Layout has no paths.");

        if (boothSlots.Count == 0)
            warnings.Add("Layout has no booth slots. Customers will not be able to find booths.");

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

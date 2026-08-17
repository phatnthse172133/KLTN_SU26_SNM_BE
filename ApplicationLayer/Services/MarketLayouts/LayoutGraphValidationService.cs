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

        // Validation must be read-only.  The old implementation repaired a
        // missing link by connecting a node to the nearest junction and saved
        // that straight edge.  With multiple Zone rows this could create a
        // route through another Zone, so every generated graph must now be
        // rebuilt by LayoutGeneratorService's obstacle-aware router instead.

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

        // BoothSlot support: require at least one Junction when booth slots exist.
        // Junctions are auto-generated per Zone by the layout generator; if the market owner
        // manually placed BoothSlots without the generator they must add a Junction manually.
        var boothSlots = nodes.Where(x => x.NodeType == LayoutNodeType.BoothSlot).ToList();
        if (boothSlots.Count > 0 && !nodes.Any(x => x.NodeType == LayoutNodeType.Junction))
            errors.Add("Layout must have at least one path point (junction). Use the Generate Layout feature or add one manually.");

        // BoothAccess is a retired, legacy-only node type. Existing rows are retained
        // for compatibility but do not affect whether a modern Gate-based layout can
        // be activated.

        if (nodes.Any(x => x.Xcoordinate < 0 || x.Xcoordinate > layout.Width || x.Ycoordinate < 0 || x.Ycoordinate > layout.Height))
            errors.Add("All nodes must be inside the layout dimensions.");

        if (blocks.Any(b => b.X < 0 || b.Y < 0 || b.X + b.Width > layout.Width || b.Y + b.Height > layout.Height))
            errors.Add("All blocks must be inside the layout dimensions.");

        if (edges.Any(x => x.FromNodeId == x.ToNodeId))
            errors.Add("Edges cannot connect a node to itself.");

        if (edges.Any(x => x.Distance <= 0))
            errors.Add("All edges must have a distance greater than zero.");

        if (edges.GroupBy(x => x.FromNodeId.CompareTo(x.ToNodeId) < 0
                ? (x.FromNodeId, x.ToNodeId)
                : (x.ToNodeId, x.FromNodeId))
            .Any(x => x.Count() > 1))
            errors.Add("Layout contains duplicate edges.");

        var nodeIds = nodes.Select(x => x.Id).ToHashSet();
        var nodeById = nodes.ToDictionary(x => x.Id);

        if (edges.Any(x => !nodeIds.Contains(x.FromNodeId) || !nodeIds.Contains(x.ToNodeId)))
            errors.Add("All edge endpoints must belong to this layout.");

        // Edges must not cross through Zone blocks (walkways go around, not through)
        var liveBlocks = blocks.Where(b => !b.IsDeleted).ToList();
        foreach (var edge in edges)
        {
            if (!nodeById.TryGetValue(edge.FromNodeId, out var fromNode) || !nodeById.TryGetValue(edge.ToNodeId, out var toNode))
                continue;
            foreach (var block in liveBlocks)
            {
                // A generated booth slot is deliberately linked to the aisle
                // junction assigned to that same Zone.  The line starts inside
                // the Zone block and exits it, so treating it as a walkway that
                // "crosses a block" rejects every normal generated layout.
                // This is a terminal access link, not a through-route.  Only
                // exempt it when both endpoints belong to this exact Zone/block;
                // arbitrary edges entering or crossing a Zone are still invalid.
                if (IsGeneratedSlotAccessLink(fromNode, toNode, block))
                    continue;

                var rect = new LayoutRect(block.X, block.Y, block.Width, block.Height);
                if (LayoutBlockGeometry.SegmentIntersects(
                    (double)fromNode.Xcoordinate, (double)fromNode.Ycoordinate,
                    (double)toNode.Xcoordinate, (double)toNode.Ycoordinate, rect))
                {
                    errors.Add($"Edge from {fromNode.NodeName ?? fromNode.SlotCode ?? fromNode.Id.ToString()[..8]} to {toNode.NodeName ?? toNode.SlotCode ?? toNode.Id.ToString()[..8]} crosses through block {block.Name}. Routes must go around zone blocks, not through them.");
                    break;
                }
            }
        }

        var connectedIds = edges.SelectMany(x => new[] { x.FromNodeId, x.ToNodeId }).ToHashSet();

        // A Gate (stored as Entrance) and each booth slot must be connected. Retired
        // BoothAccess records from an older layout are deliberately ignored here.
        var disconnectedGates = nodes.Where(x => x.NodeType is LayoutNodeType.Entrance or LayoutNodeType.Exit && !connectedIds.Contains(x.Id)).ToList();
        foreach (var gate in disconnectedGates)
            errors.Add($"Gate {gate.NodeName ?? gate.Id.ToString()[..8]} has no connected walkway. Connect it to a junction before activating the layout.");

        var disconnectedSlots = boothSlots.Where(x => !connectedIds.Contains(x.Id)).ToList();
        foreach (var slot in disconnectedSlots)
            errors.Add($"Booth {slot.SlotCode ?? slot.NodeName ?? slot.Id.ToString()[..8]} has no connected walkway to a gate. Connect its access point before activating the layout.");

        var reachable = ReachableFromEntrances(nodes.Where(x => x.NodeType == LayoutNodeType.Entrance).Select(x => x.Id), edges);

        // Every BoothSlot must be reachable from an entrance
        var unreachableSlots = boothSlots.Where(bs => !reachable.Contains(bs.Id)).ToList();
        foreach (var slot in unreachableSlots)
            errors.Add($"Booth {slot.SlotCode ?? slot.NodeName ?? slot.Id.ToString()[..8]} is not reachable from any gate. Check that walkways connect it to an entrance.");

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

    private static bool IsGeneratedSlotAccessLink(
        DomainLayer.Entities.LayoutNode first,
        DomainLayer.Entities.LayoutNode second,
        DomainLayer.Entities.LayoutBlock block)
    {
        var isSlotToJunction =
            (first.NodeType == LayoutNodeType.BoothSlot && second.NodeType == LayoutNodeType.Junction) ||
            (second.NodeType == LayoutNodeType.BoothSlot && first.NodeType == LayoutNodeType.Junction);
        if (!isSlotToJunction)
            return false;

        return BelongsToBlock(first, block) && BelongsToBlock(second, block);
    }

    private static bool BelongsToBlock(
        DomainLayer.Entities.LayoutNode node,
        DomainLayer.Entities.LayoutBlock block)
        => (node.LayoutBlockId.HasValue && node.LayoutBlockId == block.Id)
           || (block.ZoneId.HasValue && node.ZoneId == block.ZoneId);

    private async Task<IReadOnlyCollection<DomainLayer.Entities.LayoutEdge>> RepairGeneratedGraphAsync(
        Guid layoutId,
        IReadOnlyCollection<DomainLayer.Entities.LayoutNode> nodes,
        IReadOnlyCollection<DomainLayer.Entities.LayoutEdge> edges,
        CancellationToken cancellationToken)
    {
        var gates = nodes.Where(node => node.NodeType == LayoutNodeType.Entrance).ToList();
        var junctions = nodes.Where(node => node.NodeType == LayoutNodeType.Junction).ToList();
        if (gates.Count == 0 || junctions.Count == 0)
            return [];

        var nodeIds = nodes.Select(node => node.Id).ToHashSet();
        var validEdges = edges.Where(edge => nodeIds.Contains(edge.FromNodeId) && nodeIds.Contains(edge.ToNodeId)).ToList();
        var pairs = validEdges.Select(CanonicalPair).ToHashSet();
        var additions = new List<DomainLayer.Entities.LayoutEdge>();

        void AddLink(DomainLayer.Entities.LayoutNode from, DomainLayer.Entities.LayoutNode to)
        {
            if (from.Id == to.Id || !pairs.Add(CanonicalPair(from.Id, to.Id))) return;
            var distance = Math.Sqrt(Math.Pow((double)(from.Xcoordinate - to.Xcoordinate), 2) +
                                     Math.Pow((double)(from.Ycoordinate - to.Ycoordinate), 2));
            additions.Add(new DomainLayer.Entities.LayoutEdge
            {
                Id = Guid.NewGuid(),
                LayoutId = layoutId,
                FromNodeId = from.Id,
                ToNodeId = to.Id,
                Distance = (decimal)distance,
                IsBidirectional = true,
                IsAccessible = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Every gate needs an entry point into the generated zone graph. AddLink
        // is pair-idempotent, so this never duplicates an existing correct edge.
        foreach (var gate in gates)
            AddLink(gate, Nearest(gate, junctions));

        // Each generated slot belongs to its zone junction. Use the nearest
        // junction only for pre-zone legacy rows that have no ZoneId/block ID.
        foreach (var slot in nodes.Where(node => node.NodeType == LayoutNodeType.BoothSlot))
        {
            var zoneJunction = junctions.FirstOrDefault(junction =>
                (slot.LayoutBlockId.HasValue && junction.LayoutBlockId == slot.LayoutBlockId) ||
                (slot.ZoneId.HasValue && junction.ZoneId == slot.ZoneId));
            AddLink(slot, zoneJunction ?? Nearest(slot, junctions));
        }

        // Join disconnected zone junctions into the gate-connected component.
        // The edges are internal routing metadata; the simplified UI does not
        // draw them in the assignment screen.
        var allEdges = validEdges.Concat(additions).ToList();
        var reachable = ReachableFromEntrances(gates.Select(gate => gate.Id), allEdges);
        foreach (var junction in junctions.Where(junction => !reachable.Contains(junction.Id)))
        {
            var sourceCandidates = nodes.Where(node => reachable.Contains(node.Id)).ToList();
            if (sourceCandidates.Count == 0) break;
            AddLink(Nearest(junction, sourceCandidates), junction);
            allEdges = validEdges.Concat(additions).ToList();
            reachable = ReachableFromEntrances(gates.Select(gate => gate.Id), allEdges);
        }

        if (additions.Count > 0)
        {
            await _edges.AddRangeAsync(additions);
            await _edges.SaveChangesAsync();
        }

        return additions;
    }

    private static DomainLayer.Entities.LayoutNode Nearest(
        DomainLayer.Entities.LayoutNode origin,
        IEnumerable<DomainLayer.Entities.LayoutNode> candidates)
        => candidates.OrderBy(candidate =>
                Math.Pow((double)(candidate.Xcoordinate - origin.Xcoordinate), 2) +
                Math.Pow((double)(candidate.Ycoordinate - origin.Ycoordinate), 2))
            .First();

    private static (Guid First, Guid Second) CanonicalPair(DomainLayer.Entities.LayoutEdge edge)
        => CanonicalPair(edge.FromNodeId, edge.ToNodeId);

    private static (Guid First, Guid Second) CanonicalPair(Guid first, Guid second)
        => first.CompareTo(second) < 0 ? (first, second) : (second, first);

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

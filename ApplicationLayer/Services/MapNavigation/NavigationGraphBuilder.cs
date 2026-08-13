using ApplicationLayer.Configuration;
using ApplicationLayer.Services.MarketLayouts;
using DomainLayer.Entities;
using Microsoft.Extensions.Options;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MapNavigation;

/// <summary>
/// Builds a transient corridor graph from persisted layout geometry.
/// Market Owner generation/persistence is never called from here.
/// </summary>
public sealed class NavigationGraphBuilder : INavigationGraphBuilder
{
    private readonly IndoorNavigationOptions _options;

    public NavigationGraphBuilder(IOptions<IndoorNavigationOptions>? options = null)
        => _options = options?.Value ?? new IndoorNavigationOptions();

    public NavigationGraph Build(
        MarketLayout layout,
        IReadOnlyCollection<LayoutBlock> blocks,
        IReadOnlyCollection<LayoutNode> persistedNodes,
        LayoutPhysicalScale? physicalScale = null)
        => Build(
            layout.Id,
            layout.Width,
            layout.Height,
            blocks,
            persistedNodes,
            layout.PixelsPerMeter,
            _options.PedestrianClearanceMeters,
            _options.MinimumCorridorMeters,
            DateTime.UtcNow,
            physicalScale ?? LayoutPhysicalCalibration.TryResolve(layout));

    public static NavigationGraph Build(
        Guid layoutId,
        double canvasWidth,
        double canvasHeight,
        IReadOnlyCollection<LayoutBlock> blocks,
        IReadOnlyCollection<LayoutNode> persistedNodes,
        double? pixelsPerMeter,
        double pedestrianClearanceMeters,
        double minimumCorridorMeters,
        DateTime now,
        LayoutPhysicalScale? physicalScale = null)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
            return NavigationGraph.Invalid("navigation geometry invalid");

        var liveNodes = persistedNodes.Where(node => !node.IsDeleted).ToList();
        var slots = liveNodes.Where(node => node.NodeType == LayoutNodeType.BoothSlot).ToList();
        var gates = liveNodes
            .Where(node => node.NodeType is LayoutNodeType.Entrance or LayoutNodeType.Exit)
            .ToList();
        var utilities = liveNodes
            .Where(node => node.NodeType is LayoutNodeType.Entrance or LayoutNodeType.Exit
                or LayoutNodeType.Restroom or LayoutNodeType.Landmark or LayoutNodeType.Information)
            .ToList();

        if (slots.Count == 0)
        {
            return new NavigationGraph
            {
                Nodes = utilities,
                Edges = [],
                IsValid = true
            };
        }

        var liveBlocks = blocks.Where(block => !block.IsDeleted).ToList();
        if (liveBlocks.Count == 0)
            return NavigationGraph.Invalid("navigation geometry invalid");

        var obstacles = LayoutBlockGeometry.ReconstructBooths(liveBlocks, slots);
        if (obstacles.Count == 0 || obstacles.Any(item => item.Bounds.Width <= 0 || item.Bounds.Height <= 0))
            return NavigationGraph.Invalid("navigation geometry invalid");

        var clearance = LayoutDistance.ClearancePixels(pixelsPerMeter, pedestrianClearanceMeters);
        var minCorridor = LayoutDistance.MinimumCorridorPixels(pixelsPerMeter, minimumCorridorMeters);
        var inflated = obstacles
            .Select(item => item with { Bounds = item.Bounds.Inflate(clearance) })
            .ToList();

        var narrow = new List<string>();
        foreach (var block in liveBlocks)
        {
            var geometry = LayoutBlockGeometry.Read(block);
            if (geometry.HorizontalGap + 0.0001 >= minCorridor && geometry.VerticalGap + 0.0001 >= minCorridor)
                continue;
            narrow.AddRange(slots
                .Where(slot => slot.LayoutBlockId == block.Id || (slot.ZoneId.HasValue && slot.ZoneId == block.ZoneId))
                .Select(slot => slot.SlotCode ?? slot.NodeName ?? "Unnamed slot"));
        }

        var aisleX = new SortedSet<double>();
        var aisleY = new SortedSet<double>();
        CollectAisles(liveBlocks, obstacles, aisleX, aisleY);

        var nodes = new List<LayoutNode>(utilities);
        var edges = new List<LayoutEdge>();
        var junctions = new List<LayoutNode>();

        foreach (var x in aisleX)
        {
            foreach (var y in aisleY)
            {
                if (x < -0.01 || y < -0.01 || x > canvasWidth + 0.01 || y > canvasHeight + 0.01)
                    continue;
                if (inflated.Any(item => item.Bounds.Contains(x, y)))
                    continue;
                if (junctions.Any(existing =>
                        Math.Abs((double)existing.Xcoordinate - x) < 0.5
                        && Math.Abs((double)existing.Ycoordinate - y) < 0.5))
                    continue;

                var junction = NewNode(
                    layoutId, LayoutNodeType.Junction, "Corridor", x, y, now);
                junctions.Add(junction);
                nodes.Add(junction);
            }
        }

        ConnectAxisAligned(layoutId, junctions, inflated, null, pixelsPerMeter, physicalScale, edges, now);

        var accessBySlot = new Dictionary<string, LayoutNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var booth in obstacles)
        {
            var access = PlaceAccess(layoutId, booth, junctions, inflated, pixelsPerMeter, now);
            if (access is null || string.IsNullOrWhiteSpace(booth.SlotCode))
                continue;
            nodes.Add(access.Value.Node);
            accessBySlot[booth.SlotCode] = access.Value.Node;
            edges.Add(MakeEdge(layoutId, access.Value.Node, access.Value.Anchor, pixelsPerMeter, physicalScale, now));
        }

        foreach (var gate in gates)
        {
            var anchor = NearestClear(gate, junctions, inflated, ignoreSlotCode: null);
            if (anchor is null)
                continue;
            edges.Add(MakeEdge(layoutId, gate, anchor, pixelsPerMeter, physicalScale, now));
        }

        var reachable = Reachable(gates.Select(gate => gate.Id), edges);
        var unreachable = slots
            .Where(slot =>
            {
                if (string.IsNullOrWhiteSpace(slot.SlotCode)
                    || !accessBySlot.TryGetValue(slot.SlotCode, out var access))
                    return true;
                return !reachable.Contains(access.Id);
            })
            .Select(slot => slot.SlotCode ?? slot.NodeName ?? "Unnamed slot")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new NavigationGraph
        {
            Nodes = nodes,
            Edges = edges,
            AccessBySlotCode = accessBySlot,
            UnreachableSlotCodes = unreachable,
            NarrowCorridorSlotCodes = narrow.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            IsValid = true
        };
    }

    private static void CollectAisles(
        IReadOnlyCollection<LayoutBlock> blocks,
        IReadOnlyList<BoothObstacle> obstacles,
        SortedSet<double> aisleX,
        SortedSet<double> aisleY)
    {
        foreach (var block in blocks)
        {
            var geometry = LayoutBlockGeometry.Read(block);
            aisleX.Add(block.X);
            aisleX.Add(block.X + block.Width);
            aisleY.Add(block.Y);
            aisleY.Add(block.Y + block.Height);
            if (geometry.InnerPadding > 0)
            {
                aisleX.Add(block.X + geometry.InnerPadding / 2);
                aisleX.Add(block.X + block.Width - geometry.InnerPadding / 2);
                aisleY.Add(block.Y + geometry.InnerPadding / 2);
                aisleY.Add(block.Y + block.Height - geometry.InnerPadding / 2);
            }

            var inBlock = obstacles
                .Where(item => item.LayoutBlockId == block.Id || (item.ZoneId.HasValue && item.ZoneId == block.ZoneId))
                .ToList();
            var columnCenters = inBlock.Select(item => item.Bounds.CenterX).Distinct().OrderBy(value => value).ToList();
            var rowCenters = inBlock.Select(item => item.Bounds.CenterY).Distinct().OrderBy(value => value).ToList();
            for (var i = 0; i < columnCenters.Count - 1; i++)
                aisleX.Add((columnCenters[i] + columnCenters[i + 1]) / 2);
            for (var i = 0; i < rowCenters.Count - 1; i++)
                aisleY.Add((rowCenters[i] + rowCenters[i + 1]) / 2);
        }
    }

    private static void ConnectAxisAligned(
        Guid layoutId,
        List<LayoutNode> junctions,
        IReadOnlyList<BoothObstacle> inflated,
        string? ignoreSlotCode,
        double? pixelsPerMeter,
        LayoutPhysicalScale? physicalScale,
        List<LayoutEdge> edges,
        DateTime now)
    {
        foreach (var group in junctions.GroupBy(node => Math.Round((double)node.Xcoordinate, 2)))
        {
            var ordered = group.OrderBy(node => node.Ycoordinate).ToList();
            for (var i = 0; i < ordered.Count - 1; i++)
            {
                if (IsClear(ordered[i], ordered[i + 1], inflated, ignoreSlotCode))
                    edges.Add(MakeEdge(layoutId, ordered[i], ordered[i + 1], pixelsPerMeter, physicalScale, now));
            }
        }

        foreach (var group in junctions.GroupBy(node => Math.Round((double)node.Ycoordinate, 2)))
        {
            var ordered = group.OrderBy(node => node.Xcoordinate).ToList();
            for (var i = 0; i < ordered.Count - 1; i++)
            {
                if (IsClear(ordered[i], ordered[i + 1], inflated, ignoreSlotCode))
                    edges.Add(MakeEdge(layoutId, ordered[i], ordered[i + 1], pixelsPerMeter, physicalScale, now));
            }
        }
    }

    private static (LayoutNode Node, LayoutNode Anchor)? PlaceAccess(
        Guid layoutId,
        BoothObstacle booth,
        IReadOnlyList<LayoutNode> junctions,
        IReadOnlyList<BoothObstacle> inflated,
        double? pixelsPerMeter,
        DateTime now)
    {
        var bounds = booth.Bounds;
        var candidates = new (double X, double Y)[]
        {
            (bounds.CenterX, bounds.Top),
            (bounds.CenterX, bounds.Bottom),
            (bounds.Left, bounds.CenterY),
            (bounds.Right, bounds.CenterY)
        };

        (LayoutNode Node, LayoutNode Anchor, double Distance)? best = null;
        foreach (var (x, y) in candidates)
        {
            if (inflated.Any(item => item.SlotCode != booth.SlotCode && item.Bounds.Contains(x, y)))
                continue;
            var probe = new LayoutNode
            {
                Xcoordinate = (decimal)x,
                Ycoordinate = (decimal)y
            };
            var anchor = NearestClear(probe, junctions, inflated, booth.SlotCode);
            if (anchor is null)
                continue;
            var distance = (double)LayoutDistance.Between(x, y, (double)anchor.Xcoordinate, (double)anchor.Ycoordinate, (double?)null);
            if (best is null || distance < best.Value.Distance)
            {
                var node = NewNode(
                    layoutId, LayoutNodeType.BoothAccess, booth.SlotCode ?? "Access", x, y, now,
                    booth.SlotCode, booth.LayoutBlockId, booth.ZoneId, booth.RowIndex, booth.ColumnIndex);
                best = (node, anchor, distance);
            }
        }

        return best is null ? null : (best.Value.Node, best.Value.Anchor);
    }

    private static LayoutNode? NearestClear(
        LayoutNode from,
        IReadOnlyList<LayoutNode> candidates,
        IReadOnlyList<BoothObstacle> inflated,
        string? ignoreSlotCode)
        => candidates
            .Where(candidate => IsClear(from, candidate, inflated, ignoreSlotCode))
            .OrderBy(candidate => LayoutDistance.Between(from, candidate, (double?)null))
            .FirstOrDefault();

    private static bool IsClear(
        LayoutNode from,
        LayoutNode to,
        IReadOnlyList<BoothObstacle> inflated,
        string? ignoreSlotCode)
        => !inflated.Any(item =>
            !string.Equals(item.SlotCode, ignoreSlotCode, StringComparison.OrdinalIgnoreCase)
            && LayoutBlockGeometry.SegmentIntersects(
                (double)from.Xcoordinate, (double)from.Ycoordinate,
                (double)to.Xcoordinate, (double)to.Ycoordinate,
                item.Bounds));

    private static LayoutNode NewNode(
        Guid layoutId,
        LayoutNodeType type,
        string name,
        double x,
        double y,
        DateTime now,
        string? slotCode = null,
        Guid? layoutBlockId = null,
        Guid? zoneId = null,
        int? rowIndex = null,
        int? columnIndex = null)
        => new()
        {
            Id = Guid.NewGuid(),
            LayoutId = layoutId,
            LayoutBlockId = layoutBlockId,
            ZoneId = zoneId,
            NodeType = type,
            NodeName = name,
            SlotCode = slotCode,
            RowIndex = rowIndex,
            ColumnIndex = columnIndex,
            Xcoordinate = (decimal)x,
            Ycoordinate = (decimal)y,
            IsAccessible = true,
            IsStartingPoint = false,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static LayoutEdge MakeEdge(
        Guid layoutId,
        LayoutNode from,
        LayoutNode to,
        double? pixelsPerMeter,
        LayoutPhysicalScale? physicalScale,
        DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            LayoutId = layoutId,
            FromNodeId = from.Id,
            ToNodeId = to.Id,
            Distance = LayoutDistance.Between(from, to, physicalScale ?? (pixelsPerMeter is > 0
                ? new LayoutPhysicalScale(1d / pixelsPerMeter.Value, 1d / pixelsPerMeter.Value, "PixelsPerMeter")
                : null)),
            IsBidirectional = true,
            IsAccessible = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static HashSet<Guid> Reachable(IEnumerable<Guid> starts, IEnumerable<LayoutEdge> edges)
    {
        var graph = new Dictionary<Guid, List<Guid>>();
        foreach (var edge in edges.Where(item => item.IsAccessible && !item.IsDeleted))
        {
            graph.TryAdd(edge.FromNodeId, []);
            graph[edge.FromNodeId].Add(edge.ToNodeId);
            if (!edge.IsBidirectional)
                continue;
            graph.TryAdd(edge.ToNodeId, []);
            graph[edge.ToNodeId].Add(edge.FromNodeId);
        }

        var visited = starts.ToHashSet();
        var queue = new Queue<Guid>(visited);
        while (queue.TryDequeue(out var current))
        {
            if (!graph.TryGetValue(current, out var adjacent))
                continue;
            foreach (var next in adjacent)
                if (visited.Add(next))
                    queue.Enqueue(next);
        }

        return visited;
    }
}

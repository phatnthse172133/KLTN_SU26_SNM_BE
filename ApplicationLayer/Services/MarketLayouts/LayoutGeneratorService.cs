using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketLayouts;

/// <summary>
/// Grid generator — pure computation, no DB I/O.
/// Zone blocks are laid out left→right, top→bottom.
/// Slot codes follow pattern: ZoneCode-NN (e.g., A-01, B-12).
///
/// For each Zone the generator produces:
///   - A LayoutBlock (the visual bounding box of the zone)
///   - BoothSlot nodes in a grid (preserving IDs for slots that already exist)
///   - A single Junction node per Zone (aisle/walkway hub for routing)
///   - LayoutEdges connecting each BoothSlot to its Zone's Junction
///
/// Utility nodes (Entrance, Exit, Restroom, Landmark, pre-existing Junctions)
/// and their edges are PRESERVED and not regenerated.
/// </summary>
public class LayoutGeneratorService : ILayoutGeneratorService
{
    private const double BlockPadding = 28.0;
    private const double JunctionOffsetY = -30.0; // Junction sits above the Zone block
    private const double PhysicalJunctionReserveMeters = 4.0;
    private const double AutoFitFacilityReserveMeters = 7.0;
    private const double CorridorClearance = 12.0;

    public GenerationPreviewResponse ComputePreview(
        MarketLayout layout,
        IReadOnlyCollection<Zone> zones,
        IReadOnlyCollection<LayoutNode> existingNodes,
        IReadOnlyCollection<BoothLocation> assignedLocations,
        GenerateLayoutRequest request)
    {
        var (blocks, errors, warnings) = BuildZoneBlocks(layout, zones, request);
        var (canvasW, canvasH) = ComputeCanvas(layout, blocks, request);

        var preview = new GenerationPreviewResponse
        {
            CanvasWidth = canvasW,
            CanvasHeight = canvasH,
            Errors = errors,
            Warnings = warnings,
            ConflictingSlots = new List<ConflictingSlot>()
        };

        var generatedSlotCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in blocks)
        {
            var zone = zones.First(z => z.Id == block.ZoneId);
            var config = request.ZoneConfigs.First(c => c.ZoneId == block.ZoneId);
            var slots = BuildSlots(block, zone, config, assignedLocations);

            foreach (var slot in slots)
                generatedSlotCodes.Add(slot.SlotCode);

            preview.Zones.Add(new GenerationPreviewZoneResult
            {
                ZoneId = zone.Id,
                ZoneName = zone.ZoneName,
                ZoneCode = zone.ZoneCode,
                Color = zone.Color,
                ZoneType = zone.Description,
                Capacity = zone.Capacity,
                X = block.X,
                Y = block.Y,
                Width = block.Width,
                Height = block.Height,
                Columns = GetColumns(zone.Capacity, config),
                Rows = GetRows(zone.Capacity, config),
                ZoneWidthMeters = config.ZoneWidthMeters,
                ZoneLengthMeters = config.ZoneLengthMeters,
                BoothWidthMeters = config.BoothWidthMeters,
                BoothLengthMeters = config.BoothLengthMeters,
                HorizontalGapMeters = config.HorizontalGapMeters,
                VerticalGapMeters = config.VerticalGapMeters,
                Slots = slots
            });
        }

        // Conflict check using real generated slot codes
        // We ALWAYS preserve assigned slots to prevent accidental deletion of slots containing active booths.
        {
            var assignedNodeIds = assignedLocations
                .Where(bl => !bl.IsDeleted && bl.ReleasedAt == null)
                .Select(bl => bl.LayoutNodeId).ToHashSet();

            var assignedNodes = existingNodes
                .Where(n => n.NodeType == LayoutNodeType.BoothSlot && assignedNodeIds.Contains(n.Id))
                .ToList();

            foreach (var assignedNode in assignedNodes)
            {
                if (string.IsNullOrEmpty(assignedNode.SlotCode) || !generatedSlotCodes.Contains(assignedNode.SlotCode))
                {
                    var zoneName = zones.FirstOrDefault(z => z.Id == assignedNode.ZoneId)?.ZoneName ?? "Unknown Zone";
                    preview.ConflictingSlots.Add(new ConflictingSlot
                    {
                        SlotCode = assignedNode.SlotCode ?? assignedNode.NodeName ?? "?",
                        ZoneName = zoneName,
                        BoothName = "(assigned)",
                        Reason = "Slot would be removed but has an assigned booth."
                    });
                }
            }

            if (preview.ConflictingSlots.Count > 0)
                preview.Errors.Add($"{preview.ConflictingSlots.Count} slot(s) cannot be removed because they have assigned booths. Move the booths first.");
        }

        preview.CanApply = preview.Errors.Count == 0;
        return preview;
    }

    public GenerationResult ComputeGeneration(
        MarketLayout layout,
        IReadOnlyCollection<Zone> zones,
        IReadOnlyCollection<LayoutNode> existingNodes,
        IReadOnlyCollection<BoothLocation> assignedLocations,
        GenerateLayoutRequest request)
    {
        var preview = ComputePreview(layout, zones, existingNodes, assignedLocations, request);
        var now = DateTime.UtcNow;

        var assignedNodeIds = assignedLocations
            .Where(bl => !bl.IsDeleted && bl.ReleasedAt == null)
            .Select(bl => bl.LayoutNodeId).ToHashSet();

        // Index existing slot nodes by SlotCode for ID preservation
        var existingBySlotCode = existingNodes
            .Where(n => n.NodeType == LayoutNodeType.BoothSlot && !string.IsNullOrEmpty(n.SlotCode))
            .ToDictionary(n => n.SlotCode!, StringComparer.OrdinalIgnoreCase);

        // Index existing Junction nodes by ZoneId for preservation
        var existingJunctionsByZone = existingNodes
            .Where(n => n.NodeType == LayoutNodeType.Junction && n.ZoneId.HasValue)
            .GroupBy(n => n.ZoneId!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        // Legacy junctions persisted before zone materialization carry a null ZoneId — match them by name
        var legacyJunctionsByName = existingNodes
            .Where(n => n.NodeType == LayoutNodeType.Junction && !n.ZoneId.HasValue && n.NodeName != null)
            .GroupBy(n => n.NodeName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var layoutBlocks = new List<LayoutBlock>();
        var newNodes = new List<LayoutNode>();
        var newEdges = new List<LayoutEdge>();
        int displayOrder = 0;

        foreach (var zonePreview in preview.Zones)
        {
            var zone = zones.First(z => z.Id == zonePreview.ZoneId);
            var config = request.ZoneConfigs.First(c => c.ZoneId == zone.Id);
            Guid? entityZoneId = zone.Id;
            var blockId = Guid.NewGuid();

            layoutBlocks.Add(new LayoutBlock
            {
                Id = blockId,
                LayoutId = layout.Id,
                ZoneId = entityZoneId,
                Type = "Zone",
                Name = zone.ZoneName,
                X = zonePreview.X,
                Y = zonePreview.Y,
                Width = zonePreview.Width,
                Height = zonePreview.Height,
                Rotation = 0,
                ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    boothWidth = config.BoothWidth,
                    boothHeight = config.BoothHeight,
                    horizontalGap = config.Gap,
                    verticalGap = GetVerticalGap(config, request),
                    innerPadding = request.MarketWidthMeters.HasValue ? request.PixelsPerMeter : BlockPadding,
                    columns = zonePreview.Columns,
                    rows = zonePreview.Rows,
                    physical = request.MarketWidthMeters.HasValue
                }),
                DisplayOrder = displayOrder++,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            });

            // ── Junction node (one per Zone, preserved or newly created) ──
            Guid junctionId;
            // Keep the aisle point in the reserved corridor above the Zone.
            // Centring it creates a clear shared walkway across each zone row.
            decimal junctionX = (decimal)(zonePreview.X + zonePreview.Width / 2);
            decimal junctionY = (decimal)(zonePreview.Y + JunctionOffsetY);

            if (!existingJunctionsByZone.TryGetValue(zone.Id, out var existingJunction))
                legacyJunctionsByName.TryGetValue($"{zone.ZoneName} Aisle", out existingJunction);

            if (existingJunction is not null)
            {
                junctionId = existingJunction.Id;
                newNodes.Add(new LayoutNode
                {
                    Id = junctionId,
                    LayoutId = layout.Id,
                    ZoneId = entityZoneId,
                    LayoutBlockId = blockId,
                    NodeType = LayoutNodeType.Junction,
                    NodeName = $"{zone.ZoneName} Aisle",
                    Xcoordinate = junctionX,
                    Ycoordinate = Math.Max(0, junctionY),
                    IsAccessible = true,
                    IsStartingPoint = false,
                    IsDeleted = false,
                    CreatedAt = existingJunction.CreatedAt,
                    UpdatedAt = now
                });
            }
            else
            {
                junctionId = Guid.NewGuid();
                newNodes.Add(new LayoutNode
                {
                    Id = junctionId,
                    LayoutId = layout.Id,
                    ZoneId = entityZoneId,
                    LayoutBlockId = blockId,
                    NodeType = LayoutNodeType.Junction,
                    NodeName = $"{zone.ZoneName} Aisle",
                    Xcoordinate = junctionX,
                    Ycoordinate = Math.Max(0, junctionY),
                    IsAccessible = true,
                    IsStartingPoint = false,
                    IsDeleted = false,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            // ── Slot nodes ──
            foreach (var slot in zonePreview.Slots)
            {
                // Skip assigned slots that are outside the new capacity (safety — ApplyGeneration already blocked this)
                if (existingBySlotCode.TryGetValue(slot.SlotCode, out var existingSlot)
                    && assignedNodeIds.Contains(existingSlot.Id) && slot.HasAssignedBooth)
                {
                    // Keep the existing node with updated coordinates
                    existingSlot.Xcoordinate = (decimal)(slot.X + slot.Width / 2);
                    existingSlot.Ycoordinate = (decimal)(slot.Y + slot.Height / 2);
                    existingSlot.LayoutBlockId = blockId;
                    existingSlot.RowIndex = slot.RowIndex;
                    existingSlot.ColumnIndex = slot.ColumnIndex;
                    existingSlot.UpdatedAt = now;
                    newNodes.Add(existingSlot);
                }
                else
                {
                    var nodeId = existingBySlotCode.TryGetValue(slot.SlotCode, out var e2) ? e2.Id : Guid.NewGuid();
                    var createdAt = existingBySlotCode.TryGetValue(slot.SlotCode, out var e3) ? e3.CreatedAt : now;
                    newNodes.Add(new LayoutNode
                    {
                        Id = nodeId,
                        LayoutId = layout.Id,
                        ZoneId = entityZoneId,
                        LayoutBlockId = blockId,
                        NodeType = LayoutNodeType.BoothSlot,
                        NodeName = slot.SlotCode,
                        SlotCode = slot.SlotCode,
                        RowIndex = slot.RowIndex,
                        ColumnIndex = slot.ColumnIndex,
                        Xcoordinate = (decimal)(slot.X + slot.Width / 2),
                        Ycoordinate = (decimal)(slot.Y + slot.Height / 2),
                        IsAccessible = true,
                        IsStartingPoint = false,
                        IsDeleted = false,
                        CreatedAt = createdAt,
                        UpdatedAt = now
                    });
                }

                // Edge: BoothSlot ↔ Zone Junction (bidirectional for pathfinding)
                var slotNode = newNodes[^1];
                newEdges.Add(MakeEdge(layout.Id, slotNode.Id, junctionId, slotNode, newNodes, now));
            }
        }

        // ── Preserve utility nodes (Entrance, Exit, Restroom, Landmark, Junction outside zones) ──
        var generatedNodeIds = newNodes.Select(n => n.Id).ToHashSet();
        var preservedNodes = existingNodes
            .Where(n => !n.IsDeleted
                && n.NodeType != LayoutNodeType.BoothSlot
                // Corridor waypoints are regenerated from the current blocks.
                // Keeping old auto-waypoints is what caused stale cross-zone
                // edges to survive a second generation.
                && !(n.NodeType == LayoutNodeType.Junction
                     && n.ZoneId == null
                     && n.NodeName != null
                     && n.NodeName.StartsWith("Auto Corridor ", StringComparison.OrdinalIgnoreCase))
                // Zone aisles are generated routing nodes as well.  Keeping a
                // stale aisle from an older zone arrangement is what can leave
                // edges such as C-05 -> Nước uống Aisle in the persisted graph.
                // The current zone aisles are already present in newNodes with
                // their fresh LayoutBlockId, so old generated aisles must not
                // enter the preservation set.
                && !(n.NodeType == LayoutNodeType.Junction
                     && (n.ZoneId.HasValue
                         || (n.NodeName?.EndsWith(" Aisle", StringComparison.OrdinalIgnoreCase) ?? false)))
                && !generatedNodeIds.Contains(n.Id))
            .Select(n => { n.UpdatedAt = now; return n; })
            .ToList();
        newNodes.AddRange(preservedNodes);

        // Generated maps should be activatable without requiring the owner to
        // manually discover and place the mandatory utility points first.
        var zoneJunctions = newNodes
            .Where(n => n.NodeType == LayoutNodeType.Junction)
            .OrderBy(n => n.Xcoordinate)
            .ThenBy(n => n.Ycoordinate)
            .ToList();
        if (!newNodes.Any(n => n.NodeType == LayoutNodeType.Entrance))
        {
            newNodes.Add(new LayoutNode
            {
                Id = Guid.NewGuid(),
                LayoutId = layout.Id,
                NodeType = LayoutNodeType.Entrance,
                NodeName = "Main Entrance",
                Xcoordinate = Math.Max(24, preview.CanvasWidth / 2),
                Ycoordinate = 24,
                IsAccessible = true,
                IsStartingPoint = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // Physical/auto-fit layouts own explicit facility corridors. Re-anchor
        // the primary Gate on every generation so it cannot be left inside a
        // Zone after dimensions change. Existing Exit nodes are retained as
        // legacy Gates, but new layouts use only the unified Gate concept.
        if ((request.AutoFitZones || request.MarketWidthMeters.HasValue) && layoutBlocks.Count > 0)
        {
            var zoneTop = layoutBlocks.Min(block => block.Y);
            var centerX = (decimal)Math.Max(24, preview.CanvasWidth / 2);
            var entranceY = (decimal)Math.Max(16, zoneTop / 2);

            var primaryEntrance = newNodes
                .Where(n => n.NodeType == LayoutNodeType.Entrance)
                .OrderBy(n => n.CreatedAt)
                .First();
            primaryEntrance.Xcoordinate = centerX;
            primaryEntrance.Ycoordinate = entranceY;
            primaryEntrance.NodeName = "Main Entrance";
            primaryEntrance.IsStartingPoint = true;
            primaryEntrance.UpdatedAt = now;

        }

        // Utility edges are preserved by MarketLayoutService.ApplyGenerationAsync,
        // which merges them with the generated edges before persisting.

        // ── Auto-generate paths from Entrances/Exits to Junctions ──
        // Each Entrance/Exit connects to its closest Junction.
        // All zone Junctions are chained together so every BoothSlot is reachable from any Entrance.
        var entrances = newNodes.Where(n => n.NodeType == LayoutNodeType.Entrance).ToList();
        var junctions = newNodes
            .Where(n => n.NodeType == LayoutNodeType.Junction)
            .OrderBy(j => j.Xcoordinate)
            .ThenBy(j => j.Ycoordinate)
            .ToList();
        if (junctions.Any())
        {
            // Build a grid-like mesh of junction edges so every zone is reachable
            // with shorter, more natural paths than a single linear chain.
            // Route all zone junctions and gates through free corridor space.
            // A nearest-neighbour straight edge is deliberately not used: it
            // can cut through another Zone block when zones are in multiple rows.
            ReanchorGatesOutsideBlocks(newNodes, layoutBlocks, preview.CanvasWidth, preview.CanvasHeight);
            ConnectJunctionsAroundBlocks(
                layout.Id,
                junctions,
                entrances.Concat(newNodes.Where(n => n.NodeType == LayoutNodeType.Exit)).ToList(),
                layoutBlocks,
                preview.CanvasWidth,
                preview.CanvasHeight,
                newNodes,
                newEdges,
                now);
        }

        return new GenerationResult(layoutBlocks, newNodes, newEdges, preview.CanvasWidth, preview.CanvasHeight, preview);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static void ConnectJunctionsAroundBlocks(
        Guid layoutId,
        List<LayoutNode> junctions,
        List<LayoutNode> gates,
        IReadOnlyCollection<LayoutBlock> blocks,
        int canvasWidth,
        int canvasHeight,
        List<LayoutNode> allNodes,
        List<LayoutEdge> edges,
        DateTime now)
    {
        var anchors = gates
            .Concat(junctions)
            .GroupBy(node => node.Id)
            .Select(group => group.First())
            .ToList();
        if (anchors.Count <= 1)
            return;

        var routeNodes = new List<LayoutNode>(anchors);
        AddCorridorWaypoints(layoutId, blocks, canvasWidth, canvasHeight, anchors, routeNodes, allNodes, now);
        var adjacency = BuildVisibilityGraph(routeNodes, blocks);

        // One primary entrance is enough to make every aisle reachable. Using
        // shortest obstacle-free paths keeps the graph compact and avoids the
        // diagonal/vertical lines that previously crossed Zone rectangles.
        var primary = anchors.FirstOrDefault(node => node.NodeType == LayoutNodeType.Entrance)
                      ?? anchors[0];
        var primaryIndex = routeNodes.FindIndex(node => node.Id == primary.Id);
        if (primaryIndex < 0)
            return;

        foreach (var target in anchors.Where(node => node.Id != primary.Id))
        {
            var targetIndex = routeNodes.FindIndex(node => node.Id == target.Id);
            if (targetIndex < 0)
                continue;
            var path = FindShortestPath(primaryIndex, targetIndex, adjacency);
            for (var i = 0; i + 1 < path.Count; i++)
            {
                var from = routeNodes[path[i]];
                var to = routeNodes[path[i + 1]];
                if (!RouteSegmentIsClear(from, to, blocks))
                    continue;
                AddUniqueEdge(layoutId, from, to, allNodes, edges, now);
            }
        }
    }

    private static void ReanchorGatesOutsideBlocks(
        IEnumerable<LayoutNode> nodes,
        IReadOnlyCollection<LayoutBlock> blocks,
        int canvasWidth,
        int canvasHeight)
    {
        var gates = nodes.Where(node => node.NodeType is LayoutNodeType.Entrance or LayoutNodeType.Exit);
        foreach (var gate in gates)
        {
            if (!blocks.Any(block => new LayoutRect(block.X, block.Y, block.Width, block.Height)
                    .Inflate(CorridorClearance).Contains((double)gate.Xcoordinate, (double)gate.Ycoordinate)))
                continue;

            var x = Math.Clamp((double)gate.Xcoordinate, CorridorClearance, Math.Max(CorridorClearance, canvasWidth - CorridorClearance));
            var y = gate.NodeType == LayoutNodeType.Exit
                ? Math.Max(CorridorClearance, canvasHeight - CorridorClearance)
                : CorridorClearance;
            gate.Xcoordinate = (decimal)x;
            gate.Ycoordinate = (decimal)y;
            gate.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static void AddCorridorWaypoints(
        Guid layoutId,
        IReadOnlyCollection<LayoutBlock> blocks,
        int canvasWidth,
        int canvasHeight,
        IReadOnlyCollection<LayoutNode> anchors,
        List<LayoutNode> routeNodes,
        List<LayoutNode> allNodes,
        DateTime now)
    {
        var index = 1;
        var points = new List<(double X, double Y)>();
        foreach (var block in blocks.Where(block => !block.IsDeleted))
        {
            var rect = new LayoutRect(block.X, block.Y, block.Width, block.Height).Inflate(CorridorClearance);
            points.AddRange([
                (rect.Left, rect.Top), (rect.CenterX, rect.Top), (rect.Right, rect.Top),
                (rect.Left, rect.CenterY), (rect.Right, rect.CenterY),
                (rect.Left, rect.Bottom), (rect.CenterX, rect.Bottom), (rect.Right, rect.Bottom)
            ]);

            // Projection points let the visibility graph form orthogonal
            // routes from an arbitrary gate/aisle coordinate to a safe side
            // of this block instead of drawing a diagonal across the map.
            foreach (var anchor in anchors)
            {
                var ax = (double)anchor.Xcoordinate;
                var ay = (double)anchor.Ycoordinate;
                points.AddRange([(ax, rect.Top), (ax, rect.Bottom),
                    (rect.Left, ay), (rect.Right, ay)]);
            }
        }
        points.AddRange([(CorridorClearance, CorridorClearance),
            (Math.Max(CorridorClearance, canvasWidth - CorridorClearance), CorridorClearance),
            (CorridorClearance, Math.Max(CorridorClearance, canvasHeight - CorridorClearance)),
            (Math.Max(CorridorClearance, canvasWidth - CorridorClearance), Math.Max(CorridorClearance, canvasHeight - CorridorClearance))]);

        // Add the two possible Manhattan bends for every pair of anchors. The
        // obstacle filter below removes bends that fall inside a Zone, while
        // the remaining points allow a clean L-shaped route between rows.
        var anchorList = anchors.ToList();
        for (var i = 0; i < anchorList.Count; i++)
        for (var j = i + 1; j < anchorList.Count; j++)
        {
            points.Add(((double)anchorList[i].Xcoordinate, (double)anchorList[j].Ycoordinate));
            points.Add(((double)anchorList[j].Xcoordinate, (double)anchorList[i].Ycoordinate));
        }

        foreach (var (rawX, rawY) in points)
        {
            var x = Math.Clamp(rawX, 2, Math.Max(2, canvasWidth - 2));
            var y = Math.Clamp(rawY, 2, Math.Max(2, canvasHeight - 2));
            if (blocks.Any(block => new LayoutRect(block.X, block.Y, block.Width, block.Height)
                    .Inflate(2).Contains(x, y)))
                continue;
            if (routeNodes.Any(node => Math.Abs((double)node.Xcoordinate - x) < 1
                                      && Math.Abs((double)node.Ycoordinate - y) < 1))
                continue;

            var waypoint = new LayoutNode
            {
                Id = Guid.NewGuid(),
                LayoutId = layoutId,
                NodeType = LayoutNodeType.Junction,
                NodeName = $"Auto Corridor {index++}",
                Xcoordinate = (decimal)x,
                Ycoordinate = (decimal)y,
                IsAccessible = true,
                IsStartingPoint = false,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            routeNodes.Add(waypoint);
            allNodes.Add(waypoint);
        }
    }

    private static List<(int To, double Cost)>[] BuildVisibilityGraph(
        IReadOnlyList<LayoutNode> routeNodes,
        IReadOnlyCollection<LayoutBlock> blocks)
    {
        var graph = Enumerable.Range(0, routeNodes.Count)
            .Select(_ => new List<(int To, double Cost)>())
            .ToArray();
        for (var i = 0; i < routeNodes.Count; i++)
        for (var j = i + 1; j < routeNodes.Count; j++)
        {
            var dx = (double)(routeNodes[i].Xcoordinate - routeNodes[j].Xcoordinate);
            var dy = (double)(routeNodes[i].Ycoordinate - routeNodes[j].Ycoordinate);
            // Keep generated walkways horizontal/vertical, like a real market
            // corridor. A bend is represented by an Auto Corridor waypoint.
            if (Math.Abs(dx) > 0.5 && Math.Abs(dy) > 0.5)
                continue;
            if (!RouteSegmentIsClear(routeNodes[i], routeNodes[j], blocks))
                continue;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            graph[i].Add((j, distance));
            graph[j].Add((i, distance));
        }
        return graph;
    }

    private static List<int> FindShortestPath(int start, int target, IReadOnlyList<List<(int To, double Cost)>> graph)
    {
        var distances = Enumerable.Repeat(double.PositiveInfinity, graph.Count).ToArray();
        var previous = Enumerable.Repeat(-1, graph.Count).ToArray();
        var used = new bool[graph.Count];
        distances[start] = 0;
        for (var step = 0; step < graph.Count; step++)
        {
            var current = -1;
            var best = double.PositiveInfinity;
            for (var i = 0; i < graph.Count; i++)
                if (!used[i] && distances[i] < best) { best = distances[i]; current = i; }
            if (current < 0) break;
            used[current] = true;
            if (current == target) break;
            foreach (var (next, cost) in graph[current])
                if (distances[next] > distances[current] + cost)
                { distances[next] = distances[current] + cost; previous[next] = current; }
        }
        if (double.IsPositiveInfinity(distances[target]))
            return [];
        var path = new List<int>();
        for (var current = target; current >= 0; current = previous[current]) path.Add(current);
        path.Reverse();
        return path;
    }

    private static bool RouteSegmentIsClear(LayoutNode from, LayoutNode to, IReadOnlyCollection<LayoutBlock> blocks)
        => !blocks.Where(block => !block.IsDeleted)
            .Any(block => LayoutBlockGeometry.SegmentIntersects(
                (double)from.Xcoordinate, (double)from.Ycoordinate,
                (double)to.Xcoordinate, (double)to.Ycoordinate,
                new LayoutRect(block.X, block.Y, block.Width, block.Height).Inflate(1)));

    private static void AddUniqueEdge(
        Guid layoutId,
        LayoutNode from,
        LayoutNode to,
        IReadOnlyList<LayoutNode> allNodes,
        List<LayoutEdge> edges,
        DateTime now)
    {
        if (from.Id == to.Id || edges.Any(edge =>
                (edge.FromNodeId == from.Id && edge.ToNodeId == to.Id)
                || (edge.FromNodeId == to.Id && edge.ToNodeId == from.Id)))
            return;
        edges.Add(MakeEdge(layoutId, from.Id, to.Id, from, allNodes, now));
    }

    private static (List<LayoutBlock> blocks, List<string> errors, List<string> warnings)
        BuildZoneBlocks(
            MarketLayout layout,
            IReadOnlyCollection<Zone> zones,
            GenerateLayoutRequest request)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var blocks = new List<LayoutBlock>();

        var configuredZones = request.ZoneConfigs
            .Select(c => (Config: c, Zone: zones.FirstOrDefault(z => z.Id == c.ZoneId)))
            .Where(x => x.Zone != null)
            .ToList();

        if (!configuredZones.Any())
        {
            errors.Add("No valid zones found for generation.");
            return (blocks, errors, warnings);
        }

        int zonesPerRow = Math.Max(1, request.ZonesPerRow ?? 1);
        double curX = request.StartX;
        double curY = request.StartY;
        double rowMaxH = 0;
        int col = 0;
        double requiredWidth = 0;
        double requiredHeight = 0;

        foreach (var (config, zone) in configuredZones)
        {
            if (zone!.Capacity <= 0)
            {
                warnings.Add($"Zone '{zone.ZoneName}' has Capacity = 0. Skipped.");
                continue;
            }

            int columns = GetColumns(zone.Capacity, config);
            int rows = GetRows(zone.Capacity, config);
            var physical = request.MarketWidthMeters.HasValue;
            var padding = physical ? request.PixelsPerMeter : BlockPadding;
            var verticalGap = GetVerticalGap(config, request);
            double blockW = physical && config.ZoneWidthMeters.HasValue
                ? config.ZoneWidthMeters.Value * request.PixelsPerMeter
                : padding * 2 + columns * config.BoothWidth + (columns - 1) * config.Gap;
            double blockH = physical && config.ZoneLengthMeters.HasValue
                ? config.ZoneLengthMeters.Value * request.PixelsPerMeter
                : padding * 2 + rows * config.BoothHeight + (rows - 1) * verticalGap;

            if (config.AisleEveryNRows > 0 && rows > config.AisleEveryNRows)
            {
                int aisleCount = (rows - 1) / config.AisleEveryNRows;
                blockH += aisleCount * config.AisleWidth;
            }

            // Keep a scale-independent physical corridor above each zone for its
            // junction. Legacy pixel layouts retain their historical 40 px reserve.
            var junctionReserve = physical
                ? (request.AutoFitZones ? AutoFitFacilityReserveMeters : PhysicalJunctionReserveMeters) * request.PixelsPerMeter
                : Math.Abs(JunctionOffsetY) + 10;
            double blockYWithAisle = curY + junctionReserve;

            double blockX = config.CustomX ?? curX;
            double blockY = config.CustomY ?? blockYWithAisle;

            var boundaryWidth = request.MarketWidthMeters.HasValue
                ? request.MarketWidthMeters.Value * request.PixelsPerMeter
                : layout.Width;
            var boundaryHeight = request.MarketLengthMeters.HasValue
                ? request.MarketLengthMeters.Value * request.PixelsPerMeter
                : layout.Height;
            requiredWidth = Math.Max(requiredWidth, blockX + blockW);
            requiredHeight = Math.Max(requiredHeight, blockY + blockH);

            if (blockX + blockW > boundaryWidth + 0.01)
            {
                var exceedW = (blockX + blockW - boundaryWidth) / (physical ? request.PixelsPerMeter : 1);
                errors.Add($"Zone '{zone.ZoneName}' vượt boundary khu chợ {exceedW:0.##}m theo chiều rộng.");
            }
            if (blockY + blockH > boundaryHeight + 0.01)
            {
                var exceedH = (blockY + blockH - boundaryHeight) / (physical ? request.PixelsPerMeter : 1);
                errors.Add($"Zone '{zone.ZoneName}' vượt boundary khu chợ {exceedH:0.##}m theo chiều dài.");
            }

            blocks.Add(new LayoutBlock
            {
                Id = Guid.NewGuid(),
                LayoutId = layout.Id,
                ZoneId = zone.Id,
                Type = "Zone",
                Name = zone.ZoneName,
                X = blockX,
                Y = blockY,
                Width = blockW,
                Height = blockH,
            });

            rowMaxH = Math.Max(rowMaxH, blockH + junctionReserve);
            col++;
            if (col >= zonesPerRow)
            {
                curX = request.StartX;
                curY += rowMaxH + request.ZoneMargin;
                rowMaxH = 0;
                col = 0;
            }
            else
            {
                curX += blockW + request.ZoneMargin;
            }
        }

        // Check overlap between blocks
        for (int i = 0; i < blocks.Count; i++)
        {
            for (int j = i + 1; j < blocks.Count; j++)
            {
                var b1 = blocks[i];
                var b2 = blocks[j];
                bool overlaps = !(b1.X + b1.Width <= b2.X || b2.X + b2.Width <= b1.X
                               || b1.Y + b1.Height <= b2.Y || b2.Y + b2.Height <= b1.Y);
                if (overlaps)
                {
                    errors.Add($"Zone '{b1.Name}' và Zone '{b2.Name}' đang bị chồng lấn (overlap) nhau.");
                }
            }
        }

        if (request.MarketWidthMeters.HasValue
            && (requiredWidth > request.MarketWidthMeters.Value * request.PixelsPerMeter + 0.01
                || requiredHeight > request.MarketLengthMeters!.Value * request.PixelsPerMeter + 0.01))
        {
            var exceedW = Math.Max(0, (requiredWidth - request.MarketWidthMeters.Value * request.PixelsPerMeter) / request.PixelsPerMeter);
            var exceedH = Math.Max(0, (requiredHeight - request.MarketLengthMeters!.Value * request.PixelsPerMeter) / request.PixelsPerMeter);
            if (exceedW > 0)
                errors.Add($"Layout vượt boundary khu chợ {exceedW:0.##}m theo chiều rộng.");
            if (exceedH > 0)
                errors.Add($"Layout vượt boundary khu chợ {exceedH:0.##}m theo chiều dài.");
        }

        return (blocks, errors, warnings);
    }

    private static List<PreviewSlot> BuildSlots(
        LayoutBlock block, Zone zone, ZoneGenerationConfig config,
        IReadOnlyCollection<BoothLocation> assignedLocations)
    {
        int capacity = zone.Capacity;
        int columns = GetColumns(capacity, config);
        var physical = config.ZoneWidthMeters.HasValue && config.BoothWidthMeters is > 0;
        var pixelsPerMeter = physical ? config.BoothWidth / config.BoothWidthMeters!.Value : 1;
        var padding = physical ? pixelsPerMeter : BlockPadding;
        var verticalGap = physical
            ? (config.VerticalGapMeters ?? config.HorizontalGapMeters ?? 0) * pixelsPerMeter
            : config.Gap;
        string prefix = (zone.ZoneCode ?? zone.ZoneName.Substring(0, 1)).ToUpperInvariant();
        var slots = new List<PreviewSlot>();

        int slotNum = 0;
        double currentY = block.Y + padding;
        int row = 0;

        while (slotNum < capacity)
        {
            if (config.AisleEveryNRows > 0 && row > 0 && row % config.AisleEveryNRows == 0)
                currentY += config.AisleWidth;

            var slotsInRow = Math.Min(columns, capacity - slotNum);
            var rowWidth = slotsInRow * config.BoothWidth
                + Math.Max(0, slotsInRow - 1) * config.Gap;
            var innerWidth = Math.Max(0, block.Width - padding * 2);
            double currentX = block.X + padding + Math.Max(0, (innerWidth - rowWidth) / 2);
            for (int c = 0; c < slotsInRow; c++)
            {
                string code = $"{prefix}-{(slotNum + 1):D2}";
                slots.Add(new PreviewSlot
                {
                    SlotCode = code,
                    RowIndex = row,
                    ColumnIndex = c,
                    X = currentX,
                    Y = currentY,
                    Width = config.BoothWidth,
                    Height = config.BoothHeight,
                    HasAssignedBooth = false  // enriched by service layer
                });
                currentX += config.BoothWidth + config.Gap;
                slotNum++;
            }

            currentY += config.BoothHeight + verticalGap;
            row++;
        }

        return slots;
    }

    private static LayoutEdge MakeEdge(Guid layoutId, Guid fromId, Guid toId,
        LayoutNode from, IReadOnlyList<LayoutNode> allNodes, DateTime now)
    {
        var toNode = allNodes.FirstOrDefault(n => n.Id == toId);
        double dist = toNode != null
            ? Math.Sqrt(Math.Pow((double)(from.Xcoordinate - toNode.Xcoordinate), 2)
                      + Math.Pow((double)(from.Ycoordinate - toNode.Ycoordinate), 2))
            : 1.0;

        return new LayoutEdge
        {
            Id = Guid.NewGuid(),
            LayoutId = layoutId,
            FromNodeId = fromId,
            ToNodeId = toId,
            Distance = (decimal)Math.Max(dist, 1),
            IsBidirectional = true,
            IsAccessible = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static int GetColumns(int capacity, ZoneGenerationConfig config)
        => config.Columns.HasValue && config.Columns.Value > 0
            ? config.Columns.Value
            : Math.Min(4, Math.Max(1, capacity));

    private static int GetRows(int capacity, ZoneGenerationConfig config)
    {
        int cols = GetColumns(capacity, config);
        return cols == 0 ? 1 : (int)Math.Ceiling((double)capacity / cols);
    }

    private static (int w, int h) ComputeCanvas(MarketLayout layout, List<LayoutBlock> blocks, GenerateLayoutRequest request)
    {
        if (request.MarketWidthMeters.HasValue && request.MarketLengthMeters.HasValue)
            return (
                (int)Math.Ceiling(request.MarketWidthMeters.Value * request.PixelsPerMeter),
                (int)Math.Ceiling(request.MarketLengthMeters.Value * request.PixelsPerMeter));
        if (!blocks.Any()) return (layout.Width, layout.Height);
        int reqW = (int)Math.Ceiling(blocks.Max(b => b.X + b.Width) + request.StartX);
        int reqH = (int)Math.Ceiling(blocks.Max(b => b.Y + b.Height) + request.StartY);
        if (!request.AutoExpandCanvas)
            return (layout.Width, layout.Height);
        return (Math.Max(layout.Width, reqW + 20), Math.Max(layout.Height, reqH + 20));
    }

    private static double GetVerticalGap(ZoneGenerationConfig config, GenerateLayoutRequest request)
        => config.VerticalGapMeters.HasValue
            ? config.VerticalGapMeters.Value * request.PixelsPerMeter
            : config.Gap;
}

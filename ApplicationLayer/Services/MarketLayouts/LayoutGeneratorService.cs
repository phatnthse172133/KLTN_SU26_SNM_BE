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
            // Keep the aisle point in the reserved corridor above the Zone,
            // aligned near its right edge so it never covers the Zone title.
            decimal junctionX = (decimal)Math.Max(zonePreview.X + 18, zonePreview.X + zonePreview.Width - 18);
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

        if (!newNodes.Any(n => n.NodeType == LayoutNodeType.Exit))
        {
            newNodes.Add(new LayoutNode
            {
                Id = Guid.NewGuid(),
                LayoutId = layout.Id,
                NodeType = LayoutNodeType.Exit,
                NodeName = "Main Exit",
                Xcoordinate = Math.Max(24, preview.CanvasWidth / 2),
                Ycoordinate = Math.Max(24, preview.CanvasHeight - 24),
                IsAccessible = true,
                IsStartingPoint = false,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // Physical/auto-fit layouts own explicit facility corridors. Re-anchor
        // the primary entrance and exit on every generation so older saved
        // coordinates cannot leave a gate inside a Zone after dimensions change.
        if ((request.AutoFitZones || request.MarketWidthMeters.HasValue) && layoutBlocks.Count > 0)
        {
            var zoneTop = layoutBlocks.Min(block => block.Y);
            var zoneBottom = layoutBlocks.Max(block => block.Y + block.Height);
            var centerX = (decimal)Math.Max(24, preview.CanvasWidth / 2);
            var entranceY = (decimal)Math.Max(16, zoneTop / 2);
            var exitY = (decimal)Math.Min(
                preview.CanvasHeight - 16,
                zoneBottom + Math.Max(16, (preview.CanvasHeight - zoneBottom) / 2));

            var primaryEntrance = newNodes
                .Where(n => n.NodeType == LayoutNodeType.Entrance)
                .OrderBy(n => n.CreatedAt)
                .First();
            primaryEntrance.Xcoordinate = centerX;
            primaryEntrance.Ycoordinate = entranceY;
            primaryEntrance.NodeName = "Main Entrance";
            primaryEntrance.IsStartingPoint = true;
            primaryEntrance.UpdatedAt = now;

            var primaryExit = newNodes
                .Where(n => n.NodeType == LayoutNodeType.Exit)
                .OrderBy(n => n.CreatedAt)
                .First();
            primaryExit.Xcoordinate = centerX;
            primaryExit.Ycoordinate = exitY;
            primaryExit.NodeName = "Main Exit";
            primaryExit.IsStartingPoint = false;
            primaryExit.UpdatedAt = now;
        }

        // Utility edges are preserved by MarketLayoutService.ApplyGenerationAsync,
        // which merges them with the generated edges before persisting.

        // ── Auto-generate paths from Entrances/Exits to Junctions ──
        // Each Entrance/Exit connects to its closest Junction.
        // All zone Junctions are chained together so every BoothSlot is reachable from any Entrance.
        var entrances = newNodes.Where(n => n.NodeType == LayoutNodeType.Entrance).ToList();
        var exits = newNodes.Where(n => n.NodeType == LayoutNodeType.Exit).ToList();
        var junctions = newNodes
            .Where(n => n.NodeType == LayoutNodeType.Junction)
            .OrderBy(j => j.Xcoordinate)
            .ThenBy(j => j.Ycoordinate)
            .ToList();
        if (junctions.Any())
        {
            // Build a grid-like mesh of junction edges so every zone is reachable
            // with shorter, more natural paths than a single linear chain.
            ConnectJunctionsAsGrid(layout.Id, junctions, newNodes, newEdges, now);

            // Connect each Entrance/Exit to its closest Junction
            foreach (var utilityNode in entrances.Concat(exits))
            {
                var closestJunction = junctions
                    .OrderBy(j => (j.Xcoordinate - utilityNode.Xcoordinate) * (j.Xcoordinate - utilityNode.Xcoordinate)
                                + (j.Ycoordinate - utilityNode.Ycoordinate) * (j.Ycoordinate - utilityNode.Ycoordinate))
                    .First();

                newEdges.Add(MakeEdge(layout.Id, utilityNode.Id, closestJunction.Id, utilityNode, newNodes, now));
            }
        }

        return new GenerationResult(layoutBlocks, newNodes, newEdges, preview.CanvasWidth, preview.CanvasHeight, preview);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static void ConnectJunctionsAsGrid(
        Guid layoutId,
        List<LayoutNode> junctions,
        IReadOnlyList<LayoutNode> allNodes,
        List<LayoutEdge> edges,
        DateTime now)
    {
        if (junctions.Count <= 1)
            return;

        // Group junctions into rows by Y proximity.  A tolerance of 20% of the
        // average junction spacing avoids splitting a row when the generator
        // places junctions at slightly different Y values within the same row.
        var sortedByY = junctions.OrderBy(j => j.Ycoordinate).ThenBy(j => j.Xcoordinate).ToList();
        var yTolerance = Math.Max(10.0, (double)(sortedByY[^1].Ycoordinate - sortedByY[0].Ycoordinate) / Math.Max(1, sortedByY.Count) * 0.3);

        var rows = new List<List<LayoutNode>>();
        foreach (var junction in sortedByY)
        {
            if (rows.Count > 0 && Math.Abs((double)(junction.Ycoordinate - rows[^1][0].Ycoordinate)) <= yTolerance)
                rows[^1].Add(junction);
            else
                rows.Add([junction]);
        }

        // Ensure each row is sorted by X for horizontal chaining
        foreach (var row in rows)
            row.Sort((a, b) => a.Xcoordinate.CompareTo(b.Xcoordinate));

        // Horizontal edges: connect adjacent junctions within each row
        foreach (var row in rows)
        {
            for (int i = 0; i < row.Count - 1; i++)
            {
                edges.Add(MakeEdge(layoutId, row[i].Id, row[i + 1].Id, row[i], allNodes, now));
            }
        }

        // Vertical edges: connect junctions in the same column across consecutive rows
        for (int r = 0; r < rows.Count - 1; r++)
        {
            var upperRow = rows[r];
            var lowerRow = rows[r + 1];

            // For each junction in the upper row, connect to the closest
            // junction in the lower row that hasn't been connected yet.
            var lowerUsed = new HashSet<Guid>();
            foreach (var upper in upperRow)
            {
                var bestLower = lowerRow
                    .Where(l => !lowerUsed.Contains(l.Id))
                    .OrderBy(l => Math.Abs(l.Xcoordinate - upper.Xcoordinate))
                    .ThenBy(l => Math.Abs(l.Ycoordinate - upper.Ycoordinate))
                    .FirstOrDefault();

                if (bestLower != null)
                {
                    lowerUsed.Add(bestLower.Id);
                    edges.Add(MakeEdge(layoutId, upper.Id, bestLower.Id, upper, allNodes, now));
                }
            }

            // Connect any remaining unconnected lower-row junctions to their
            // closest upper-row junction so no zone is left disconnected.
            foreach (var lower in lowerRow.Where(l => !lowerUsed.Contains(l.Id)))
            {
                var closestUpper = upperRow
                    .OrderBy(u => Math.Abs(u.Xcoordinate - lower.Xcoordinate))
                    .ThenBy(u => Math.Abs(u.Ycoordinate - lower.Ycoordinate))
                    .First();
                edges.Add(MakeEdge(layoutId, closestUpper.Id, lower.Id, closestUpper, allNodes, now));
            }
        }
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

        int zonesPerRow = request.ZonesPerRow ?? (int)Math.Ceiling(Math.Sqrt(configuredZones.Count));
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

            var boundaryWidth = request.MarketWidthMeters.HasValue
                ? request.MarketWidthMeters.Value * request.PixelsPerMeter
                : layout.Width;
            var boundaryHeight = request.MarketLengthMeters.HasValue
                ? request.MarketLengthMeters.Value * request.PixelsPerMeter
                : layout.Height;
            requiredWidth = Math.Max(requiredWidth, curX + blockW);
            requiredHeight = Math.Max(requiredHeight, blockYWithAisle + blockH);
            if (!request.AutoExpandCanvas && !physical
                && (curX + blockW > boundaryWidth || blockYWithAisle + blockH > boundaryHeight))
                errors.Add($"Zone '{zone.ZoneName}' does not fit inside the market boundary.");

            blocks.Add(new LayoutBlock
            {
                Id = Guid.NewGuid(),
                LayoutId = layout.Id,
                ZoneId = zone.Id,
                Type = "Zone",
                Name = zone.ZoneName,
                X = curX,
                Y = blockYWithAisle,
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

        if (!request.AutoExpandCanvas && request.MarketWidthMeters.HasValue
            && (requiredWidth > request.MarketWidthMeters.Value * request.PixelsPerMeter
                || requiredHeight > request.MarketLengthMeters!.Value * request.PixelsPerMeter))
        {
            errors.Add(
                $"The configured zones require at least {requiredWidth / request.PixelsPerMeter:0.##} m width "
                + $"and {requiredHeight / request.PixelsPerMeter:0.##} m length with the selected margins, "
                + $"but the market boundary is {request.MarketWidthMeters:0.##} m x {request.MarketLengthMeters:0.##} m. "
                + "Increase the market dimensions, reduce zone sizes or spacing, or change zones per row.");
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

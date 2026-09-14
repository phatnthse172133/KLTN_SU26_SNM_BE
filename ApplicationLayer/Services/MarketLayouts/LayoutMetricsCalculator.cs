using ApplicationLayer.DTOs.Responses;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketLayouts;

internal static class LayoutMetricsCalculator
{
    public static MarketLayoutMetricsResponse Calculate(
        MarketLayout layout,
        int? marketBoundaryWidthMeters,
        int? marketBoundaryHeightMeters,
        IReadOnlyCollection<LayoutBlock> blocks,
        IReadOnlyCollection<LayoutNode> nodes,
        IReadOnlyCollection<LayoutEdge> edges,
        IReadOnlyCollection<BoothLocation> locations,
        IReadOnlyCollection<Zone> zones)
    {
        var marketArea = Math.Max(0d,
            (marketBoundaryWidthMeters ?? 0) * (marketBoundaryHeightMeters ?? 0));
        var mapArea = Math.Max(0d,
            (layout.MarketWidthMeters ?? marketBoundaryWidthMeters ?? 0) *
            (layout.MarketLengthMeters ?? marketBoundaryHeightMeters ?? 0));
        var pixelsPerMeter = layout.PixelsPerMeter is > 0 ? layout.PixelsPerMeter.Value : 0d;

        var activeBlocks = blocks.Where(block => !block.IsDeleted && block.Type == "Zone").ToList();
        var zoneArea = pixelsPerMeter > 0
            ? activeBlocks.Sum(block => Math.Max(0, block.Width / pixelsPerMeter) * Math.Max(0, block.Height / pixelsPerMeter))
            : zones.Where(zone => activeBlocks.Any(block => block.ZoneId == zone.Id))
                .Sum(zone => Math.Max(0, zone.WidthMeters ?? 0) * Math.Max(0, zone.LengthMeters ?? 0));

        var slots = nodes.Where(node => !node.IsDeleted && node.NodeType == LayoutNodeType.BoothSlot).ToList();
        var zonesById = zones.ToDictionary(zone => zone.Id);
        var boothArea = slots.Sum(slot =>
        {
            if (!slot.ZoneId.HasValue || !zonesById.TryGetValue(slot.ZoneId.Value, out var zone))
                return 0d;
            if (zone.BoothWidthMeters is > 0 && zone.BoothLengthMeters is > 0)
                return zone.BoothWidthMeters.Value * zone.BoothLengthMeters.Value;
            if (pixelsPerMeter <= 0) return 0d;
            return Math.Max(0, zone.DefaultBoothWidth / pixelsPerMeter) *
                   Math.Max(0, zone.DefaultBoothHeight / pixelsPerMeter);
        });

        var assigned = locations
            .Where(location => !location.IsDeleted && !location.ReleasedAt.HasValue)
            .Select(location => location.LayoutNodeId)
            .ToHashSet();
        var reachableNodes = ReachableNodes(nodes, edges);
        var reachableAssigned = assigned.Count(reachableNodes.Contains);

        var zoneUtilization = Percent(zoneArea, mapArea);
        var occupancy = Percent(assigned.Count, slots.Count);
        var navigationReadiness = Percent(reachableAssigned, assigned.Count);
        var assessment = navigationReadiness < 80 || zoneUtilization > 90
            ? "Needs Review"
            : occupancy >= 70 && navigationReadiness >= 90 && zoneUtilization <= 85
                ? "Good"
                : "Balanced";

        return new MarketLayoutMetricsResponse
        {
            LayoutId = layout.Id,
            SectionCode = layout.SectionCode,
            SectionName = layout.SectionName,
            MarketAreaSquareMeters = Round(marketArea),
            MapAreaSquareMeters = Round(mapArea),
            MarketCoveragePercent = Round(Percent(mapArea, marketArea)),
            ZoneAreaSquareMeters = Round(zoneArea),
            ZoneUtilizationPercent = Round(zoneUtilization),
            BoothAreaSquareMeters = Round(boothArea),
            BoothUtilizationPercent = Round(Percent(boothArea, zoneArea)),
            WalkwayOpenAreaSquareMeters = Round(Math.Max(0, mapArea - zoneArea)),
            ZoneCount = activeBlocks.Select(block => block.ZoneId ?? block.Id).Distinct().Count(),
            TotalSlots = slots.Count,
            AssignedSlots = assigned.Count,
            OccupancyPercent = Round(occupancy),
            ReachableAssignedBooths = reachableAssigned,
            NavigationReadinessPercent = Round(navigationReadiness),
            Assessment = assessment
        };
    }

    private static HashSet<Guid> ReachableNodes(
        IReadOnlyCollection<LayoutNode> nodes,
        IReadOnlyCollection<LayoutEdge> edges)
    {
        var activeNodes = nodes.Where(node => !node.IsDeleted && node.IsAccessible).ToDictionary(node => node.Id);
        var starts = activeNodes.Values
            .Where(node => node.IsStartingPoint || node.NodeType == LayoutNodeType.Entrance)
            .Select(node => node.Id)
            .ToList();
        var adjacency = activeNodes.Keys.ToDictionary(id => id, _ => new List<Guid>());

        foreach (var edge in edges.Where(edge => !edge.IsDeleted && edge.IsAccessible))
        {
            if (!adjacency.ContainsKey(edge.FromNodeId) || !adjacency.ContainsKey(edge.ToNodeId)) continue;
            adjacency[edge.FromNodeId].Add(edge.ToNodeId);
            if (edge.IsBidirectional) adjacency[edge.ToNodeId].Add(edge.FromNodeId);
        }

        var visited = new HashSet<Guid>(starts);
        var queue = new Queue<Guid>(starts);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in adjacency[current])
                if (visited.Add(next)) queue.Enqueue(next);
        }
        return visited;
    }

    private static double Percent(double value, double total) =>
        total <= 0 ? 0 : value / total * 100d;

    private static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
using ApplicationLayer.DTOs.Responses;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.MapNavigation;

public class IndoorRouteInstructionBuilder : IIndoorRouteInstructionBuilder
{
    public IReadOnlyCollection<RouteInstructionResponse> Build(
        IReadOnlyList<Guid> orderedNodeIds, IReadOnlyList<Guid> traversedEdgeIds,
        IReadOnlyDictionary<Guid, LayoutNode> nodes, IReadOnlyDictionary<Guid, LayoutEdge> edges,
        decimal minimumSegmentMeters = 1m, LayoutNode? destinationBooth = null)
    {
        if (orderedNodeIds.Count == 0) return [];
        var result = new List<RouteInstructionResponse>
        {
            new() { InstructionCode = "START", AtNodeId = orderedNodeIds[0], ReferenceName = nodes[orderedNodeIds[0]].NodeName }
        };
        for (var segment = 0; segment < traversedEdgeIds.Count; segment++)
        {
            var edge = edges[traversedEdgeIds[segment]];
            if (edge.Distance < minimumSegmentMeters) continue;
            var code = segment == 0 ? "STRAIGHT" : ClassifyTurn(
                nodes[orderedNodeIds[segment - 1]], nodes[orderedNodeIds[segment]], nodes[orderedNodeIds[segment + 1]]);
            var instruction = new RouteInstructionResponse
            {
                InstructionCode = code, DistanceMeters = edge.Distance,
                AtNodeId = orderedNodeIds[segment], ReferenceName = nodes[orderedNodeIds[segment + 1]].NodeName
            };
            if (code == "STRAIGHT" && result.LastOrDefault()?.InstructionCode == "STRAIGHT")
                result[^1].DistanceMeters += instruction.DistanceMeters;
            else
                result.Add(instruction);
        }

        var arriveCode = "ARRIVE";
        var arriveName = nodes[orderedNodeIds[^1]].NodeName;
        if (destinationBooth is not null && orderedNodeIds.Count >= 2)
        {
            arriveCode = ClassifyArrival(nodes[orderedNodeIds[^2]], nodes[orderedNodeIds[^1]], destinationBooth);
            arriveName = destinationBooth.SlotCode ?? destinationBooth.NodeName ?? arriveName;
        }

        result.Add(new RouteInstructionResponse
        {
            InstructionCode = arriveCode, AtNodeId = orderedNodeIds[^1], ReferenceName = arriveName
        });
        return result;
    }

    public static string ClassifyTurn(LayoutNode previous, LayoutNode current, LayoutNode next)
    {
        var angle = SignedTurnDegrees(previous, current, next);
        if (angle is null) return "STRAIGHT";
        var magnitude = Math.Abs(angle.Value);
        if (magnitude <= 15d) return "STRAIGHT";
        if (magnitude >= 150d) return "UTURN";
        if (magnitude <= 45d) return angle > 0 ? "SLIGHT_RIGHT" : "SLIGHT_LEFT";
        return angle > 0 ? "TURN_RIGHT" : "TURN_LEFT";
    }

    public static string ClassifyArrival(LayoutNode previous, LayoutNode access, LayoutNode booth)
    {
        var angle = SignedTurnDegrees(previous, access, booth);
        if (angle is null || Math.Abs(angle.Value) <= 30d) return "ARRIVE_AHEAD";
        return angle > 0 ? "ARRIVE_RIGHT" : "ARRIVE_LEFT";
    }

    private static double? SignedTurnDegrees(LayoutNode previous, LayoutNode current, LayoutNode next)
    {
        var ax = (double)(current.Xcoordinate - previous.Xcoordinate);
        var ay = (double)(current.Ycoordinate - previous.Ycoordinate);
        var bx = (double)(next.Xcoordinate - current.Xcoordinate);
        var by = (double)(next.Ycoordinate - current.Ycoordinate);
        if ((ax == 0 && ay == 0) || (bx == 0 && by == 0)) return null;
        // Layout coordinates use a top-left origin with Y increasing downward.
        // A positive cross product is therefore a clockwise/right turn.
        return Math.Atan2(ax * by - ay * bx, ax * bx + ay * by) * 180d / Math.PI;
    }
}

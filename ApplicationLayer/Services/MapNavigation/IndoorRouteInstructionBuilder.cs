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
        var startNode = nodes[orderedNodeIds[0]];
        var startName = startNode.NodeName ?? "\u0110i\u1ec3m b\u1eaft \u0111\u1ea7u";
        var result = new List<RouteInstructionResponse>
        {
            new() { InstructionCode = "START", Maneuver = "depart", Text = $"B\u1eaft \u0111\u1ea7u t\u1eeb {startName}", AtNodeId = orderedNodeIds[0], ReferenceName = startNode.NodeName }
        };
        for (var segment = 0; segment < traversedEdgeIds.Count; segment++)
        {
            var edge = edges[traversedEdgeIds[segment]];
            if (edge.Distance < minimumSegmentMeters) continue;
            var code = segment == 0 ? "STRAIGHT" : ClassifyTurn(
                nodes[orderedNodeIds[segment - 1]], nodes[orderedNodeIds[segment]], nodes[orderedNodeIds[segment + 1]]);
            var refNode = nodes[orderedNodeIds[segment + 1]];
            var refName = refNode.NodeName ?? refNode.SlotCode;
            var instruction = new RouteInstructionResponse
            {
                InstructionCode = code,
                Maneuver = ToManeuver(code),
                Text = BuildInstructionText(code, edge.Distance, refName),
                DistanceMeters = edge.Distance,
                AtNodeId = orderedNodeIds[segment],
                ReferenceName = refName
            };
            if (code == "STRAIGHT" && result.LastOrDefault()?.InstructionCode == "STRAIGHT")
            {
                result[^1].DistanceMeters += instruction.DistanceMeters;
                result[^1].Text = BuildInstructionText("STRAIGHT", result[^1].DistanceMeters ?? 0, refName);
            }
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
            InstructionCode = arriveCode,
            Maneuver = ToManeuver(arriveCode),
            Text = BuildArrivalText(arriveCode, arriveName),
            AtNodeId = orderedNodeIds[^1],
            ReferenceName = arriveName
        });
        return result;
    }

    private static string ToManeuver(string code) => code switch
    {
        "START" => "depart",
        "STRAIGHT" => "continue",
        "SLIGHT_LEFT" => "slight-left",
        "SLIGHT_RIGHT" => "slight-right",
        "TURN_LEFT" => "turn-left",
        "TURN_RIGHT" => "turn-right",
        "UTURN" => "uturn",
        "ARRIVE" or "ARRIVE_AHEAD" => "arrive",
        "ARRIVE_LEFT" => "arrive-left",
        "ARRIVE_RIGHT" => "arrive-right",
        _ => "continue"
    };

    private static string BuildInstructionText(string code, decimal distance, string? refName)
    {
        var dist = distance > 0 ? $" {FormatMeters(distance)}" : "";
        return code switch
        {
            "STRAIGHT" => $"\u0110i th\u1eb3ng{dist}",
            "SLIGHT_LEFT" => $"H\u01a1i l\u1ec7ch tr\u00e1i{dist}",
            "SLIGHT_RIGHT" => $"H\u01a1i l\u1ec7ch ph\u1ea3i{dist}",
            "TURN_LEFT" => $"R\u1ebd tr\u00e1i{dist}",
            "TURN_RIGHT" => $"R\u1ebd ph\u1ea3i{dist}",
            "UTURN" => $"Quay \u0111\u1ea7u{dist}",
            _ => $"{code}{dist}"
        };
    }

    private static string BuildArrivalText(string code, string? refName)
    {
        return code switch
        {
            "ARRIVE" or "ARRIVE_AHEAD" => refName is not null ? $"\u0110\u1ebfn {refName}" : "\u0110\u1ebfn n\u01a1i",
            "ARRIVE_LEFT" => refName is not null ? $"\u0110\u1ebfn {refName} b\u00ean tr\u00e1i" : "\u0110\u1ebfn b\u00ean tr\u00e1i",
            "ARRIVE_RIGHT" => refName is not null ? $"\u0110\u1ebfn {refName} b\u00ean ph\u1ea3i" : "\u0110\u1ebfn b\u00ean ph\u1ea3i",
            _ => refName is not null ? $"\u0110\u1ebfn {refName}" : "\u0110\u1ebfn n\u01a1i"
        };
    }

    private static string FormatMeters(decimal meters)
    {
        if (meters < 10) return $"{Math.Round(meters * 10) / 10:F1} m";
        return $"{Math.Round(meters)} m";
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

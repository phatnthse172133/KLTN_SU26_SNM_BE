using DomainLayer.Entities;

namespace ApplicationLayer.Services.MapNavigation;

public sealed class DijkstraIndoorRouteSolver : IIndoorRouteSolver
{
    public IndoorRouteSolution Solve(
        IReadOnlyCollection<LayoutNode> nodes,
        IReadOnlyCollection<LayoutEdge> edges,
        Guid sourceNodeId,
        Guid destinationNodeId,
        IndoorRoutePolicy policy)
    {
        var usableNodes = nodes.Where(node => IndoorGraphPolicy.CanUseNode(node, policy))
            .ToDictionary(node => node.Id);
        if (!usableNodes.ContainsKey(sourceNodeId) || !usableNodes.ContainsKey(destinationNodeId))
            return IndoorRouteSolution.NotFound;

        var graph = usableNodes.Keys.ToDictionary(id => id, _ => new List<Arc>());
        foreach (var edge in edges.Where(edge => IndoorGraphPolicy.CanUseEdge(edge, policy)))
        {
            if (!graph.ContainsKey(edge.FromNodeId) || !graph.ContainsKey(edge.ToNodeId)) continue;
            graph[edge.FromNodeId].Add(new Arc(edge.ToNodeId, edge.Id, edge.Distance));
            if (edge.IsBidirectional)
                graph[edge.ToNodeId].Add(new Arc(edge.FromNodeId, edge.Id, edge.Distance));
        }

        var distances = graph.Keys.ToDictionary(id => id, _ => decimal.MaxValue);
        var previous = new Dictionary<Guid, (Guid NodeId, Guid EdgeId)>();
        var queue = new PriorityQueue<Guid, decimal>();
        distances[sourceNodeId] = 0;
        queue.Enqueue(sourceNodeId, 0);

        while (queue.TryDequeue(out var current, out var currentDistance))
        {
            if (currentDistance != distances[current]) continue;
            if (current == destinationNodeId) break;
            foreach (var arc in graph[current])
            {
                var candidate = currentDistance + arc.DistanceMeters;
                if (candidate >= distances[arc.ToNodeId]) continue;
                distances[arc.ToNodeId] = candidate;
                previous[arc.ToNodeId] = (current, arc.EdgeId);
                queue.Enqueue(arc.ToNodeId, candidate);
            }
        }

        if (distances[destinationNodeId] == decimal.MaxValue)
            return IndoorRouteSolution.NotFound;

        var nodePath = new List<Guid> { destinationNodeId };
        var edgePath = new List<Guid>();
        while (nodePath[^1] != sourceNodeId)
        {
            var step = previous[nodePath[^1]];
            edgePath.Add(step.EdgeId);
            nodePath.Add(step.NodeId);
        }
        nodePath.Reverse();
        edgePath.Reverse();
        return new IndoorRouteSolution(nodePath, edgePath, distances[destinationNodeId]);
    }

    private sealed record Arc(Guid ToNodeId, Guid EdgeId, decimal DistanceMeters);
}

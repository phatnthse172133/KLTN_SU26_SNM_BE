using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.MapNavigation;

public class MapNavigationService : IMapNavigationService
{
    private readonly INightMarketRepository _markets;
    private readonly IMarketLayoutRepository _layouts;
    private readonly IZoneRepository _zones;
    private readonly ILayoutNodeRepository _nodes;
    private readonly ILayoutEdgeRepository _edges;
    private readonly IBoothLocationRepository _locations;
    private readonly IBoothRepository _booths;
    private readonly IMapper _mapper;
    public MapNavigationService(INightMarketRepository markets, IMarketLayoutRepository layouts, IZoneRepository zones,
        ILayoutNodeRepository nodes, ILayoutEdgeRepository edges, IBoothLocationRepository locations,
        IBoothRepository booths, IMapper mapper)
        => (_markets, _layouts, _zones, _nodes, _edges, _locations, _booths, _mapper) =
            (markets, layouts, zones, nodes, edges, locations, booths, mapper);

    public async Task<ApiResponse<NightMarketMapResponse>> GetMapAsync(Guid nightMarketId, CancellationToken cancellationToken = default)
    {
        var market = await _markets.GetCustomerByIdAsync(nightMarketId, cancellationToken)
            ?? throw AppException.NotFound("Night market was not found.", "NIGHT_MARKET_NOT_FOUND");
        var layout = await _layouts.GetActiveMapAsync(nightMarketId, cancellationToken) ?? throw AppException.NotFound("This night market has no active map.");
        var nodes = await _nodes.GetByLayoutAsync(layout.Id, accessibleOnly: true, cancellationToken);
        var locations = await _locations.GetCustomerCurrentByLayoutAsync(layout.Id, cancellationToken);
        var zones = await _zones.GetActiveByNightMarketIdAsync(nightMarketId, cancellationToken);

        return ApiResponse<NightMarketMapResponse>.SuccessResponse(new()
        {
            NightMarket = new() { Id = market.Id, Name = market.Name },
            Layout = new() { Id = layout.Id, ImageUrl = layout.LayoutImageUrl, Width = layout.Width, Height = layout.Height },
            Zones = _mapper.Map<List<ZoneResponse>>(zones),
            StartingPoints = _mapper.Map<List<LayoutNodeResponse>>(nodes.Where(IsStartingPoint)),
            Booths = locations.Select(x => new MapBoothResponse
            {
                BoothId = x.BoothId, BoothName = x.Booth.BoothName, NodeId = x.LayoutNodeId,
                ZoneId = x.ZoneId, SlotNumber = x.SlotNumber, XCoordinate = x.Xcoordinate, YCoordinate = x.Ycoordinate
            }).ToList()
        });
    }

    public async Task<ApiResponse<PaginationResp<LayoutNodeResponse>>> GetStartingPointsAsync(
        Guid layoutId,
        PaginationReq pagination,
        CancellationToken cancellationToken = default)
    {
        await EnsureLayoutAsync(layoutId, cancellationToken);
        var page = await _nodes.GetStartingPointsPagedAsync(
            layoutId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<LayoutNodeResponse>>.SuccessResponse(
            _mapper.MapPage<LayoutNode, LayoutNodeResponse>(page, pagination));
    }

    public async Task<ApiResponse<NearestNodeResponse>> FindNearestNodeAsync(Guid layoutId, NearestNodeRequest request, CancellationToken cancellationToken = default)
    {
        var layout = await EnsureLayoutAsync(layoutId, cancellationToken);
        if (request.XCoordinate > layout.Width || request.YCoordinate > layout.Height)
            throw AppException.BadRequest("Coordinates must be inside the layout dimensions.");

        var nodes = await _nodes.GetByLayoutAsync(layoutId, accessibleOnly: true, cancellationToken);
        var nearest = nodes.Select(x => new { Node = x, Distance = Distance(request.XCoordinate, request.YCoordinate, x.Xcoordinate, x.Ycoordinate) })
            .OrderBy(x => x.Distance).FirstOrDefault() ?? throw AppException.NotFound("No accessible node was found.");

        return ApiResponse<NearestNodeResponse>.SuccessResponse(new()
        {
            FromNodeId = nearest.Node.Id, NodeName = nearest.Node.NodeName, Distance = nearest.Distance
        });
    }

    public async Task<ApiResponse<ShortestPathResponse>> FindRouteToBoothAsync(Guid layoutId, Guid fromNodeId, Guid boothId, CancellationToken cancellationToken = default)
    {
        var layout = await EnsureLayoutAsync(layoutId, cancellationToken);
        var nodes = await _nodes.GetByLayoutAsync(layoutId, accessibleOnly: true, cancellationToken);

        var byId = nodes.ToDictionary(x => x.Id);
        if (!byId.TryGetValue(fromNodeId, out var from)) 
            throw AppException.BadRequest("The starting node is invalid or inaccessible.");

        var booth = await _booths.GetCustomerByIdAsync(boothId, cancellationToken)
            ?? throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");
        if (booth.MarketId != layout.NightMarketId)
            throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");

        var location = await _locations.GetCurrentByBoothAsync(boothId, cancellationToken)
            ?? throw AppException.NotFound("The booth does not have an active location.");

        if (location.LayoutId != layoutId || !byId.ContainsKey(location.LayoutNodeId))
            throw AppException.BadRequest("The booth is not located on this accessible layout.");

        var edges = await _edges.GetByLayoutAsync(layoutId, accessibleOnly: true, cancellationToken);
        var (distance, ids) = Dijkstra(fromNodeId, location.LayoutNodeId, byId.Keys, edges);
        if (ids.Count == 0) throw AppException.NotFound("No accessible route to the booth was found.", "ROUTE_NOT_FOUND");
        return ApiResponse<ShortestPathResponse>.SuccessResponse(new()
        {
            LayoutId = layoutId,
            FromNode = new() { NodeId = from.Id, NodeName = from.NodeName },
            Destination = new() { BoothId = booth.Id, BoothName = booth.Name, NodeId = location.LayoutNodeId },
            TotalDistance = distance,
            EstimatedWalkingMinutes = Math.Max(1, (int)Math.Ceiling((double)distance / 80d)),
            Path = ids.Select((id, index) => new RoutePathNodeResponse
            {
                Sequence = index + 1, NodeId = id, XCoordinate = byId[id].Xcoordinate, YCoordinate = byId[id].Ycoordinate
            }).ToList()
        });
    }

    private async Task<MarketLayout> EnsureLayoutAsync(Guid id, CancellationToken token)
    {
        var layout = await _layouts.GetActiveByIdAsync(id, token) ?? throw AppException.NotFound("Market layout was not found.");
        if (layout.Status != DomainLayer.Enums.GeneralEnum.MarketLayoutStatus.Active)
            throw AppException.NotFound("An active market layout was not found.");
        if (!await _markets.CustomerVisibleExistsAsync(layout.NightMarketId, token))
            throw AppException.NotFound("Market layout was not found.");
        return layout;
    }
    private static bool IsStartingPoint(LayoutNode x) => x.IsStartingPoint ||
        x.NodeType is DomainLayer.Enums.GeneralEnum.LayoutNodeType.Entrance or DomainLayer.Enums.GeneralEnum.LayoutNodeType.Exit or DomainLayer.Enums.GeneralEnum.LayoutNodeType.Landmark;
    private static decimal Distance(decimal x1, decimal y1, decimal x2, decimal y2)
        => (decimal)Math.Sqrt(Math.Pow((double)(x1 - x2), 2) + Math.Pow((double)(y1 - y2), 2));

    private static (decimal Distance, List<Guid> Path) Dijkstra(Guid start, Guid target, IEnumerable<Guid> nodeIds, IEnumerable<LayoutEdge> edges)
    {
        var graph = nodeIds.ToDictionary(x => x, _ => new List<(Guid To, decimal Weight)>());
        foreach (var edge in edges)
        {
            // Corrupt/legacy zero or negative weights must never enter Dijkstra:
            // walking edges are physical distances and therefore strictly positive.
            if (edge.Distance <= 0 || !graph.ContainsKey(edge.FromNodeId) || !graph.ContainsKey(edge.ToNodeId)) continue;
            graph[edge.FromNodeId].Add((edge.ToNodeId, edge.Distance));
            if (edge.IsBidirectional) graph[edge.ToNodeId].Add((edge.FromNodeId, edge.Distance));
        }

        var distances = graph.Keys.ToDictionary(x => x, _ => decimal.MaxValue);
        var previous = new Dictionary<Guid, Guid>();
        var queue = new PriorityQueue<Guid, decimal>();
        distances[start] = 0; queue.Enqueue(start, 0);
        while (queue.TryDequeue(out var current, out var currentDistance))
        {
            if (currentDistance != distances[current]) continue;
            if (current == target) break;
            foreach (var (next, weight) in graph[current])
            {
                var candidate = currentDistance + weight;
                if (candidate >= distances[next]) continue;
                distances[next] = candidate; previous[next] = current; queue.Enqueue(next, candidate);
            }
        }
        if (distances[target] == decimal.MaxValue) return (0, []);
        var path = new List<Guid> { target };
        while (path[^1] != start) path.Add(previous[path[^1]]);
        path.Reverse();
        return (distances[target], path);
    }
}

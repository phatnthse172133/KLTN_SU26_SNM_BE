using ApplicationLayer.Configuration;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.MapNavigation;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Options;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.IndoorPositioning;

public class IndoorPositioningService : IIndoorPositioningService
{
    private readonly IMarketLayoutRepository _layouts;
    private readonly INightMarketRepository _markets;
    private readonly ILayoutNodeRepository _nodes;
    private readonly ILayoutEdgeRepository _edges;
    private readonly IBoothLocationRepository _locations;
    private readonly IBoothRepository _booths;
    private readonly IIndoorRouteSolver _solver;
    private readonly IndoorNavigationOptions _options;

    public IndoorPositioningService(IMarketLayoutRepository layouts, INightMarketRepository markets,
        ILayoutNodeRepository nodes, ILayoutEdgeRepository edges, IBoothLocationRepository locations,
        IBoothRepository booths, IIndoorRouteSolver solver, IOptions<IndoorNavigationOptions>? options = null)
    {
        (_layouts, _markets, _nodes, _edges, _locations, _booths, _solver) =
            (layouts, markets, nodes, edges, locations, booths, solver);
        _options = options?.Value ?? new IndoorNavigationOptions();
    }

    public async Task<ApiResponse<IndoorPositionEstimateResponse>> SnapAsync(Guid layoutId, SnapIndoorPositionRequest request, CancellationToken token = default)
    {
        var layout = await ActiveLayoutAsync(layoutId, request.ExpectedLayoutVersion, request.ExpectedGraphRevision, token);
        if (request.X > layout.Width || request.Y > layout.Height)
            throw AppException.BadRequest("Coordinates must be inside the layout dimensions.", "POSITION_OUTSIDE_LAYOUT");
        var policy = new IndoorRoutePolicy();
        var nodes = (await _nodes.GetByLayoutAsync(layoutId, cancellationToken: token)).ToDictionary(x => x.Id);
        var candidates = (await _edges.GetByLayoutAsync(layoutId, cancellationToken: token))
            .Where(edge => edge.LayoutId == layoutId && IndoorGraphPolicy.CanUseEdge(edge, policy)
                && nodes.TryGetValue(edge.FromNodeId, out var from) && from.LayoutId == layoutId && IndoorGraphPolicy.CanUseNode(from, policy)
                && nodes.TryGetValue(edge.ToNodeId, out var to) && to.LayoutId == layoutId && IndoorGraphPolicy.CanUseNode(to, policy))
            .Select(edge => Project(request.X, request.Y, edge, nodes[edge.FromNodeId], nodes[edge.ToNodeId]))
            .OrderBy(x => x.DistanceLayoutUnits).ThenBy(x => x.Edge.Id).ToList();
        var best = candidates.FirstOrDefault() ?? throw AppException.NotFound("No accessible walkway was found.", "NAVIGATION_GRAPH_UNAVAILABLE");
        var calibrated = layout.DistanceCalibrationStatus == DistanceCalibrationStatus.Calibrated && layout.MetersPerLayoutUnit is > 0;
        var distanceMeters = calibrated ? best.DistanceLayoutUnits * layout.MetersPerLayoutUnit : null;
        var tooFar = calibrated
            ? distanceMeters > (request.MaximumSnapDistanceMeters ?? _options.MaximumSnapDistanceMeters)
            : best.DistanceLayoutUnits > (request.MaximumSnapDistanceLayoutUnits ?? _options.MaximumSnapDistanceLayoutUnits);
        if (tooFar)
            throw AppException.BadRequest("The selected position is too far from an accessible walkway.", "SNAP_TOO_FAR");
        var threshold = calibrated ? distanceMeters!.Value : best.DistanceLayoutUnits;
        var maximum = calibrated ? request.MaximumSnapDistanceMeters ?? _options.MaximumSnapDistanceMeters
            : request.MaximumSnapDistanceLayoutUnits ?? _options.MaximumSnapDistanceLayoutUnits;
        var confidence = threshold <= maximum * .25m ? IndoorPositionConfidence.High
            : threshold <= maximum * .6m ? IndoorPositionConfidence.Medium : IndoorPositionConfidence.Low;
        return ApiResponse<IndoorPositionEstimateResponse>.SuccessResponse(new()
        {
            LayoutId = layout.Id, LayoutVersion = layout.Version, GraphRevision = layout.GraphRevision,
            Source = SourceCode(request.Source), LayoutX = request.X, LayoutY = request.Y,
            SnappedX = best.X, SnappedY = best.Y, SnappedEdgeId = best.Edge.Id,
            FromNodeId = best.Edge.FromNodeId, ToNodeId = best.Edge.ToNodeId, EdgeProgress = best.Progress,
            DistanceFromGraphLayoutUnits = best.DistanceLayoutUnits, DistanceFromGraphMeters = distanceMeters,
            Confidence = confidence.ToString().ToUpperInvariant(), CapturedAt = DateTime.UtcNow
        });
    }

    public async Task<ApiResponse<VirtualOriginRouteResponse>> RouteFromSnappedPositionAsync(Guid layoutId, RouteFromSnappedPositionRequest request, CancellationToken token = default)
    {
        var layout = await ActiveLayoutAsync(layoutId, request.ExpectedLayoutVersion, request.ExpectedGraphRevision, token);
        var policy = new IndoorRoutePolicy();
        var nodes = await _nodes.GetByLayoutAsync(layoutId, cancellationToken: token);
        var byId = nodes.ToDictionary(x => x.Id);
        var edges = await _edges.GetByLayoutAsync(layoutId, cancellationToken: token);
        var origin = edges.SingleOrDefault(x => x.Id == request.SnappedEdgeId && x.LayoutId == layoutId && IndoorGraphPolicy.CanUseEdge(x, policy))
            ?? throw AppException.BadRequest("The snapped edge is unavailable.", "SNAPPED_EDGE_UNAVAILABLE");
        if (!byId.TryGetValue(origin.FromNodeId, out var from) || !byId.TryGetValue(origin.ToNodeId, out var to)
            || !IndoorGraphPolicy.CanUseNode(from, policy) || !IndoorGraphPolicy.CanUseNode(to, policy))
            throw AppException.BadRequest("The snapped edge endpoints are unavailable.", "SNAPPED_EDGE_UNAVAILABLE");
        var projection = Project(request.SnappedX, request.SnappedY, origin, from, to);
        if (Math.Abs(projection.Progress - request.EdgeProgress) > .01m || projection.DistanceLayoutUnits > .01m)
            throw AppException.BadRequest("The virtual origin does not match the snapped edge.", "SNAPPED_POSITION_INVALID");
        var booth = await _booths.GetCustomerByIdAsync(request.BoothId, token)
            ?? throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");
        if (booth.MarketId != layout.NightMarketId) throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");
        var location = await _locations.GetCurrentByLayoutAndBoothAsync(layoutId, booth.Id, token)
            ?? throw AppException.NotFound("The booth does not have an active location.");

        var endpoints = new List<(Guid NodeId, decimal Partial)>();
        if (origin.IsBidirectional) endpoints.Add((origin.FromNodeId, origin.Distance * request.EdgeProgress));
        endpoints.Add((origin.ToNodeId, origin.Distance * (1m - request.EdgeProgress)));
        var solutions = endpoints.Select(x => (Endpoint: x, Route: _solver.Solve(nodes, edges, x.NodeId, location.LayoutNodeId, policy)))
            .Where(x => x.Route.Found).Select(x => (x.Endpoint.NodeId, x.Endpoint.Partial, x.Route, Total: x.Endpoint.Partial + x.Route.TotalDistanceMeters))
            .OrderBy(x => x.Total).ThenBy(x => x.NodeId).ToList();
        var best = solutions.FirstOrDefault();
        if (best.Route is null) throw AppException.NotFound("No accessible route to the booth was found.", "ROUTE_NOT_FOUND");
        var path = new List<VirtualRoutePointResponse> { new() { Sequence = 1, XCoordinate = request.SnappedX, YCoordinate = request.SnappedY, IsVirtualOrigin = true } };
        path.AddRange(best.Route.NodeIds.Select((id, index) => new VirtualRoutePointResponse
            { Sequence = index + 2, NodeId = id, XCoordinate = byId[id].Xcoordinate, YCoordinate = byId[id].Ycoordinate }));
        var traversed = (best.Partial > 0 ? new[] { origin.Id } : Array.Empty<Guid>()).Concat(best.Route.EdgeIds).ToList();
        var calibrated = layout.DistanceCalibrationStatus == DistanceCalibrationStatus.Calibrated;
        return ApiResponse<VirtualOriginRouteResponse>.SuccessResponse(new()
        {
            LayoutId = layout.Id, LayoutVersion = layout.Version, GraphRevision = layout.GraphRevision,
            OriginEdgeId = origin.Id, OriginEdgeProgress = request.EdgeProgress,
            Destination = new() { BoothId = booth.Id, BoothName = booth.Name, NodeId = location.LayoutNodeId },
            TotalDistanceMeters = best.Total,
            EstimatedWalkingMinutes = calibrated ? Math.Max(1, (int)Math.Ceiling((double)best.Total / _options.WalkingSpeedMetersPerMinute)) : null,
            IsDistanceCalibrated = calibrated, DistanceCalibrationStatus = layout.DistanceCalibrationStatus.ToString(),
            TraversedEdgeIds = traversed, Path = path
        });
    }

    private async Task<MarketLayout> ActiveLayoutAsync(Guid id, int version, int revision, CancellationToken token)
    {
        var layout = await _layouts.GetActiveByIdAsync(id, token) ?? throw AppException.NotFound("Market layout was not found.");
        if (layout.Status != MarketLayoutStatus.Active || !await _markets.CustomerVisibleExistsAsync(layout.NightMarketId, token))
            throw AppException.NotFound("An active market layout was not found.");
        if (layout.Version != version || layout.GraphRevision != revision)
            throw AppException.Conflict("The market map was updated. Reload before positioning.", "MAP_LAYOUT_VERSION_MISMATCH",
                new { CurrentLayoutVersion = layout.Version, CurrentGraphRevision = layout.GraphRevision });
        return layout;
    }

    public static EdgeProjection Project(decimal x, decimal y, LayoutEdge edge, LayoutNode from, LayoutNode to)
    {
        var dx = to.Xcoordinate - from.Xcoordinate; var dy = to.Ycoordinate - from.Ycoordinate;
        var lengthSquared = dx * dx + dy * dy;
        decimal progress = lengthSquared == 0 ? 0 : ((x - from.Xcoordinate) * dx + (y - from.Ycoordinate) * dy) / lengthSquared;
        progress = Math.Clamp(progress, 0m, 1m);
        var px = from.Xcoordinate + progress * dx; var py = from.Ycoordinate + progress * dy;
        var distance = (decimal)Math.Sqrt((double)((x - px) * (x - px) + (y - py) * (y - py)));
        return new(edge, px, py, progress, distance);
    }

    public sealed record EdgeProjection(LayoutEdge Edge, decimal X, decimal Y, decimal Progress, decimal DistanceLayoutUnits);
    public static string SourceCode(IndoorPositionSource source) => source switch
    {
        IndoorPositionSource.EntranceHandoff => "ENTRANCE_HANDOFF",
        IndoorPositionSource.MapTap => "MAP_TAP",
        IndoorPositionSource.ManualStartingPoint => "MANUAL_STARTING_POINT",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };
}

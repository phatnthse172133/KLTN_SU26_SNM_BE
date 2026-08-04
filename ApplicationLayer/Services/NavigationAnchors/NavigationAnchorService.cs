using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Configuration;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.MapNavigation;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services.NavigationAnchors;

public class NavigationAnchorService : INavigationAnchorService
{
    private readonly ILayoutNavigationAnchorRepository _anchors;
    private readonly IMarketLayoutRepository _layouts;
    private readonly ILayoutNodeRepository _nodes;
    private readonly ILayoutEdgeRepository _edges;
    private readonly IBoothLocationRepository _locations;
    private readonly IBoothRepository _booths;
    private readonly INightMarketRepository _markets;
    private readonly IIndoorRouteSolver _solver;
    private readonly IndoorNavigationOptions _navigationOptions;

    public NavigationAnchorService(ILayoutNavigationAnchorRepository anchors, IMarketLayoutRepository layouts,
        ILayoutNodeRepository nodes, ILayoutEdgeRepository edges, IBoothLocationRepository locations,
        IBoothRepository booths, INightMarketRepository markets, IIndoorRouteSolver solver,
        IOptions<IndoorNavigationOptions>? navigationOptions = null)
    {
        (_anchors, _layouts, _nodes, _edges, _locations, _booths, _markets, _solver) =
            (anchors, layouts, nodes, edges, locations, booths, markets, solver);
        _navigationOptions = navigationOptions?.Value ?? new IndoorNavigationOptions();
    }

    public async Task<ApiResponse<NavigationEntranceCollectionResponse>> GetEntrancesAsync(
        Guid marketId, Guid? targetBoothId = null, int? expectedLayoutVersion = null,
        int? expectedGraphRevision = null, CancellationToken token = default)
        => ApiResponse<NavigationEntranceCollectionResponse>.SuccessResponse(
            await BuildAsync(marketId, null, null, targetBoothId, expectedLayoutVersion, expectedGraphRevision, token));

    public async Task<ApiResponse<NavigationEntranceCollectionResponse>> GetNearestEntrancesAsync(
        Guid marketId, double latitude, double longitude, Guid? targetBoothId = null,
        int? expectedLayoutVersion = null, int? expectedGraphRevision = null, CancellationToken token = default)
    {
        ValidateCoordinates(latitude, longitude);
        return ApiResponse<NavigationEntranceCollectionResponse>.SuccessResponse(
            await BuildAsync(marketId, latitude, longitude, targetBoothId, expectedLayoutVersion, expectedGraphRevision, token));
    }

    public async Task<ApiResponse<NavigationAnchorAdminResponse>> CreateAsync(Guid layoutId, SaveNavigationAnchorRequest request, CancellationToken token = default)
    {
        var layout = await EditableLayoutAsync(layoutId, token);
        await ValidateRequestAsync(layout, request, null, token);
        var now = DateTime.UtcNow;
        var anchor = new LayoutNavigationAnchor { Id = Guid.NewGuid(), LayoutId = layoutId, CreatedAt = now, UpdatedAt = now };
        Apply(anchor, request);
        layout.GraphRevision = checked(layout.GraphRevision + 1); layout.UpdatedAt = now;
        await _anchors.AddAsync(anchor); await _anchors.SaveChangesAsync();
        return ApiResponse<NavigationAnchorAdminResponse>.SuccessResponse(ToAdmin(anchor), "Navigation anchor created.");
    }

    public async Task<ApiResponse<NavigationAnchorAdminResponse>> UpdateAsync(Guid anchorId, SaveNavigationAnchorRequest request, CancellationToken token = default)
    {
        var anchor = await _anchors.GetActiveByIdAsync(anchorId, token) ?? throw AppException.NotFound("Navigation anchor was not found.");
        var layout = await EditableLayoutAsync(anchor.LayoutId, token);
        await ValidateRequestAsync(layout, request, anchorId, token);
        Apply(anchor, request); anchor.UpdatedAt = DateTime.UtcNow;
        layout.GraphRevision = checked(layout.GraphRevision + 1); layout.UpdatedAt = anchor.UpdatedAt;
        _anchors.Update(anchor); await _anchors.SaveChangesAsync();
        return ApiResponse<NavigationAnchorAdminResponse>.SuccessResponse(ToAdmin(anchor), "Navigation anchor updated.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid anchorId, CancellationToken token = default)
    {
        var anchor = await _anchors.GetActiveByIdAsync(anchorId, token) ?? throw AppException.NotFound("Navigation anchor was not found.");
        var layout = await EditableLayoutAsync(anchor.LayoutId, token);
        anchor.IsDeleted = true; anchor.UpdatedAt = DateTime.UtcNow;
        layout.GraphRevision = checked(layout.GraphRevision + 1); layout.UpdatedAt = anchor.UpdatedAt;
        _anchors.Update(anchor); await _anchors.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { anchor.Id }, "Navigation anchor deleted.");
    }

    private async Task<NavigationEntranceCollectionResponse> BuildAsync(Guid marketId, double? latitude, double? longitude,
        Guid? targetBoothId, int? expectedVersion, int? expectedRevision, CancellationToken token)
    {
        if (!await _markets.CustomerVisibleExistsAsync(marketId, token))
            throw AppException.NotFound("Night market was not found.", "NIGHT_MARKET_NOT_FOUND");
        var layout = await _layouts.GetActiveMapAsync(marketId, token)
            ?? throw AppException.NotFound("This night market has no active map.");
        EnsureRevision(layout, expectedVersion, expectedRevision);
        var nodes = await _nodes.GetByLayoutAsync(layout.Id, cancellationToken: token);
        var nodeIds = nodes.ToDictionary(x => x.Id);
        var edges = await _edges.GetByLayoutAsync(layout.Id, cancellationToken: token);
        Guid? destinationNodeId = null;
        if (targetBoothId.HasValue)
        {
            var booth = await _booths.GetCustomerByIdAsync(targetBoothId.Value, token);
            if (booth?.MarketId != marketId) throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");
            destinationNodeId = (await _locations.GetCurrentByLayoutAndBoothAsync(layout.Id, targetBoothId.Value, token))?.LayoutNodeId
                ?? throw AppException.NotFound("The booth does not have an active location.");
        }
        var results = new List<NavigationEntranceResponse>();
        foreach (var anchor in await _anchors.GetByLayoutAsync(layout.Id, token))
        {
            if (!IsPublicEntrance(anchor, layout.Id, nodeIds)) continue;
            IndoorRouteSolution? route = null;
            if (destinationNodeId.HasValue)
            {
                route = _solver.Solve(nodes, edges, anchor.LayoutNodeId, destinationNodeId.Value, new IndoorRoutePolicy());
                if (!route.Found) continue;
            }
            var response = ToPublic(anchor);
            if (latitude.HasValue)
                response.OutdoorDistanceMeters = HaversineMeters(latitude.Value, longitude!.Value, (double)anchor.Latitude, (double)anchor.Longitude);
            if (route is { Found: true } && layout.DistanceCalibrationStatus == DistanceCalibrationStatus.Calibrated)
            {
                response.IndoorDistanceMeters = route.TotalDistanceMeters;
                response.IndoorEstimatedWalkingMinutes = Math.Max(1, (int)Math.Ceiling((double)route.TotalDistanceMeters / _navigationOptions.WalkingSpeedMetersPerMinute));
            }
            results.Add(response);
        }
        if (latitude.HasValue) results = results.OrderBy(x => x.OutdoorDistanceMeters).ThenBy(x => x.Name).ToList();
        return new() { MarketId = marketId, LayoutId = layout.Id, LayoutVersion = layout.Version, GraphRevision = layout.GraphRevision, Entrances = results };
    }

    private static bool IsPublicEntrance(LayoutNavigationAnchor anchor, Guid layoutId, IReadOnlyDictionary<Guid, LayoutNode> nodes)
        => anchor.LayoutId == layoutId && anchor.IsActive && anchor.IsCustomerAccessible && !anchor.IsDeleted &&
           anchor.AnchorType is NavigationAnchorType.Entrance or NavigationAnchorType.Both &&
           nodes.TryGetValue(anchor.LayoutNodeId, out var node) && node.LayoutId == layoutId && node.IsAccessible && !node.IsDeleted &&
           node.NodeType == LayoutNodeType.Entrance;

    private async Task ValidateRequestAsync(MarketLayout layout, SaveNavigationAnchorRequest request, Guid? excludeId, CancellationToken token)
    {
        ValidateCoordinates((double)request.Latitude, (double)request.Longitude);
        if (request.OpeningTime.HasValue != request.ClosingTime.HasValue)
            throw AppException.BadRequest("Opening and closing time must be provided together.", "ANCHOR_HOURS_INCOMPLETE");
        var node = await _nodes.GetActiveByIdAsync(request.LayoutNodeId, token) ?? throw AppException.NotFound("Layout node was not found.");
        if (node.LayoutId != layout.Id) throw AppException.BadRequest("Anchor node must belong to the same layout.", "ANCHOR_NODE_LAYOUT_MISMATCH");
        if (request.AnchorType is NavigationAnchorType.Entrance or NavigationAnchorType.Both && node.NodeType != LayoutNodeType.Entrance)
            throw AppException.BadRequest("An entrance anchor must use an Entrance node.", "ANCHOR_NODE_TYPE_INVALID");
        var code = request.AnchorCode.Trim().ToUpperInvariant();
        if (await _anchors.CodeExistsAsync(layout.Id, code, excludeId, token))
            throw AppException.Conflict("Anchor code already exists in this layout.", "ANCHOR_CODE_CONFLICT");
    }

    private async Task<MarketLayout> EditableLayoutAsync(Guid layoutId, CancellationToken token)
    {
        var layout = await _layouts.GetActiveByIdAsync(layoutId, token) ?? throw AppException.NotFound("Market layout was not found.");
        if (layout.Status == MarketLayoutStatus.Active)
            throw AppException.Conflict("Clone the active layout to a draft before editing navigation anchors.", "ACTIVE_LAYOUT_IMMUTABLE");
        return layout;
    }

    private static void Apply(LayoutNavigationAnchor anchor, SaveNavigationAnchorRequest request)
    {
        anchor.LayoutNodeId = request.LayoutNodeId; anchor.AnchorType = request.AnchorType;
        anchor.AnchorCode = request.AnchorCode.Trim().ToUpperInvariant(); anchor.DisplayName = request.DisplayName.Trim();
        anchor.Latitude = request.Latitude; anchor.Longitude = request.Longitude;
        anchor.IsCustomerAccessible = request.IsCustomerAccessible; anchor.IsActive = request.IsActive;
        anchor.OpeningTime = request.OpeningTime; anchor.ClosingTime = request.ClosingTime; anchor.IsDeleted = false;
    }

    private static NavigationEntranceResponse ToPublic(LayoutNavigationAnchor anchor) => new()
    {
        AnchorId = anchor.Id, NodeId = anchor.LayoutNodeId, AnchorCode = anchor.AnchorCode, Name = anchor.DisplayName,
        Latitude = anchor.Latitude, Longitude = anchor.Longitude,
        IsEntrance = anchor.AnchorType is NavigationAnchorType.Entrance or NavigationAnchorType.Both,
        IsExit = anchor.AnchorType is NavigationAnchorType.Exit or NavigationAnchorType.Both,
        IsAccessible = anchor.IsCustomerAccessible && (anchor.LayoutNode?.IsAccessible ?? true),
        OpeningTime = anchor.OpeningTime, ClosingTime = anchor.ClosingTime
    };
    private static NavigationAnchorAdminResponse ToAdmin(LayoutNavigationAnchor anchor)
    {
        var value = ToPublic(anchor);
        return new() { AnchorId = value.AnchorId, NodeId = value.NodeId, AnchorCode = value.AnchorCode, Name = value.Name,
            Latitude = value.Latitude, Longitude = value.Longitude, IsEntrance = value.IsEntrance, IsExit = value.IsExit,
            IsAccessible = value.IsAccessible, OpeningTime = value.OpeningTime, ClosingTime = value.ClosingTime,
            LayoutId = anchor.LayoutId, AnchorType = anchor.AnchorType.ToString(), IsActive = anchor.IsActive,
            IsCustomerAccessible = anchor.IsCustomerAccessible };
    }
    private static void EnsureRevision(MarketLayout layout, int? version, int? revision)
    {
        if ((version.HasValue && version != layout.Version) || (revision.HasValue && revision != layout.GraphRevision))
            throw AppException.Conflict("The market map was updated. Reload before handoff.", "MAP_LAYOUT_VERSION_MISMATCH",
                new { CurrentLayoutVersion = layout.Version, CurrentGraphRevision = layout.GraphRevision });
    }
    public static void ValidateCoordinates(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90 || !double.IsFinite(longitude) || longitude is < -180 or > 180)
            throw AppException.BadRequest("Latitude or longitude is invalid.", "INVALID_GEO_COORDINATE");
    }
    public static double HaversineMeters(double fromLatitude, double fromLongitude, double toLatitude, double toLongitude)
    {
        const double radius = 6_371_000d;
        static double Radians(double value) => value * Math.PI / 180d;
        var lat1 = Radians(fromLatitude); var lat2 = Radians(toLatitude);
        var dLat = lat2 - lat1; var dLon = Radians(toLongitude - fromLongitude);
        var a = Math.Pow(Math.Sin(dLat / 2d), 2d) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLon / 2d), 2d);
        return radius * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
    }
}

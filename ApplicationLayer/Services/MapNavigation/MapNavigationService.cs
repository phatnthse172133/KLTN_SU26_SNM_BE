using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Configuration;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.MarketLayouts;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Options;

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
    private readonly IIndoorRouteSolver _solver;
    private readonly IIndoorRouteInstructionBuilder _instructions;
    private readonly INavigationGraphBuilder _graphBuilder;
    private readonly IndoorNavigationOptions _navigationOptions;
    public MapNavigationService(INightMarketRepository markets, IMarketLayoutRepository layouts, IZoneRepository zones,
        ILayoutNodeRepository nodes, ILayoutEdgeRepository edges, IBoothLocationRepository locations,
        IBoothRepository booths, IMapper mapper, IIndoorRouteSolver? solver = null,
        IIndoorRouteInstructionBuilder? instructions = null, IOptions<IndoorNavigationOptions>? navigationOptions = null,
        INavigationGraphBuilder? graphBuilder = null)
    {
        (_markets, _layouts, _zones, _nodes, _edges, _locations, _booths, _mapper) =
            (markets, layouts, zones, nodes, edges, locations, booths, mapper);
        _solver = solver ?? new DijkstraIndoorRouteSolver();
        _instructions = instructions ?? new IndoorRouteInstructionBuilder();
        _navigationOptions = navigationOptions?.Value ?? new IndoorNavigationOptions();
        _graphBuilder = graphBuilder ?? new NavigationGraphBuilder(navigationOptions);
    }

    public async Task<ApiResponse<NightMarketMapResponse>> GetMapAsync(Guid nightMarketId, CancellationToken cancellationToken = default)
    {
        var market = await _markets.GetCustomerByIdAsync(nightMarketId, cancellationToken)
            ?? throw AppException.NotFound("Night market was not found.", "NIGHT_MARKET_NOT_FOUND");
        var layout = await _layouts.GetActiveMapAsync(nightMarketId, cancellationToken) ?? throw AppException.NotFound("This night market has no active map.");
        var nodes = await _nodes.GetByLayoutAsync(layout.Id, cancellationToken: cancellationToken);
        var edges = await _edges.GetByLayoutAsync(layout.Id, cancellationToken: cancellationToken);
        var blocks = await _layouts.GetBlocksByLayoutIdAsync(layout.Id, cancellationToken);
        var locations = await _locations.GetCustomerCurrentByLayoutAsync(layout.Id, cancellationToken);
        var zones = await _zones.GetActiveByNightMarketIdAsync(nightMarketId, cancellationToken: cancellationToken);

        return ApiResponse<NightMarketMapResponse>.SuccessResponse(new()
        {
            MarketId = market.Id,
            NightMarket = new() { Id = market.Id, Name = market.Name },
            Layout = new()
            {
                Id = layout.Id, Version = layout.Version, ImageUrl = layout.LayoutImageUrl,
                Width = layout.Width, Height = layout.Height,
                CoordinateUnit = layout.CoordinateUnit.ToString(),
                MetersPerLayoutUnit = layout.MetersPerLayoutUnit,
                DistanceCalibrationStatus = layout.DistanceCalibrationStatus.ToString(),
                GraphRevision = layout.GraphRevision,
                MarketWidthMeters = layout.MarketWidthMeters,
                MarketLengthMeters = layout.MarketLengthMeters,
                PixelsPerMeter = layout.PixelsPerMeter
            },
            Zones = _mapper.Map<List<ZoneResponse>>(zones),
            Blocks = blocks.Select(ToMapBlock).ToList(),
            Nodes = _mapper.Map<List<LayoutNodeResponse>>(nodes),
            Edges = _mapper.Map<List<LayoutEdgeResponse>>(edges),
            StartingPoints = _mapper.Map<List<LayoutNodeResponse>>(nodes.Where(node => node.IsAccessible && IsStartingPoint(node))),
            Booths = locations.Select(x => new MapBoothResponse
            {
                BoothId = x.BoothId, BoothName = x.Booth.BoothName, NodeId = x.LayoutNodeId,
                ZoneId = x.ZoneId, SlotNumber = x.SlotNumber, XCoordinate = x.Xcoordinate, YCoordinate = x.Ycoordinate,
                SlotCode = x.LayoutNode?.SlotCode, ZoneName = x.Zone?.ZoneName
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

    public async Task<ApiResponse<ShortestPathResponse>> FindRouteToBoothAsync(
        Guid layoutId, Guid fromNodeId, Guid boothId,
        CancellationToken cancellationToken = default,
        int? expectedLayoutVersion = null, int? expectedGraphRevision = null)
    {
        var layout = await EnsureLayoutAsync(layoutId, cancellationToken);
        if ((expectedLayoutVersion.HasValue && expectedLayoutVersion.Value != layout.Version) ||
            (expectedGraphRevision.HasValue && expectedGraphRevision.Value != layout.GraphRevision))
            throw AppException.Conflict(
                "The market map was updated. Reload it before requesting a route.",
                "MAP_LAYOUT_VERSION_MISMATCH",
                new { CurrentLayoutVersion = layout.Version, CurrentGraphRevision = layout.GraphRevision });

        var nodes = await _nodes.GetByLayoutAsync(layoutId, cancellationToken: cancellationToken);

        var byId = nodes.ToDictionary(x => x.Id);
        if (!byId.TryGetValue(fromNodeId, out var from) || !from.IsAccessible)
            throw AppException.BadRequest("The starting node is invalid or inaccessible.");

        var booth = await _booths.GetCustomerByIdAsync(boothId, cancellationToken)
            ?? throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");
        if (booth.MarketId != layout.NightMarketId)
            throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");

        var location = await _locations.GetCurrentByLayoutAndBoothAsync(layoutId, boothId, cancellationToken)
            ?? throw AppException.NotFound("The booth does not have an active location.");

        if (location.LayoutId != layoutId || !byId.ContainsKey(location.LayoutNodeId))
            throw AppException.BadRequest("The booth is not located on this accessible layout.");

        var blocks = await _layouts.GetBlocksByLayoutIdAsync(layoutId, cancellationToken);
        var persistedEdges = await _edges.GetByLayoutAsync(layoutId, cancellationToken: cancellationToken);
        var market = await _markets.GetByIdAsync(layout.NightMarketId);
        var scale = LayoutPhysicalCalibration.TryResolve(layout, market);
        // Layouts created before the physical Zone generator store a complete,
        // persisted navigation graph but have no LayoutBlocks. Keep those maps
        // navigable while modern layouts use the safer, transient corridor graph.
        var hasZoneBlocks = blocks.Any(block => !block.IsDeleted);
        var usesLegacyPersistedGraph = !hasZoneBlocks
            && nodes.Any(node => node.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothAccess);
        var graph = usesLegacyPersistedGraph
            // Only layouts using the retired BoothAccess model may fall back to
            // their persisted graph. A modern BoothSlot layout with no Zone
            // geometry is still invalid and must not receive a fabricated route.
            ? new NavigationGraph
            {
                Nodes = nodes.Where(node => !node.IsDeleted).ToList(),
                Edges = persistedEdges.Where(edge => !edge.IsDeleted && edge.IsAccessible).ToList()
            }
            : _graphBuilder.Build(layout, blocks, nodes, scale);
        if (!graph.IsValid)
            throw AppException.BadRequest(
                graph.InvalidReason ?? "navigation geometry invalid",
                "NAVIGATION_GEOMETRY_INVALID");

        var slot = byId[location.LayoutNodeId];
        var zones = await _zones.GetActiveByNightMarketIdAsync(layout.NightMarketId, cancellationToken: cancellationToken);
        var zoneName = slot.ZoneId.HasValue
            ? zones.FirstOrDefault(z => z.Id == slot.ZoneId.Value)?.ZoneName
            : null;
        var hasGeneratedAccess = graph.TryGetAccess(slot.SlotCode, out var access);
        var target = hasGeneratedAccess ? access : slot;
        var isLegacyAccessTarget = slot.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothAccess;
        if ((!hasGeneratedAccess && !isLegacyAccessTarget)
            || graph.UnreachableSlotCodes.Contains(slot.SlotCode ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            throw AppException.NotFound("No accessible route to the booth was found.", "ROUTE_NOT_FOUND");

        var routeNodes = graph.Nodes.ToDictionary(node => node.Id);
        if (!routeNodes.ContainsKey(fromNodeId))
            throw AppException.BadRequest("The starting node is invalid or inaccessible.");

        var solution = _solver.Solve(graph.Nodes, graph.Edges, fromNodeId, target.Id, new IndoorRoutePolicy());
        if (!solution.Found) throw AppException.NotFound("No accessible route to the booth was found.", "ROUTE_NOT_FOUND");
        var pathNodes = solution.NodeIds.Select(id => routeNodes[id]).ToList();
        var meters = usesLegacyPersistedGraph
            ? solution.TotalDistanceMeters
            : scale is { } physical
                ? LayoutDistance.PathMeters(pathNodes, physical)
                : (decimal?)null;
        var edgesById = graph.Edges.ToDictionary(x => x.Id);
        var instructions = _instructions.Build(solution.NodeIds, solution.EdgeIds, routeNodes, edgesById,
            _navigationOptions.MinimumInstructionSegmentMeters, slot).ToList();
        if (meters is null)
        {
            foreach (var instruction in instructions)
                instruction.DistanceMeters = null;
        }
        else
        {
            foreach (var instruction in instructions)
            {
                if (instruction.DistanceMeters is not null)
                    instruction.Text = RebuildTextWithDistance(instruction, meters.Value);
            }
        }
        return ApiResponse<ShortestPathResponse>.SuccessResponse(new()
        {
            LayoutId = layoutId,
            LayoutVersion = layout.Version,
            GraphRevision = layout.GraphRevision,
            FromNode = new() { NodeId = from.Id, NodeName = from.NodeName, NodeType = from.NodeType.ToString() },
            Destination = new() { BoothId = booth.Id, BoothName = booth.Name, NodeId = location.LayoutNodeId, SlotCode = slot.SlotCode, ZoneName = zoneName },
            TotalDistance = meters,
            TotalDistanceMeters = meters,
            EstimatedWalkingMinutes = meters is > 0
                ? Math.Max(1, (int)Math.Ceiling((double)meters.Value / _navigationOptions.WalkingSpeedMetersPerMinute))
                : null,
            IsDistanceCalibrated = meters is not null,
            DistanceCalibrationStatus = meters is not null ? "Calibrated" : "Uncalibrated",
            DistanceScaleSource = scale?.Source,
            ScaleX = scale?.ScaleX,
            ScaleY = scale?.ScaleY,
            TraversedEdgeIds = solution.EdgeIds,
            RouteStepCount = instructions.Count(x =>
                x.InstructionCode is not "START" && !x.InstructionCode.StartsWith("ARRIVE", StringComparison.Ordinal)),
            Instructions = instructions,
            Path = solution.NodeIds.Select((id, index) => new RoutePathNodeResponse
            {
                Sequence = index + 1, NodeId = id, XCoordinate = routeNodes[id].Xcoordinate, YCoordinate = routeNodes[id].Ycoordinate
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

    // Read-only projection of persisted zone rectangles. Coordinates are copied
    // as stored; ConfigJson is intentionally omitted from the customer map DTO.
    private static MapLayoutBlockResponse ToMapBlock(LayoutBlock block) => new()
    {
        Id = block.Id,
        ZoneId = block.ZoneId,
        Name = block.Name,
        Type = block.Type,
        Color = block.Zone?.Color,
        X = block.X,
        Y = block.Y,
        Width = block.Width,
        Height = block.Height,
        Rotation = block.Rotation
    };

    private static string RebuildTextWithDistance(RouteInstructionResponse instruction, decimal totalMeters)
    {
        if (instruction.DistanceMeters is not null and > 0)
        {
            var dist = instruction.DistanceMeters < 10
                ? $"{Math.Round(instruction.DistanceMeters.Value * 10) / 10:F1} m"
                : $"{Math.Round(instruction.DistanceMeters.Value)} m";
            return instruction.InstructionCode switch
            {
                "STRAIGHT" => $"\u0110i th\u1eb3ng {dist}",
                "SLIGHT_LEFT" => $"H\u01a1i l\u1ec7ch tr\u00e1i {dist}",
                "SLIGHT_RIGHT" => $"H\u01a1i l\u1ec7ch ph\u1ea3i {dist}",
                "TURN_LEFT" => $"R\u1ebd tr\u00e1i {dist}",
                "TURN_RIGHT" => $"R\u1ebd ph\u1ea3i {dist}",
                "UTURN" => $"Quay \u0111\u1ea7u {dist}",
                _ => $"{instruction.Text}"
            };
        }
        return instruction.Text;
    }

    private static decimal Distance(decimal x1, decimal y1, decimal x2, decimal y2)
        => (decimal)Math.Sqrt(Math.Pow((double)(x1 - x2), 2) + Math.Pow((double)(y1 - y2), 2));

}

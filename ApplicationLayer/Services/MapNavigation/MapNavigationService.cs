using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Configuration;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.MarketLayouts;
using ApplicationLayer.Services.MarketMaps;
using AutoMapper;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepositories;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MapNavigation;

public class MapNavigationService : IMapNavigationService
{
    private readonly INightMarketRepository _markets;
    private readonly IMarketMapRepository _marketMaps;
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
    private readonly ILogger<MapNavigationService> _logger;
    public MapNavigationService(INightMarketRepository markets, IMarketMapRepository marketMaps,
        IMarketLayoutRepository layouts, IZoneRepository zones,
        ILayoutNodeRepository nodes, ILayoutEdgeRepository edges, IBoothLocationRepository locations,
        IBoothRepository booths, IMapper mapper, IIndoorRouteSolver? solver = null,
        IIndoorRouteInstructionBuilder? instructions = null, IOptions<IndoorNavigationOptions>? navigationOptions = null,
        INavigationGraphBuilder? graphBuilder = null, ILogger<MapNavigationService>? logger = null)
    {
        (_markets, _layouts, _zones, _nodes, _edges, _locations, _booths, _mapper) =
            (markets, layouts, zones, nodes, edges, locations, booths, mapper);
        _marketMaps = marketMaps;
        _solver = solver ?? new DijkstraIndoorRouteSolver();
        _instructions = instructions ?? new IndoorRouteInstructionBuilder();
        _navigationOptions = navigationOptions?.Value ?? new IndoorNavigationOptions();
        _graphBuilder = graphBuilder ?? new NavigationGraphBuilder(navigationOptions);
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MapNavigationService>.Instance;
    }

    public async Task<ApiResponse<CustomerMarketMapResponse>> GetActiveMarketMapAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default)
    {
        var composition = await LoadActiveCompositionAsync(nightMarketId, cancellationToken);
        var layouts = composition.Layouts
            .Select(layout => ToOverallLayout(composition, layout))
            .ToArray();
        var representedZoneIds = composition.Layouts
            .SelectMany(layout => composition.BlocksByLayout[layout.Id]
                .Select(block => block.ZoneId)
                .Concat(layout.LayoutNodes.Select(node => node.ZoneId)))
            .OfType<Guid>()
            .Intersect(composition.Zones.Select(zone => zone.Id))
            .Distinct()
            .Count();

        return ApiResponse<CustomerMarketMapResponse>.SuccessResponse(new()
        {
            MarketId = composition.Market.Id,
            MarketMapId = composition.Map.Id,
            MarketMapName = composition.Map.Name,
            MarketMapVersion = composition.Map.Version,
            PublishedAt = composition.Map.PublishedAt!.Value,
            LayoutCount = layouts.Length,
            ZoneCount = representedZoneIds,
            BoothSlotCount = composition.Layouts.Sum(layout =>
                layout.LayoutNodes.Count(node => !node.IsDeleted && node.NodeType == LayoutNodeType.BoothSlot)),
            DefaultLayoutId = composition.Layouts.SingleOrDefault(layout => layout.IsDefaultView)?.Id,
            OverallBounds = composition.OverallBounds,
            Layouts = layouts
        });
    }

    public async Task<ApiResponse<IReadOnlyCollection<PublishedMapSectionResponse>>> GetPublishedMapsAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default)
    {
        var composition = await LoadActiveCompositionAsync(nightMarketId, cancellationToken);
        var result = new List<PublishedMapSectionResponse>();

        // Preserve the legacy section-list ordering while sourcing membership
        // from the authoritative Active MarketMap.
        foreach (var layout in composition.Layouts
                     .OrderByDescending(layout => layout.IsDefaultView)
                     .ThenBy(layout => layout.DisplayOrder)
                     .ThenBy(layout => layout.SectionName)
                     .ThenBy(layout => layout.Id))
        {
            var nodes = layout.LayoutNodes.Where(node => !node.IsDeleted).ToArray();
            var nodeIds = nodes.Select(node => node.Id).ToHashSet();
            var edges = layout.LayoutEdges.Where(edge =>
                !edge.IsDeleted && nodeIds.Contains(edge.FromNodeId) && nodeIds.Contains(edge.ToNodeId)).ToArray();
            var blocks = composition.BlocksByLayout[layout.Id];
            var locations = layout.BoothLocations.Where(location =>
                !location.IsDeleted && location.ReleasedAt == null).ToArray();
            var metrics = LayoutMetricsCalculator.Calculate(
                layout, composition.Market.BoundaryWidthMeters, composition.Market.BoundaryHeightMeters,
                blocks, nodes, edges, locations, composition.Zones);
            result.Add(new PublishedMapSectionResponse
            {
                Id = layout.Id,
                LayoutName = layout.LayoutName,
                SectionCode = layout.SectionCode,
                SectionName = layout.SectionName,
                Description = layout.Description,
                Version = layout.Version,
                OffsetXMeters = layout.OffsetXMeters,
                OffsetYMeters = layout.OffsetYMeters,
                WidthMeters = layout.MarketWidthMeters,
                LengthMeters = layout.MarketLengthMeters,
                IsDefaultView = layout.IsDefaultView,
                DisplayOrder = layout.DisplayOrder,
                Metrics = metrics
            });
        }

        return ApiResponse<IReadOnlyCollection<PublishedMapSectionResponse>>.SuccessResponse(result);
    }

    public async Task<ApiResponse<NightMarketMapResponse>> GetMapAsync(
        Guid nightMarketId, Guid? layoutId = null, CancellationToken cancellationToken = default)
    {
        var composition = await LoadActiveCompositionAsync(nightMarketId, cancellationToken);
        var layout = layoutId.HasValue
            ? composition.Layouts.FirstOrDefault(item => item.Id == layoutId.Value)
            : composition.Layouts
                .OrderByDescending(item => item.IsDefaultView)
                .ThenBy(item => item.DisplayOrder)
                .ThenBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .FirstOrDefault();
        if (layout is null)
            throw AppException.NotFound("The published map section was not found.", "MAP_SECTION_NOT_PUBLISHED");
        var nodes = layout.LayoutNodes.Where(node => !node.IsDeleted)
            .OrderBy(node => node.CreatedAt).ToArray();
        var nodeIds = nodes.Select(node => node.Id).ToHashSet();
        var nodesById = nodes.ToDictionary(node => node.Id);
        var edges = layout.LayoutEdges.Where(edge =>
                !edge.IsDeleted && nodeIds.Contains(edge.FromNodeId) && nodeIds.Contains(edge.ToNodeId))
            .OrderBy(edge => edge.CreatedAt).ToArray();
        var blocks = composition.BlocksByLayout[layout.Id];
        var locations = layout.BoothLocations.Where(location =>
            !location.IsDeleted && location.ReleasedAt == null).ToArray();
        var zones = LayoutZoneSnapshot.Resolve(composition.Zones, blocks);

        return ApiResponse<NightMarketMapResponse>.SuccessResponse(new()
        {
            MarketId = composition.Market.Id,
            NightMarket = new() { Id = composition.Market.Id, Name = composition.Market.Name },
            Layout = new()
            {
                Id = layout.Id, LayoutName = layout.LayoutName,
                SectionCode = layout.SectionCode, SectionName = layout.SectionName,
                Description = layout.Description, OffsetXMeters = layout.OffsetXMeters,
                OffsetYMeters = layout.OffsetYMeters, IsDefaultView = layout.IsDefaultView,
                DisplayOrder = layout.DisplayOrder,
                Version = layout.Version, ImageUrl = layout.LayoutImageUrl,
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
                SlotCode = nodesById.GetValueOrDefault(x.LayoutNodeId)?.SlotCode,
                ZoneName = zones.FirstOrDefault(z => z.Id == x.ZoneId)?.ZoneName
            }).ToList()
        });
    }

    private async Task<ActiveCustomerComposition> LoadActiveCompositionAsync(
        Guid nightMarketId, CancellationToken cancellationToken)
    {
        var market = await _markets.GetCustomerByIdAsync(nightMarketId, cancellationToken)
            ?? throw AppException.NotFound("Night market was not found.", "NIGHT_MARKET_NOT_FOUND");
        var map = await _marketMaps.GetCustomerActiveDetailAsync(nightMarketId, cancellationToken)
            ?? throw AppException.NotFound(
                "This night market has no published MarketMap.",
                "MARKET_MAP_NOT_PUBLISHED");

        var children = map.MarketLayouts.ToArray();
        if (!map.PublishedAt.HasValue || children.Length == 0 || children.Any(layout =>
                layout.MarketMapId != map.Id ||
                layout.NightMarketId != nightMarketId ||
                layout.IsDeleted ||
                layout.Status != MarketLayoutStatus.Active))
            throw CompositionInvariantError(nightMarketId, map.Id,
                "The Active MarketMap contains a missing, deleted, foreign, or non-active child layout.");

        var defaultCount = children.Count(layout => layout.IsDefaultView);
        if (defaultCount > 1)
            throw CompositionInvariantError(nightMarketId, map.Id,
                "The Active MarketMap contains more than one default layout.");

        // This query is an invariant check only. Response membership and all
        // child content come from the Active MarketMap aggregate loaded above.
        var operationalLayouts = await _layouts.GetPublishedMapsAsync(
            nightMarketId, cancellationToken);
        var childIds = children.Select(layout => layout.Id).ToHashSet();
        var operationalIds = operationalLayouts.Select(layout => layout.Id).ToHashSet();
        if (!childIds.SetEquals(operationalIds))
            throw CompositionInvariantError(nightMarketId, map.Id,
                "Active MarketMap children do not match the operational Active layouts.");

        var layouts = children
            .OrderBy(layout => layout.DisplayOrder)
            .ThenBy(layout => layout.SectionCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(layout => layout.Id)
            .ToArray();
        var blocks = await _layouts.GetBlocksByLayoutIdsAsync(childIds, cancellationToken);
        var blocksByLayout = layouts.ToDictionary(
            layout => layout.Id,
            layout => (IReadOnlyCollection<LayoutBlock>)blocks
                .Where(block => block.LayoutId == layout.Id && !block.IsDeleted)
                .OrderBy(block => block.DisplayOrder)
                .ThenBy(block => block.Id)
                .ToArray());
        var zones = await _zones.GetActiveByNightMarketIdAsync(
            nightMarketId, cancellationToken: cancellationToken);
        var overallBounds = MarketMapGeometry.CalculateOverallBounds(
            layouts, market.BoundaryWidthMeters, market.BoundaryHeightMeters)
            ?? throw CompositionInvariantError(nightMarketId, map.Id,
                "An Active MarketMap child has no valid physical calibration.");

        return new ActiveCustomerComposition(
            market, map, layouts, blocksByLayout, zones, overallBounds);
    }

    private CustomerMarketMapLayoutResponse ToOverallLayout(
        ActiveCustomerComposition composition, MarketLayout layout)
    {
        var scale = LayoutPhysicalCalibration.TryResolve(
            layout,
            composition.Market.BoundaryWidthMeters,
            composition.Market.BoundaryHeightMeters)
            ?? throw CompositionInvariantError(composition.Market.Id, composition.Map.Id,
                $"Layout '{layout.Id}' has no valid physical calibration.");
        var blocks = composition.BlocksByLayout[layout.Id];
        var nodes = layout.LayoutNodes.Where(node => !node.IsDeleted)
            .OrderBy(node => node.CreatedAt)
            .ThenBy(node => node.Id)
            .ToArray();
        var nodesById = nodes.ToDictionary(node => node.Id);
        var nodeIds = nodesById.Keys.ToHashSet();
        var edges = layout.LayoutEdges.Where(edge =>
                !edge.IsDeleted && nodeIds.Contains(edge.FromNodeId) && nodeIds.Contains(edge.ToNodeId))
            .OrderBy(edge => edge.CreatedAt)
            .ThenBy(edge => edge.Id)
            .ToArray();
        var locations = layout.BoothLocations.Where(location =>
                !location.IsDeleted && location.ReleasedAt == null && nodeIds.Contains(location.LayoutNodeId))
            .OrderBy(location => location.SlotNumber)
            .ThenBy(location => location.Booth.BoothName)
            .ToArray();
        var representedZoneIds = blocks.Select(block => block.ZoneId)
            .Concat(nodes.Select(node => node.ZoneId))
            .Concat(locations.Select(location => location.ZoneId))
            .OfType<Guid>()
            .ToHashSet();
        var layoutZones = LayoutZoneSnapshot.Resolve(
            composition.Zones.Where(zone => representedZoneIds.Contains(zone.Id)).ToArray(),
            blocks);
        var zoneNames = layoutZones.ToDictionary(zone => zone.Id, zone => zone.ZoneName);
        var zonesById = layoutZones.ToDictionary(zone => zone.Id);
        var nodeResponses = nodes.Select(node => ToOverallNode(layout, node, scale)).ToArray();
        var nodeResponsesById = nodeResponses.ToDictionary(node => node.Id);

        return new CustomerMarketMapLayoutResponse
        {
            LayoutId = layout.Id,
            LayoutName = layout.LayoutName,
            SectionCode = layout.SectionCode,
            SectionName = layout.SectionName,
            Description = layout.Description,
            Version = layout.Version,
            GraphRevision = layout.GraphRevision,
            OffsetXMeters = layout.OffsetXMeters,
            OffsetYMeters = layout.OffsetYMeters,
            PhysicalWidthMeters = layout.Width * scale.ScaleX,
            PhysicalHeightMeters = layout.Height * scale.ScaleY,
            ScaleX = scale.ScaleX,
            ScaleY = scale.ScaleY,
            ScaleSource = scale.Source,
            DisplayOrder = layout.DisplayOrder,
            IsDefaultView = layout.IsDefaultView,
            ImageUrl = layout.LayoutImageUrl,
            Width = layout.Width,
            Height = layout.Height,
            CoordinateUnit = layout.CoordinateUnit.ToString(),
            MetersPerLayoutUnit = layout.MetersPerLayoutUnit,
            DistanceCalibrationStatus = layout.DistanceCalibrationStatus.ToString(),
            PixelsPerMeter = layout.PixelsPerMeter,
            Zones = layoutZones.Select(zone => new CustomerMapZoneResponse
            {
                Id = zone.Id,
                ZoneName = zone.ZoneName,
                Description = zone.Description,
                Color = zone.Color,
                ZoneCode = zone.ZoneCode
            }).ToArray(),
            Blocks = blocks.Select(block => new CustomerOverallMapBlockResponse
            {
                Id = block.Id,
                LayoutId = layout.Id,
                ZoneId = block.ZoneId,
                Name = block.Name,
                Type = block.Type,
                Color = block.ZoneId.HasValue && zonesById.TryGetValue(block.ZoneId.Value, out var blockZone)
                    ? blockZone.Color
                    : block.Zone?.Color,
                X = block.X,
                Y = block.Y,
                Width = block.Width,
                Height = block.Height,
                Rotation = block.Rotation,
                GlobalXMeters = layout.OffsetXMeters + block.X * scale.ScaleX,
                GlobalYMeters = layout.OffsetYMeters + block.Y * scale.ScaleY,
                PhysicalWidthMeters = block.Width * scale.ScaleX,
                PhysicalHeightMeters = block.Height * scale.ScaleY
            }).ToArray(),
            Nodes = nodeResponses,
            Edges = _mapper.Map<List<LayoutEdgeResponse>>(edges),
            StartingPoints = nodes
                .Where(node => node.IsAccessible && IsStartingPoint(node))
                .Select(node => nodeResponsesById[node.Id])
                .ToArray(),
            Anchors = layout.NavigationAnchors
                .Where(anchor => IsPublicAnchor(anchor, layout.Id, nodesById))
                .OrderBy(anchor => anchor.DisplayName)
                .ThenBy(anchor => anchor.AnchorCode)
                .Select(ToPublicAnchor)
                .ToArray(),
            Booths = locations.Select(location => new CustomerOverallMapBoothResponse
            {
                MarketMapId = composition.Map.Id,
                LayoutId = layout.Id,
                SectionCode = layout.SectionCode,
                BoothId = location.BoothId,
                BoothName = location.Booth.BoothName,
                NodeId = location.LayoutNodeId,
                ZoneId = location.ZoneId,
                SlotNumber = location.SlotNumber,
                SlotCode = nodesById[location.LayoutNodeId].SlotCode,
                ZoneName = location.ZoneId.HasValue
                    ? zoneNames.GetValueOrDefault(location.ZoneId.Value)
                    : null,
                XCoordinate = location.Xcoordinate,
                YCoordinate = location.Ycoordinate,
                GlobalXMeters = layout.OffsetXMeters + (double)location.Xcoordinate * scale.ScaleX,
                GlobalYMeters = layout.OffsetYMeters + (double)location.Ycoordinate * scale.ScaleY
            }).ToArray()
        };
    }

    private static CustomerOverallMapNodeResponse ToOverallNode(
        MarketLayout layout, LayoutNode node, LayoutPhysicalScale scale)
        => new()
        {
            Id = node.Id,
            LayoutId = layout.Id,
            ZoneId = node.ZoneId,
            NodeName = node.NodeName,
            NodeType = node.NodeType.ToString(),
            XCoordinate = node.Xcoordinate,
            YCoordinate = node.Ycoordinate,
            GlobalXMeters = layout.OffsetXMeters + (double)node.Xcoordinate * scale.ScaleX,
            GlobalYMeters = layout.OffsetYMeters + (double)node.Ycoordinate * scale.ScaleY,
            IsAccessible = node.IsAccessible,
            IsStartingPoint = node.IsStartingPoint,
            SlotCode = node.SlotCode,
            RowIndex = node.RowIndex,
            ColumnIndex = node.ColumnIndex,
            LayoutBlockId = node.LayoutBlockId
        };

    private static bool IsPublicAnchor(
        LayoutNavigationAnchor anchor,
        Guid layoutId,
        IReadOnlyDictionary<Guid, LayoutNode> nodes)
        => anchor.LayoutId == layoutId &&
           anchor.IsActive &&
           anchor.IsCustomerAccessible &&
           !anchor.IsDeleted &&
           nodes.TryGetValue(anchor.LayoutNodeId, out var node) &&
           node.LayoutId == layoutId &&
           node.IsAccessible &&
           !node.IsDeleted &&
           ((anchor.AnchorType is NavigationAnchorType.Entrance or NavigationAnchorType.Both &&
             node.NodeType == LayoutNodeType.Entrance) ||
            (anchor.AnchorType == NavigationAnchorType.Exit &&
             node.NodeType == LayoutNodeType.Exit));

    private static NavigationEntranceResponse ToPublicAnchor(LayoutNavigationAnchor anchor)
        => new()
        {
            AnchorId = anchor.Id,
            NodeId = anchor.LayoutNodeId,
            AnchorCode = anchor.AnchorCode,
            Name = anchor.DisplayName,
            Latitude = anchor.Latitude,
            Longitude = anchor.Longitude,
            IsEntrance = anchor.AnchorType is NavigationAnchorType.Entrance or NavigationAnchorType.Both,
            IsExit = anchor.AnchorType is NavigationAnchorType.Exit or NavigationAnchorType.Both,
            IsAccessible = true,
            OpeningTime = anchor.OpeningTime,
            ClosingTime = anchor.ClosingTime
        };

    private AppException CompositionInvariantError(
        Guid nightMarketId, Guid marketMapId, string reason)
    {
        _logger.LogError(
            "Active MarketMap consistency failure for NightMarket {NightMarketId}, MarketMap {MarketMapId}: {Reason}",
            nightMarketId, marketMapId, reason);
        return AppException.Conflict(
            "The published market map is temporarily inconsistent.",
            "ACTIVE_MARKET_MAP_INCONSISTENT");
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
        var layout = await EnsureRouteLayoutAsync(layoutId, cancellationToken);
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
        zones = LayoutZoneSnapshot.Resolve(zones, blocks);
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

    private async Task<MarketLayout> EnsureRouteLayoutAsync(Guid id, CancellationToken token)
    {
        var requested = await _layouts.GetActiveByIdAsync(id, token)
            ?? throw AppException.NotFound("Market layout was not found.");
        if (!await _markets.CustomerVisibleExistsAsync(requested.NightMarketId, token))
            throw AppException.NotFound("Market layout was not found.");
        if (requested.Status == DomainLayer.Enums.GeneralEnum.MarketLayoutStatus.Active)
            return requested;

        // A customer may legitimately keep an old published layout in memory
        // while the owner activates a newer MarketMap. Return a structured
        // conflict (with the matching current section) instead of disguising
        // that state transition as a missing layout.
        var published = await _layouts.GetPublishedMapsAsync(requested.NightMarketId, token);
        var current = published.FirstOrDefault(layout =>
                string.Equals(layout.SectionCode, requested.SectionCode, StringComparison.OrdinalIgnoreCase))
            ?? published.FirstOrDefault();
        throw AppException.Conflict(
            "The market map was updated. Reload it before requesting a route.",
            "MAP_LAYOUT_VERSION_MISMATCH",
            new
            {
                CurrentMarketMapId = current?.MarketMapId,
                CurrentLayoutId = current?.Id,
                CurrentLayoutVersion = current?.Version,
                CurrentGraphRevision = current?.GraphRevision
            });
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

    private sealed record ActiveCustomerComposition(
        NightMarketCustomerReadModel Market,
        MarketMap Map,
        IReadOnlyList<MarketLayout> Layouts,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<LayoutBlock>> BlocksByLayout,
        IReadOnlyCollection<Zone> Zones,
        MarketMapOverallBoundsResponse OverallBounds);

}

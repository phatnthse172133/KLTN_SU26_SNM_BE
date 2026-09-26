using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Subscriptions;
using ApplicationLayer.Services.Realtime;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepositories;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketLayouts;

public class MarketLayoutService : IMarketLayoutService
{
    private const string GeneralAreaZoneName = "General Area";
    private const string GeneralAreaZoneCode = "G";
    private const double DefaultPixelsPerMeter = 10.0;
    private const int MaxCanvasDimension = 50000;

    private readonly IMarketLayoutRepository _layouts;
    private readonly IMarketMapRepository _marketMaps;
    private readonly IZoneRepository _zones;
    private readonly INightMarketRepository _nightMarkets;
    private readonly IMapper _mapper;
    private readonly ILayoutGraphValidationService _graphValidation;
    private readonly ISubscriptionEntitlementService _entitlements;
    private readonly IMarketResourceQuotaService _resourceQuota;
    private readonly ILayoutGeneratorService _generator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeEventPublisher _eventPublisher;

    public MarketLayoutService(
        IMarketLayoutRepository layouts, IMarketMapRepository marketMaps, IZoneRepository zones,
        INightMarketRepository nightMarkets, IMapper mapper, ILayoutGraphValidationService graphValidation,
        ISubscriptionEntitlementService entitlements, IMarketResourceQuotaService resourceQuota,
        ILayoutGeneratorService generator, IUnitOfWork unitOfWork,
        IRealtimeEventPublisher eventPublisher)
    {
        _layouts = layouts;
        _marketMaps = marketMaps;
        _zones = zones;
        _nightMarkets = nightMarkets;
        _mapper = mapper;
        _graphValidation = graphValidation;
        _entitlements = entitlements;
        _resourceQuota = resourceQuota;
        _generator = generator;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
    }

    public async Task<ApiResponse<MarketLayoutResponse>> CloneAsync(
        Guid layoutId, CloneMarketLayoutDraftRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var source = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(source, actorId, cancellationToken);
        var market = await EnsureNightMarketExistsAsync(source.NightMarketId, cancellationToken);
        if (market.MarketOwnerId is Guid ownerId)
        {
            if (!await _entitlements.HasActiveMarketSubscriptionAsync(ownerId))
                throw AppException.Forbidden("An active Market subscription is required to perform this action.", "MARKET_SUBSCRIPTION_REQUIRED");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _layouts.AcquireMarketLockAsync(source.NightMarketId, cancellationToken);
            var now = DateTime.UtcNow;
            var targetMap = await _marketMaps.GetOrCreateLegacyDraftAsync(
                source.NightMarketId, now, cancellationToken);
            if (market.MarketOwnerId is Guid quotaOwnerId)
                await _resourceQuota.EnsureCanAddLayoutsAsync(
                    quotaOwnerId, targetMap.Id, 1, cancellationToken);

            var clone = await _layouts.CloneToDraftAsync(
                layoutId, targetMap.Id, request.LayoutName, now, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return ApiResponse<MarketLayoutResponse>.SuccessResponse(
                _mapper.Map<MarketLayoutResponse>(clone), "Layout copied to a draft. The active layout is unchanged.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<PaginationResp<MarketLayoutResponse>>> GetAllAsync(
        Guid nightMarketId, MarketLayoutListRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var market = await EnsureNightMarketExistsAsync(nightMarketId, cancellationToken);
        EnsureOwnership(market, actorId);
        var page = await _layouts.GetActivePagedAsync(
            nightMarketId, request.Keyword, request.Status, request.Page, request.PageSize,
            request.SortBy, request.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase),
            cancellationToken);

        return ApiResponse<PaginationResp<MarketLayoutResponse>>.SuccessResponse(
            _mapper.MapPage<MarketLayout, MarketLayoutResponse>(page, request));
    }

    public async Task<ApiResponse<MarketLayoutResponse>> GetByIdAsync(
        Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipOnlyAsync(layout, actorId, cancellationToken);
        return ApiResponse<MarketLayoutResponse>.SuccessResponse(
            _mapper.Map<MarketLayoutResponse>(layout));
    }

    public async Task<ApiResponse<MarketLayoutResponse>> CreateAsync(
        Guid nightMarketId, CreateMarketLayoutRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var market = await EnsureNightMarketExistsAsync(nightMarketId, cancellationToken);
        EnsureOwnership(market, actorId);
        if (market.MarketOwnerId.HasValue)
        {
            var hasSubscription = await _entitlements.HasActiveMarketSubscriptionAsync(market.MarketOwnerId.Value);
            if (!hasSubscription)
                throw AppException.Forbidden("An active Market subscription is required to perform this action.", "MARKET_SUBSCRIPTION_REQUIRED");
        }

        var sectionCode = NormalizeSectionCode(request.SectionCode, request.LayoutName);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize layout creation per market. This makes both the package-limit
            // check and MAX(version) + 1 allocation atomic across app instances.
            await _layouts.AcquireMarketLockAsync(nightMarketId, cancellationToken);
            var now = DateTime.UtcNow;
            var targetMap = await _marketMaps.GetOrCreateLegacyDraftAsync(
                nightMarketId, now, cancellationToken);

            if (market.MarketOwnerId is Guid ownerId)
                await _resourceQuota.EnsureCanAddLayoutsAsync(
                    ownerId, targetMap.Id, 1, cancellationToken);

            var resolvedVersion = await _layouts.GetNextVersionAsync(nightMarketId, sectionCode, cancellationToken);
            await ValidateIdentityAsync(
                nightMarketId, sectionCode, request.LayoutName, resolvedVersion, null, cancellationToken);

            var layout = _mapper.Map<MarketLayout>(request);
            layout.Id = Guid.NewGuid();
            layout.NightMarketId = nightMarketId;
            layout.MarketMapId = targetMap.Id;
            layout.SectionCode = sectionCode;
            layout.SectionName = string.IsNullOrWhiteSpace(request.SectionName) ? request.LayoutName.Trim() : request.SectionName.Trim();
            layout.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            layout.OffsetXMeters = request.OffsetXMeters;
            layout.OffsetYMeters = request.OffsetYMeters;
            layout.MarketWidthMeters = request.MapWidthMeters ?? market.BoundaryWidthMeters;
            layout.MarketLengthMeters = request.MapLengthMeters ?? market.BoundaryHeightMeters;
            layout.DisplayOrder = request.DisplayOrder;
            layout.IsDefaultView = false;
            layout.Version = resolvedVersion;
            layout.Status = MarketLayoutStatus.Draft;
            layout.IsDeleted = false;
            layout.CreatedAt = now;
            layout.UpdatedAt = now;
            ValidateSectionInsideMarket(layout, market);
            ApplyDerivedCanvasDimensions(layout);

            await _layouts.AddAsync(layout);
            await _layouts.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return ApiResponse<MarketLayoutResponse>.SuccessResponse(
                _mapper.Map<MarketLayoutResponse>(layout),
                "Market layout created successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<MarketLayoutResponse>> UpdateAsync(
        Guid layoutId, UpdateMarketLayoutRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        await EnsureDraftLayoutEditableAsync(layout, cancellationToken);

        var market = await EnsureNightMarketExistsAsync(layout.NightMarketId, cancellationToken);
        var sectionCode = NormalizeSectionCode(request.SectionCode, layout.SectionCode);
        await ValidateIdentityAsync(layout.NightMarketId, sectionCode, request.LayoutName, layout.Version, layoutId, cancellationToken);
        _mapper.Map(request, layout);
        layout.SectionCode = sectionCode;
        layout.SectionName = string.IsNullOrWhiteSpace(request.SectionName) ? request.LayoutName.Trim() : request.SectionName.Trim();
        layout.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        layout.OffsetXMeters = request.OffsetXMeters;
        layout.OffsetYMeters = request.OffsetYMeters;
        layout.MarketWidthMeters = request.MapWidthMeters ?? layout.MarketWidthMeters ?? market.BoundaryWidthMeters;
        layout.MarketLengthMeters = request.MapLengthMeters ?? layout.MarketLengthMeters ?? market.BoundaryHeightMeters;
        layout.DisplayOrder = request.DisplayOrder;
        ValidateSectionInsideMarket(layout, market);
        await EnsureExistingGraphFitsDerivedCanvasAsync(layout, cancellationToken);
        ApplyDerivedCanvasDimensions(layout);
        layout.UpdatedAt = DateTime.UtcNow;
        _layouts.Update(layout);

        await _layouts.SaveChangesAsync();
        return ApiResponse<MarketLayoutResponse>.SuccessResponse(_mapper.Map<MarketLayoutResponse>(layout), "Market layout updated successfully.");
    }

    public async Task<ApiResponse<MarketLayoutResponse>> UpdateImageAsync(
        Guid layoutId, UpdateMarketLayoutImageRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        await EnsureDraftLayoutEditableAsync(layout, cancellationToken);

        if (!Uri.TryCreate(request.LayoutImageUrl, UriKind.RelativeOrAbsolute, out var imageUri)
            || (imageUri.IsAbsoluteUri && imageUri.Scheme is not ("http" or "https"))
            || (!imageUri.IsAbsoluteUri && !request.LayoutImageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)))
            throw AppException.BadRequest(
                "The uploaded layout image URL is invalid.",
                "LAYOUT_IMAGE_URL_INVALID");

        EnsureCanvasMatchesPhysicalArea(layout, request.Width, request.Height);

        _mapper.Map(request, layout);
        layout.UpdatedAt = DateTime.UtcNow;
        _layouts.Update(layout);

        await _layouts.SaveChangesAsync();
        return ApiResponse<MarketLayoutResponse>.SuccessResponse(_mapper.Map<MarketLayoutResponse>(layout), "Market layout image updated successfully.");
    }


    public async Task<ApiResponse<MarketLayoutResponse>> UpdateDimensionsAsync(
        Guid layoutId, UpdateMarketLayoutDimensionsRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        await EnsureDraftLayoutEditableAsync(layout, cancellationToken);

        EnsureCanvasMatchesPhysicalArea(layout, request.Width, request.Height);

        var nodes = (await _layouts.GetNodesByLayoutIdAsync(layoutId, cancellationToken)).ToList();
        var outsideNodes = nodes
            .Where(n => n.Xcoordinate > request.Width || n.Ycoordinate > request.Height)
            .ToList();
        if (outsideNodes.Count > 0)
        {
            var requiredWidth = (int)Math.Ceiling(nodes.Max(n => n.Xcoordinate));
            var requiredHeight = (int)Math.Ceiling(nodes.Max(n => n.Ycoordinate));
            var examples = string.Join(", ", outsideNodes
                .Take(5)
                .Select(n => string.IsNullOrWhiteSpace(n.SlotCode)
                    ? (string.IsNullOrWhiteSpace(n.NodeName) ? n.Id.ToString("N")[..8] : n.NodeName)
                    : n.SlotCode));
            var suffix = outsideNodes.Count > 5 ? " and more" : string.Empty;

            throw AppException.BadRequest(
                $"The new layout size {request.Width} × {request.Height}px is too small for the existing map. " +
                $"Keep at least {requiredWidth} × {requiredHeight}px, or move/regenerate the {outsideNodes.Count} node(s) outside the new boundary " +
                $"({examples}{suffix}).",
                "LAYOUT_DIMENSIONS_TOO_SMALL");
        }

        layout.Width = request.Width;
        layout.Height = request.Height;
        layout.UpdatedAt = DateTime.UtcNow;
        _layouts.Update(layout);

        await _layouts.SaveChangesAsync();
        return ApiResponse<MarketLayoutResponse>.SuccessResponse(_mapper.Map<MarketLayoutResponse>(layout), "Market layout dimensions updated successfully.");
    }

    private static void EnsureCanvasMatchesPhysicalArea(MarketLayout layout, int width, int height)
    {
        if (layout.MarketWidthMeters is not > 0 || layout.MarketLengthMeters is not > 0)
            return;

        var pixelsPerMeter = layout.PixelsPerMeter is > 0
            ? layout.PixelsPerMeter.Value
            : DefaultPixelsPerMeter;
        var expectedWidth = (int)Math.Ceiling(layout.MarketWidthMeters.Value * pixelsPerMeter);
        var expectedHeight = (int)Math.Ceiling(layout.MarketLengthMeters.Value * pixelsPerMeter);
        if (width == expectedWidth && height == expectedHeight)
            return;

        throw AppException.BadRequest(
            $"Canvas dimensions are calculated automatically from this map area's physical size. " +
            $"Use {expectedWidth} × {expectedHeight}px ({layout.MarketWidthMeters:0.##} × {layout.MarketLengthMeters:0.##}m at {pixelsPerMeter:0.##}px/m).",
            "LAYOUT_CANVAS_DERIVED");
    }

    private static void ApplyDerivedCanvasDimensions(MarketLayout layout)
    {
        if (layout.MarketWidthMeters is not > 0 || layout.MarketLengthMeters is not > 0)
            return;

        var pixelsPerMeter = layout.PixelsPerMeter is > 0
            ? layout.PixelsPerMeter.Value
            : DefaultPixelsPerMeter;
        var width = (int)Math.Ceiling(layout.MarketWidthMeters.Value * pixelsPerMeter);
        var height = (int)Math.Ceiling(layout.MarketLengthMeters.Value * pixelsPerMeter);
        if (width > MaxCanvasDimension || height > MaxCanvasDimension)
            throw AppException.BadRequest(
                $"The calculated drawing canvas cannot exceed {MaxCanvasDimension:N0}px per side. " +
                "Reduce the map area's physical size before saving.",
                "LAYOUT_CANVAS_TOO_LARGE");

        layout.PixelsPerMeter = pixelsPerMeter;
        layout.Width = width;
        layout.Height = height;
    }

    private async Task EnsureExistingGraphFitsDerivedCanvasAsync(
        MarketLayout layout,
        CancellationToken cancellationToken)
    {
        if (layout.MarketWidthMeters is not > 0 || layout.MarketLengthMeters is not > 0)
            return;

        var pixelsPerMeter = layout.PixelsPerMeter is > 0
            ? layout.PixelsPerMeter.Value
            : DefaultPixelsPerMeter;
        var expectedWidth = (int)Math.Ceiling(layout.MarketWidthMeters.Value * pixelsPerMeter);
        var expectedHeight = (int)Math.Ceiling(layout.MarketLengthMeters.Value * pixelsPerMeter);
        var nodes = await _layouts.GetNodesByLayoutIdAsync(layout.Id, cancellationToken);
        var blocks = await _layouts.GetBlocksByLayoutIdAsync(layout.Id, cancellationToken);
        var outsideNodeCount = nodes.Count(node =>
            node.Xcoordinate < 0 || node.Ycoordinate < 0 ||
            node.Xcoordinate > expectedWidth || node.Ycoordinate > expectedHeight);
        var outsideBlockCount = blocks.Count(block =>
            block.X < 0 || block.Y < 0 ||
            block.X + block.Width > expectedWidth || block.Y + block.Height > expectedHeight);

        if (outsideNodeCount == 0 && outsideBlockCount == 0)
            return;

        throw AppException.BadRequest(
            $"This map area cannot be reduced to {layout.MarketWidthMeters:0.##} × {layout.MarketLengthMeters:0.##}m " +
            $"because {outsideBlockCount} zone(s) and {outsideNodeCount} map point(s) would fall outside it. " +
            "Move or regenerate the zones first, then resize the map area.",
            "LAYOUT_AREA_TOO_SMALL");
    }

    public async Task<ApiResponse<SaveGraphResponse>> SaveGraphTransactionalAsync(
        Guid layoutId, SaveGraphRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        await EnsureDraftLayoutEditableAsync(layout, cancellationToken);

        if (layout.GraphRevision != request.ExpectedGraphRevision ||
            Math.Abs((layout.UpdatedAt - request.ExpectedUpdatedAt).TotalSeconds) > 1)
        {
            throw AppException.Conflict(
                "This layout was changed in another session. Reload it before saving.",
                "LAYOUT_CONCURRENCY_CONFLICT",
                new
                {
                    CurrentUpdatedAt = layout.UpdatedAt,
                    CurrentGraphRevision = layout.GraphRevision,
                    request.ExpectedUpdatedAt,
                    request.ExpectedGraphRevision
                });
        }

        var market = await EnsureNightMarketExistsAsync(layout.NightMarketId, cancellationToken);
        var existingNodes = await _layouts.GetNodesByLayoutIdAsync(layoutId, cancellationToken);
        var activeZones = await _zones.GetActiveByNightMarketIdAsync(
            layout.NightMarketId, cancellationToken: cancellationToken);
        if (market.MarketOwnerId != null)
        {
            var boothSlotsCount = request.Nodes.Count(n => n.NodeType == LayoutNodeType.BoothSlot);
            await _resourceQuota.EnsureBoothSlotCapacityAsync(
                market.MarketOwnerId.Value, layout.MarketMapId, layout.Id,
                boothSlotsCount, cancellationToken);

            if (request.Nodes.Any(node => node.ZoneId.HasValue)
                && !await _entitlements.CanUseZoneManagementAsync(market.MarketOwnerId.Value))
            {
                var generalZoneId = FindGeneralAreaZone(activeZones)?.Id;
                var existingZoneAssignments = existingNodes.ToDictionary(n => n.Id, n => n.ZoneId);
                if (request.Nodes.Any(node => node.ZoneId.HasValue
                    && node.ZoneId != generalZoneId
                    && (!existingZoneAssignments.TryGetValue(node.Id, out var currentZoneId)
                        || currentZoneId != node.ZoneId)))
                {
                    throw AppException.Forbidden(
                        "Your current subscription does not include zone management.",
                        "ZONE_MANAGEMENT_NOT_INCLUDED");
                }
            }
        }

        var existingBlocks = await _layouts.GetBlocksByLayoutIdAsync(layoutId, cancellationToken);
        var existingEdges = await _layouts.GetEdgesByLayoutIdAsync(layoutId, cancellationToken);
        var editorData = await _layouts.GetEditorLayoutAsync(layoutId, cancellationToken)
            ?? throw AppException.NotFound("Market layout was not found.");

        if (request.Nodes.Any(node => node.Id == Guid.Empty)
            || request.Edges.Any(edge => edge.Id == Guid.Empty))
            throw AppException.BadRequest(
                "Every node and path must have an identifier.",
                "LAYOUT_GRAPH_ID_REQUIRED");

        if (request.Nodes.GroupBy(node => node.Id).Any(group => group.Count() > 1)
            || request.Edges.GroupBy(edge => edge.Id).Any(group => group.Count() > 1))
            throw AppException.BadRequest(
                "The layout contains duplicate node or path identifiers.",
                "LAYOUT_GRAPH_DUPLICATE_ID");

        if (request.Nodes.Any(n => n.XCoordinate < 0 || n.XCoordinate > layout.Width || n.YCoordinate < 0 || n.YCoordinate > layout.Height))
            throw AppException.BadRequest(
                "All map points must be inside the layout boundaries.",
                "LAYOUT_NODE_OUT_OF_BOUNDS");

        if (request.Edges.Any(e => e.FromNodeId == e.ToNodeId))
            throw AppException.BadRequest(
                "A path cannot connect a map point to itself.",
                "LAYOUT_EDGE_SELF_REFERENCE");

        var requestedNodeIds = request.Nodes.Select(node => node.Id).ToHashSet();
        if (request.Edges.Any(edge =>
                !requestedNodeIds.Contains(edge.FromNodeId)
                || !requestedNodeIds.Contains(edge.ToNodeId)))
            throw AppException.BadRequest(
                "Every path endpoint must belong to this layout.",
                "LAYOUT_EDGE_NODE_INVALID");

        if (request.Edges
            .GroupBy(edge => edge.FromNodeId.CompareTo(edge.ToNodeId) < 0
                ? (edge.FromNodeId, edge.ToNodeId)
                : (edge.ToNodeId, edge.FromNodeId))
            .Any(group => group.Count() > 1))
            throw AppException.BadRequest(
                "The layout contains duplicate paths.",
                "LAYOUT_EDGE_DUPLICATE");

        var validZones = activeZones.Select(zone => zone.Id).ToHashSet();
        if (request.Nodes.Any(node => node.ZoneId.HasValue && !validZones.Contains(node.ZoneId.Value)))
            throw AppException.BadRequest(
                "A selected zone does not belong to this night market.",
                "LAYOUT_ZONE_INVALID");

        var removedNodeIds = existingNodes.Select(node => node.Id)
            .Except(requestedNodeIds)
            .ToHashSet();
        if (editorData.BoothLocations.Any(location =>
                !location.IsDeleted && removedNodeIds.Contains(location.LayoutNodeId)))
            throw AppException.Conflict(
                "Release the booth assigned to a slot before deleting that slot.",
                "LAYOUT_SLOT_OCCUPIED");

        var now = DateTime.UtcNow;
        var existingBlocksById = existingBlocks.ToDictionary(block => block.Id);
        var existingNodesById = existingNodes.ToDictionary(node => node.Id);
        var existingEdgesById = existingEdges.ToDictionary(edge => edge.Id);

        var blocks = request.Blocks.Select(b => {
            var existing = existingBlocksById.TryGetValue(b.Id, out var e) ? e : null;
            return new LayoutBlock
            {
                Id = b.Id,
                LayoutId = layoutId,
                ZoneId = existing?.ZoneId,
                Type = existing?.Type ?? "Zone",
                Name = existing?.Name ?? "",
                X = b.X,
                Y = b.Y,
                Width = existing?.Width ?? 0,
                Height = existing?.Height ?? 0,
                Rotation = existing?.Rotation ?? 0,
                DisplayOrder = existing?.DisplayOrder ?? 0,
                ConfigJson = existing?.ConfigJson,
                IsDeleted = false,
                CreatedAt = existing?.CreatedAt ?? now,
                UpdatedAt = now
            };
        }).ToList();

        // Preserve any block that wasn't updated by the FE (drag-drop)
        foreach (var existing in existingBlocks)
        {
            if (!request.Blocks.Any(b => b.Id == existing.Id))
            {
                existing.UpdatedAt = now;
                blocks.Add(existing);
            }
        }

        foreach (var block in blocks)
        {
            var blockErrors = LayoutGeometryValidator.ValidateBlockMove(layout, block, block.X, block.Y, blocks);
            if (blockErrors.Count > 0)
                throw AppException.BadRequest(blockErrors[0], "LAYOUT_BLOCK_INVALID");
        }

        var nodes = request.Nodes.Select(node => new LayoutNode
        {
            Id = node.Id,
            LayoutId = layoutId,
            ZoneId = node.ZoneId,
            NodeName = node.NodeName?.Trim(),
            NodeType = node.NodeType,
            Xcoordinate = node.XCoordinate,
            Ycoordinate = node.YCoordinate,
            IsAccessible = node.IsAccessible,
            IsStartingPoint = node.IsStartingPoint,
            SlotCode = node.NodeType == LayoutNodeType.BoothSlot ? node.SlotCode?.Trim() : null,
            RowIndex = node.NodeType == LayoutNodeType.BoothSlot ? node.RowIndex : null,
            ColumnIndex = node.NodeType == LayoutNodeType.BoothSlot ? node.ColumnIndex : null,
            LayoutBlockId = node.NodeType == LayoutNodeType.BoothSlot ? node.LayoutBlockId : null,
            IsDeleted = false,
            CreatedAt = existingNodesById.TryGetValue(node.Id, out var existingNode)
                ? existingNode.CreatedAt
                : now,
            UpdatedAt = now
        }).ToList();
        var duplicateSlotCode = nodes
            .Where(node => node.NodeType == LayoutNodeType.BoothSlot)
            .GroupBy(node => node.SlotCode?.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
        if (duplicateSlotCode is not null)
            throw AppException.BadRequest(
                string.IsNullOrWhiteSpace(duplicateSlotCode.Key)
                    ? "Every booth slot must have a slot code."
                    : $"Booth slot code '{duplicateSlotCode.Key}' is duplicated.",
                "LAYOUT_SLOT_CODE_DUPLICATED");
        foreach (var node in nodes)
        {
            var nodeErrors = LayoutGeometryValidator.ValidateNodeMove(
                layout, node, node.Xcoordinate, node.Ycoordinate, nodes, blocks);
            if (nodeErrors.Count > 0)
                throw AppException.Validation(
                    $"Map point '{node.SlotCode ?? node.NodeName ?? node.Id.ToString()}': {string.Join(" ", nodeErrors)}",
                    new Dictionary<string, string[]> { ["nodes"] = nodeErrors.ToArray() },
                    "LAYOUT_NODE_GEOMETRY_INVALID");
        }

        var edges = request.Edges.Select(edge => new LayoutEdge
        {
            Id = edge.Id,
            LayoutId = layoutId,
            FromNodeId = edge.FromNodeId,
            ToNodeId = edge.ToNodeId,
            // Distance is derived from geometry. Recalculate it on every graph
            // save so moving a zone and its child points cannot leave stale
            // routing weights from the previous position.
            Distance = CalculateDistance(nodes, edge.FromNodeId, edge.ToNodeId),
            IsBidirectional = edge.IsBidirectional,
            IsAccessible = edge.IsAccessible,
            IsDeleted = false,
            CreatedAt = existingEdgesById.TryGetValue(edge.Id, out var existingEdge)
                ? existingEdge.CreatedAt
                : now,
            UpdatedAt = now
        }).ToList();

        if (edges.Any(edge => edge.Distance <= 0))
            throw AppException.BadRequest(
                "Path distance must be greater than zero.",
                "LAYOUT_EDGE_DISTANCE_INVALID");

        // A graph save is also used after moving a gate/zone.  Older maps can
        // still contain stale straight edges that cross a newly regenerated
        // Zone, and rejecting the whole payload made every subsequent edit
        // fail with HTTP 400.  Drop only those unsafe segments; valid edges
        // and booth-access links are kept, while the transient customer
        // router rebuilds safe corridors from the current geometry.
        edges.RemoveAll(edge => EdgeCrossesGeneratedBlock(edge, nodes, blocks));

        var saved = await _layouts.SaveGraphTransactionalAsync(
            layoutId,
            request.ExpectedUpdatedAt,
            request.ExpectedGraphRevision,
            blocks,
            nodes,
            edges,
            null,
            cancellationToken);
        return ApiResponse<SaveGraphResponse>.SuccessResponse(new SaveGraphResponse
        {
            LayoutId = layoutId,
            BlockCount = blocks.Count,
            NodeCount = nodes.Count,
            EdgeCount = edges.Count,
            GraphRevision = saved.GraphRevision,
            UpdatedAt = saved.UpdatedAt
        }, "Layout saved successfully.");
    }

    private static decimal CalculateDistance(
        IReadOnlyCollection<LayoutNode> nodes,
        Guid fromNodeId,
        Guid toNodeId)
    {
        var from = nodes.First(node => node.Id == fromNodeId);
        var to = nodes.First(node => node.Id == toNodeId);
        return (decimal)Math.Sqrt(
            Math.Pow((double)(from.Xcoordinate - to.Xcoordinate), 2)
            + Math.Pow((double)(from.Ycoordinate - to.Ycoordinate), 2));
    }

    public async Task<ApiResponse<MarketLayoutEditorDataResponse>> GetEditorDataAsync(
        Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await _layouts.GetEditorLayoutAsync(layoutId, cancellationToken)
                     ?? throw AppException.NotFound("Market layout was not found.");
        await EnsureLayoutOwnershipOnlyAsync(layout, actorId, cancellationToken);
        var zones = await _zones.GetActiveByNightMarketIdAsync(layout.NightMarketId, cancellationToken: cancellationToken);
        var blocks = await _layouts.GetBlocksByLayoutIdAsync(layout.Id, cancellationToken);
        var edges = await _layouts.GetEdgesByLayoutIdAsync(layout.Id, cancellationToken);
        zones = LayoutZoneSnapshot.Resolve(zones, blocks);
        var blockResponses = _mapper.Map<List<LayoutBlockResponse>>(blocks);
        foreach (var block in blockResponses)
        {
            var zone = zones.FirstOrDefault(item => item.Id == block.ZoneId);
            block.ZoneName = zone?.ZoneName ?? block.ZoneName ?? block.Name;
            block.ZoneCode = zone?.ZoneCode ?? block.ZoneCode;
            block.ZoneColor = zone?.Color ?? block.ZoneColor;
            block.Capacity = zone?.Capacity ?? block.Capacity;
            block.SlotCount = layout.LayoutNodes.Count(node =>
                !node.IsDeleted
                && node.LayoutBlockId == block.Id
                && node.NodeType == LayoutNodeType.BoothSlot);
        }

        return ApiResponse<MarketLayoutEditorDataResponse>.SuccessResponse(new MarketLayoutEditorDataResponse
        {
            Layout = _mapper.Map<MarketLayoutResponse>(layout),
            Zones = _mapper.Map<List<ZoneResponse>>(zones),
            Blocks = blockResponses,
            Nodes = _mapper.Map<List<LayoutNodeResponse>>(layout.LayoutNodes),
            Edges = _mapper.Map<List<LayoutEdgeResponse>>(edges),
            BoothLocations = _mapper.Map<List<BoothLocationResponse>>(layout.BoothLocations)
        });
    }

    public async Task<ApiResponse<GenerationPreviewResponse>> GenerationPreviewAsync(
        Guid layoutId, GenerateLayoutRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        // Generate/Apply are strictly MarketOwner-only; Admin cannot call these
        if (!actorId.HasValue)
            throw AppException.Forbidden("Only the market owner can generate layouts.", "OWNER_REQUIRED");

        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        await EnsureDraftLayoutEditableAsync(layout, cancellationToken);

        var market = await EnsureNightMarketExistsAsync(layout.NightMarketId, cancellationToken);
        if (!market.BoundaryWidthMeters.HasValue || !market.BoundaryHeightMeters.HasValue || market.BoundaryWidthMeters.Value <= 0 || market.BoundaryHeightMeters.Value <= 0)
            throw AppException.BadRequest("The market boundary dimensions have not been configured. Set the market width and length before continuing.", "MARKET_BOUNDARY_MISSING");

        request.MarketWidthMeters = layout.MarketWidthMeters ?? market.BoundaryWidthMeters.Value;
        request.MarketLengthMeters = layout.MarketLengthMeters ?? market.BoundaryHeightMeters.Value;
        request.ZoneConfigs ??= [];
        var physicalPreparation = PhysicalGridCalculator.Prepare(request);
        var hasZoneManagement = await ValidateGenerationEntitlementAsync(market, request);

        var zones = await _zones.GetActiveByNightMarketIdAsync(layout.NightMarketId, cancellationToken: cancellationToken);
        ValidateZoneConfigs(zones, request.ZoneConfigs);

        var existingNodes = await _layouts.GetNodesByLayoutIdAsync(layoutId, cancellationToken);
        var editorData = await _layouts.GetEditorLayoutAsync(layoutId, cancellationToken)
                         ?? throw AppException.NotFound("Market layout was not found.");

        var boothLocations = editorData.BoothLocations.ToList();
        var layoutBlocks = await _layouts.GetBlocksByLayoutIdAsync(layoutId, cancellationToken);
        zones = LayoutZoneSnapshot.Resolve(zones, layoutBlocks);
        var plan = BuildZoneMaterializationPlan(market, zones, existingNodes, boothLocations, request, hasZoneManagement);
        var generationResult = _generator.ComputeGeneration(
            layout, plan.EffectiveZones, existingNodes, boothLocations, plan.NormalizedRequest);
        var result = generationResult.Preview;
        result.Errors.AddRange(physicalPreparation.Errors);
        result.Warnings.AddRange(physicalPreparation.Warnings);
        if (physicalPreparation.Errors.Count > 0)
        {
            result.CanApply = false;
        }
        result.Nodes = _mapper.Map<List<LayoutNodeResponse>>(generationResult.Nodes) ?? [];
        result.Edges = _mapper.Map<List<LayoutEdgeResponse>>(generationResult.Edges) ?? [];
        result.Blocks = _mapper.Map<List<LayoutBlockResponse>>(generationResult.Blocks) ?? [];
        foreach (var block in result.Blocks)
        {
            var previewZone = (result.Zones ?? []).FirstOrDefault(zone =>
                (zone.ZoneId.HasValue && zone.ZoneId == block.ZoneId)
                || string.Equals(zone.ZoneName, block.Name, StringComparison.OrdinalIgnoreCase));
            block.ZoneName = previewZone?.ZoneName ?? block.ZoneName ?? block.Name;
            block.ZoneCode = previewZone?.ZoneCode ?? block.ZoneCode;
            block.ZoneColor = previewZone?.Color ?? block.ZoneColor;
            block.ZoneType = previewZone?.ZoneType ?? block.ZoneType;
            block.Capacity = previewZone?.Capacity ?? block.Capacity;
            block.SlotCount = previewZone?.Slots?.Count ?? result.Nodes.Count(node =>
                node.LayoutBlockId == block.Id
                && string.Equals(node.NodeType, LayoutNodeType.BoothSlot.ToString(), StringComparison.Ordinal));
        }

        // Zones not persisted yet are surfaced with a null ZoneId so the FE can render them as new
        foreach (var zoneResult in (result.Zones ?? []).Where(z =>
                     z.ZoneId.HasValue && plan.NewZoneIds.Contains(z.ZoneId.Value)))
            zoneResult.ZoneId = null;
        foreach (var node in (result.Nodes ?? []).Where(n =>
                     n.ZoneId.HasValue && plan.NewZoneIds.Contains(n.ZoneId.Value)))
            node.ZoneId = null;
        foreach (var block in (result.Blocks ?? []).Where(b =>
                     b.ZoneId.HasValue && plan.NewZoneIds.Contains(b.ZoneId.Value)))
            block.ZoneId = null;

        if (plan.CapacityErrors.Count > 0)
        {
            result.Errors.AddRange(plan.CapacityErrors);
            result.CanApply = false;
        }

        if (market.MarketOwnerId != null)
        {
            var slotCount = generationResult.Nodes.Count(n => n.NodeType == LayoutNodeType.BoothSlot);
            var quotaError = await _resourceQuota.GetBoothSlotCapacityErrorAsync(
                market.MarketOwnerId.Value, layout.MarketMapId, layout.Id,
                slotCount, cancellationToken);
            if (quotaError is not null)
            {
                result.Errors.Add(quotaError);
                result.CanApply = false;
            }
        }

        // Enrich HasAssignedBooth per slot using real BoothLocations
        var assignedNodeIds = boothLocations
            .Where(bl => !bl.IsDeleted && bl.ReleasedAt == null)
            .Select(bl => bl.LayoutNodeId).ToHashSet();
        var nodeById = existingNodes.Where(n => n.SlotCode != null)
            .ToDictionary(n => n.SlotCode!, n => n.Id);
        foreach (var zoneResult in result.Zones ?? [])
            foreach (var slot in zoneResult.Slots ?? [])
                if (nodeById.TryGetValue(slot.SlotCode, out var nid))
                    slot.HasAssignedBooth = assignedNodeIds.Contains(nid);

        return ApiResponse<GenerationPreviewResponse>.SuccessResponse(result,
            result.CanApply ? "Preview generated successfully." : "Preview contains conflicts.");
    }

    public async Task<ApiResponse<object>> ApplyGenerationAsync(
        Guid layoutId, GenerateLayoutRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        // Generate/Apply are strictly MarketOwner-only; Admin cannot call these
        if (!actorId.HasValue)
            throw AppException.Forbidden("Only the market owner can apply layout generation.", "OWNER_REQUIRED");

        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        await EnsureDraftLayoutEditableAsync(layout, cancellationToken);

        if (Math.Abs((layout.UpdatedAt - request.ExpectedUpdatedAt).TotalSeconds) > 1)
            throw AppException.Conflict(
                "This layout was changed in another session. Reload it before generating.",
                "LAYOUT_VERSION_CONFLICT");

        var market = await EnsureNightMarketExistsAsync(layout.NightMarketId, cancellationToken);
        if (!market.BoundaryWidthMeters.HasValue || !market.BoundaryHeightMeters.HasValue || market.BoundaryWidthMeters.Value <= 0 || market.BoundaryHeightMeters.Value <= 0)
            throw AppException.BadRequest("The market boundary dimensions have not been configured. Set the market width and length before continuing.", "MARKET_BOUNDARY_MISSING");

        request.MarketWidthMeters = layout.MarketWidthMeters ?? market.BoundaryWidthMeters.Value;
        request.MarketLengthMeters = layout.MarketLengthMeters ?? market.BoundaryHeightMeters.Value;
        request.ZoneConfigs ??= [];
        var physicalPreparation = PhysicalGridCalculator.Prepare(request);
        if (physicalPreparation.Errors.Count > 0)
            throw AppException.BadRequest(physicalPreparation.Errors[0], "PHYSICAL_LAYOUT_INVALID");
        var hasZoneManagement = await ValidateGenerationEntitlementAsync(market, request);

        var zones = await _zones.GetActiveByNightMarketIdAsync(layout.NightMarketId, cancellationToken: cancellationToken);
        ValidateZoneConfigs(zones, request.ZoneConfigs);

        var existingNodes = await _layouts.GetNodesByLayoutIdAsync(layoutId, cancellationToken);
        var editorData = await _layouts.GetEditorLayoutAsync(layoutId, cancellationToken)
                         ?? throw AppException.NotFound("Market layout was not found.");

        var boothLocations = editorData.BoothLocations.ToList();
        var layoutBlocks = await _layouts.GetBlocksByLayoutIdAsync(layoutId, cancellationToken);
        zones = LayoutZoneSnapshot.Resolve(zones, layoutBlocks);
        var plan = BuildZoneMaterializationPlan(market, zones, existingNodes, boothLocations, request, hasZoneManagement);
        if (plan.CapacityErrors.Count > 0)
            throw AppException.Conflict(plan.CapacityErrors[0], "CAPACITY_BELOW_ASSIGNED");

        // Conflict check is now fully handled by _generator.ComputeGeneration (which calls ComputePreview)
        // It accurately detects both reduced capacities and omitted zones that drop assigned booths.
        var result = _generator.ComputeGeneration(
            layout, plan.EffectiveZones, existingNodes, boothLocations, plan.NormalizedRequest);
        var generatedBoothSlotCount = result.Nodes.Count(node =>
            !node.IsDeleted && node.NodeType == LayoutNodeType.BoothSlot);
        if (request.RequestedBoothCount is not int requestedBoothCount
            || generatedBoothSlotCount != requestedBoothCount)
        {
            throw AppException.Conflict(
                $"Generation requested exactly {request.RequestedBoothCount?.ToString() ?? "an unspecified number of"} booth slots but produced {generatedBoothSlotCount}.",
                "LAYOUT_GENERATION_SLOT_COUNT_MISMATCH");
        }
        if (!result.Preview.CanApply || physicalPreparation.Errors.Count > 0)
        {
            var msg = result.Preview.Errors.FirstOrDefault()
                      ?? physicalPreparation.Errors.FirstOrDefault()
                      ?? "Cannot apply generation due to conflicts. Check preview first.";
            if (result.Preview.ConflictingSlots.Any())
            {
                var codes = string.Join(", ", result.Preview.ConflictingSlots.Select(c => c.SlotCode));
                msg = $"Cannot reduce capacity or omit zones: slots {codes} have active booth assignments. Release them first.";
            }
            throw AppException.Conflict(msg, "LAYOUT_GENERATION_CONFLICT");
        }

        if (market.MarketOwnerId != null)
        {
            var slotCount = result.Nodes.Count(n => n.NodeType == LayoutNodeType.BoothSlot);
            await _resourceQuota.EnsureBoothSlotCapacityAsync(
                market.MarketOwnerId.Value, layout.MarketMapId, layout.Id,
                slotCount, cancellationToken);
        }

        // Physical generation fixes the canvas to the declared market dimensions.
        // Legacy generation keeps the previous grow-only behavior.
        if (request.MarketWidthMeters.HasValue
            || result.NewCanvasWidth > layout.Width || result.NewCanvasHeight > layout.Height)
        {
            layout.Width = result.NewCanvasWidth;
            layout.Height = result.NewCanvasHeight;
            layout.MarketWidthMeters = request.MarketWidthMeters;
            layout.MarketLengthMeters = request.MarketLengthMeters;
            layout.PixelsPerMeter = request.MarketWidthMeters.HasValue
                ? request.PixelsPerMeter
                : layout.PixelsPerMeter;
            layout.UpdatedAt = DateTime.UtcNow;
            _layouts.Update(layout);
        }

        // Merge existing utility edges (Entrance→Junction, Exit→Junction, etc.)
        // so manual routes placed by the market owner are preserved after regeneration.
        var existingEdges = await _layouts.GetEdgesByLayoutIdAsync(layoutId, cancellationToken);
        var generatedSlotNodeIds = result.Nodes
            .Where(n => n.NodeType == LayoutNodeType.BoothSlot)
            .Select(n => n.Id).ToHashSet();
        var generatedRoutingNodeIds = result.Nodes
            .Where(n => n.NodeType == LayoutNodeType.Junction)
            .Select(n => n.Id).ToHashSet();
        var finalNodeIds = result.Nodes.Select(n => n.Id).ToHashSet();
        var utilityEdges = existingEdges
            .Where(e => !e.IsDeleted
                && finalNodeIds.Contains(e.FromNodeId)
                && finalNodeIds.Contains(e.ToNodeId)
                && !generatedSlotNodeIds.Contains(e.FromNodeId)
                && !generatedSlotNodeIds.Contains(e.ToNodeId)
                // Junctions and slot-access edges are generated routing data.
                // Never carry them across a regeneration: an old edge may
                // point to an aisle from a previous zone arrangement and cut
                // straight through a newly placed Zone block.  The generator
                // has already rebuilt all current aisle/corridor edges above.
                && !generatedRoutingNodeIds.Contains(e.FromNodeId)
                && !generatedRoutingNodeIds.Contains(e.ToNodeId)
                // Do not carry a legacy utility edge into the new graph when
                // its straight segment crosses a regenerated Zone block.
                // Keeping these stale edges was the main reason the same
                // crossing errors returned after every Preview/Apply cycle.
                && !EdgeCrossesGeneratedBlock(e, result.Nodes, result.Blocks))
            .ToList();
        // Deduplicate edges to prevent duplicate paths (e.g., auto-generated Entrance->Junction vs preserved utility edge)
        var mergedEdges = result.Edges.Concat(utilityEdges)
            .GroupBy(e => e.FromNodeId.CompareTo(e.ToNodeId) < 0
                ? $"{e.FromNodeId}_{e.ToNodeId}"
                : $"{e.ToNodeId}_{e.FromNodeId}")
            .Select(g => g.First())
            .Where(e => finalNodeIds.Contains(e.FromNodeId) && finalNodeIds.Contains(e.ToNodeId))
            .ToList();

        // Generation must never persist a graph in which a booth slot is disconnected
        // from every entrance. This final invariant also repairs incomplete legacy utility
        // paths while preserving the owner's existing nodes and routes.
        EnsureGeneratedGraphConnectivity(layoutId, result.Nodes, mergedEdges);

        await _layouts.SaveGraphTransactionalAsync(
            layoutId,
            result.Blocks,
            result.Nodes,
            mergedEdges,
            plan.ZoneUpserts,
            cancellationToken);

        return ApiResponse<object>.SuccessResponse(new
        {
            LayoutId = layoutId,
            BlockCount = result.Blocks.Count,
            NodeCount = result.Nodes.Count,
            EdgeCount = mergedEdges.Count,
            UpdatedAt = layout.UpdatedAt
        }, "Layout generated successfully.");
    }

    private static void EnsureGeneratedGraphConnectivity(
        Guid layoutId,
        IReadOnlyList<LayoutNode> nodes,
        List<LayoutEdge> edges)
    {
        // Generation must not fail because a persisted walkway is incomplete.
        // A gate is the only required map anchor.  Customer navigation builds
        // a transient obstacle-aware corridor graph from the saved geometry,
        // so missing/isolated junctions are not a reason to reject the layout.
        var junctions = nodes
            .Where(node => node.NodeType == LayoutNodeType.Junction
                && !IsGeneratedCorridorNode(node))
            .ToList();
        var slots = nodes.Where(node => node.NodeType == LayoutNodeType.BoothSlot).ToList();

        var nodeIds = nodes.Select(node => node.Id).ToHashSet();
        edges.RemoveAll(edge => edge.IsDeleted
                                || edge.FromNodeId == edge.ToNodeId
                                || !nodeIds.Contains(edge.FromNodeId)
                                || !nodeIds.Contains(edge.ToNodeId));

        var pairs = edges
            .Select(edge => CanonicalEdgePair(edge.FromNodeId, edge.ToNodeId))
            .ToHashSet();
        var junctionIds = junctions.Select(junction => junction.Id).ToHashSet();

        // Add the safe local slot-to-zone-junction links when a matching
        // junction exists.  This is only an enrichment step; it never throws
        // when a hand-edited layout has no junction or an isolated zone.
        foreach (var slot in slots)
        {
            var hasAccessibleJunctionEdge = edges.Any(edge => edge.IsAccessible
                && ((edge.FromNodeId == slot.Id && junctionIds.Contains(edge.ToNodeId))
                    || (edge.ToNodeId == slot.Id && junctionIds.Contains(edge.FromNodeId))));
            if (hasAccessibleJunctionEdge)
                continue;

            var junction = junctions
                .Where(candidate =>
                    (slot.LayoutBlockId.HasValue && candidate.LayoutBlockId == slot.LayoutBlockId)
                    || (slot.ZoneId.HasValue && candidate.ZoneId == slot.ZoneId))
                .OrderBy(candidate => SquaredDistance(slot, candidate))
                .FirstOrDefault();

            if (junction is not null)
                AddConnectivityEdge(layoutId, slot, junction, edges, pairs);
        }
    }

    private static void AddConnectivityEdge(
        Guid layoutId,
        LayoutNode from,
        LayoutNode to,
        List<LayoutEdge> edges,
        ISet<(Guid First, Guid Second)> pairs)
    {
        var pair = CanonicalEdgePair(from.Id, to.Id);
        if (!pairs.Add(pair))
        {
            var existing = edges.First(edge =>
                CanonicalEdgePair(edge.FromNodeId, edge.ToNodeId) == pair);
            existing.IsAccessible = true;
            existing.IsBidirectional = true;
            existing.IsDeleted = false;
            existing.UpdatedAt = DateTime.UtcNow;
            return;
        }

        var deltaX = from.Xcoordinate - to.Xcoordinate;
        var deltaY = from.Ycoordinate - to.Ycoordinate;
        var distance = (decimal)Math.Sqrt((double)(deltaX * deltaX + deltaY * deltaY));
        var now = DateTime.UtcNow;
        edges.Add(new LayoutEdge
        {
            Id = Guid.NewGuid(),
            LayoutId = layoutId,
            FromNodeId = from.Id,
            ToNodeId = to.Id,
            Distance = Math.Max(1m, distance),
            IsBidirectional = true,
            IsAccessible = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static HashSet<Guid> ReachableFromEntrances(
        IEnumerable<Guid> entrances,
        IEnumerable<LayoutEdge> edges)
    {
        var graph = new Dictionary<Guid, List<Guid>>();
        foreach (var edge in edges.Where(edge => edge.IsAccessible && !edge.IsDeleted))
        {
            graph.TryAdd(edge.FromNodeId, []);
            graph[edge.FromNodeId].Add(edge.ToNodeId);
            if (!edge.IsBidirectional)
                continue;

            graph.TryAdd(edge.ToNodeId, []);
            graph[edge.ToNodeId].Add(edge.FromNodeId);
        }

        var visited = entrances.ToHashSet();
        var queue = new Queue<Guid>(visited);
        while (queue.TryDequeue(out var current))
            if (graph.TryGetValue(current, out var adjacent))
                foreach (var next in adjacent)
                    if (visited.Add(next))
                        queue.Enqueue(next);

        return visited;
    }

    private static (Guid First, Guid Second) CanonicalEdgePair(Guid first, Guid second)
        => first.CompareTo(second) < 0 ? (first, second) : (second, first);

    private static decimal SquaredDistance(LayoutNode first, LayoutNode second)
    {
        var deltaX = first.Xcoordinate - second.Xcoordinate;
        var deltaY = first.Ycoordinate - second.Ycoordinate;
        return deltaX * deltaX + deltaY * deltaY;
    }

    private static bool IsGeneratedCorridorNode(LayoutNode node)
        => node.NodeType == LayoutNodeType.Junction
           && (node.NodeName?.StartsWith("Auto Corridor ", StringComparison.OrdinalIgnoreCase) == true
               || (!node.ZoneId.HasValue && !node.LayoutBlockId.HasValue));

    private static bool EdgeCrossesGeneratedBlock(
        LayoutEdge edge,
        IReadOnlyCollection<LayoutNode> nodes,
        IReadOnlyCollection<LayoutBlock> blocks)
    {
        var from = nodes.FirstOrDefault(node => node.Id == edge.FromNodeId);
        var to = nodes.FirstOrDefault(node => node.Id == edge.ToNodeId);
        if (from is null || to is null)
            return true;

        var slotToOwnJunction =
            (from.NodeType == LayoutNodeType.BoothSlot && to.NodeType == LayoutNodeType.Junction)
            || (to.NodeType == LayoutNodeType.BoothSlot && from.NodeType == LayoutNodeType.Junction);
        foreach (var block in blocks.Where(block => !block.IsDeleted))
        {
            if (slotToOwnJunction &&
                ((from.LayoutBlockId.HasValue && from.LayoutBlockId == block.Id)
                 || (block.ZoneId.HasValue && from.ZoneId == block.ZoneId)) &&
                ((to.LayoutBlockId.HasValue && to.LayoutBlockId == block.Id)
                 || (block.ZoneId.HasValue && to.ZoneId == block.ZoneId)))
                continue;

            if (LayoutBlockGeometry.SegmentIntersects(
                    (double)from.Xcoordinate, (double)from.Ycoordinate,
                    (double)to.Xcoordinate, (double)to.Ycoordinate,
                    new LayoutRect(block.X, block.Y, block.Width, block.Height)))
                return true;
        }
        return false;
    }

    public async Task<ApiResponse<MarketLayoutValidationResponse>> ValidateAsync(
        Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipOnlyAsync(layout, actorId, cancellationToken);
        var graphResult = await _graphValidation.ValidateAsync(layoutId, cancellationToken);
        var planLimitError = await GetPlanSlotLimitErrorAsync(layout, actorId, cancellationToken);
        // The draft may intentionally replace sections of the currently active
        // MarketMap. Only the draft composition, not its predecessor, determines
        // whether physical areas overlap at publication time.
        var sectionErrors = await GetSectionGeometryErrorsAsync(layout, false, cancellationToken);
        var market = await EnsureNightMarketExistsAsync(layout.NightMarketId, cancellationToken);
        var calibrationWarnings = LayoutPhysicalCalibration.DetectConflicts(
                layout, market.BoundaryWidthMeters, market.BoundaryHeightMeters)
            .Select(conflict => conflict.Message + " Explicit/precedence-resolved physical dimensions remain authoritative.");
        var errors = graphResult.Errors.Concat(sectionErrors);
        if (planLimitError is not null) errors = errors.Append(planLimitError);
        var result = new MarketLayoutValidationResponse
        {
            Errors = errors.Distinct().ToArray(),
            Warnings = graphResult.Warnings.Concat(calibrationWarnings).Distinct().ToArray()
        };
        return ApiResponse<MarketLayoutValidationResponse>.SuccessResponse(result,
            result.IsValid ? "Market layout is valid." : "Market layout validation failed.");
    }

    public async Task<ApiResponse<MarketLayoutMetricsResponse>> GetMetricsAsync(
        Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipOnlyAsync(layout, actorId, cancellationToken);
        return ApiResponse<MarketLayoutMetricsResponse>.SuccessResponse(
            await BuildMetricsAsync(layout, cancellationToken));
    }

    public async Task<ApiResponse<MarketLayoutComparisonResponse>> CompareAsync(
        Guid leftLayoutId, Guid rightLayoutId,
        CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        if (leftLayoutId == rightLayoutId)
            throw AppException.BadRequest("Select two different map layouts to compare.", "LAYOUT_COMPARE_SAME");

        var left = await GetActiveLayoutAsync(leftLayoutId, cancellationToken);
        var right = await GetActiveLayoutAsync(rightLayoutId, cancellationToken);
        await EnsureLayoutOwnershipOnlyAsync(left, actorId, cancellationToken);
        await EnsureLayoutOwnershipOnlyAsync(right, actorId, cancellationToken);
        if (left.NightMarketId != right.NightMarketId)
            throw AppException.BadRequest("Only map layouts from the same night market can be compared.", "LAYOUT_COMPARE_MARKET_MISMATCH");

        var leftMetrics = await BuildMetricsAsync(left, cancellationToken);
        var rightMetrics = await BuildMetricsAsync(right, cancellationToken);
        return ApiResponse<MarketLayoutComparisonResponse>.SuccessResponse(new()
        {
            Left = _mapper.Map<MarketLayoutResponse>(left),
            LeftMetrics = leftMetrics,
            Right = _mapper.Map<MarketLayoutResponse>(right),
            RightMetrics = rightMetrics,
            ZoneDifference = rightMetrics.ZoneCount - leftMetrics.ZoneCount,
            SlotDifference = rightMetrics.TotalSlots - leftMetrics.TotalSlots,
            AssignedSlotDifference = rightMetrics.AssignedSlots - leftMetrics.AssignedSlots,
            ZoneAreaDifferenceSquareMeters = rightMetrics.ZoneAreaSquareMeters - leftMetrics.ZoneAreaSquareMeters,
            BoothAreaDifferenceSquareMeters = rightMetrics.BoothAreaSquareMeters - leftMetrics.BoothAreaSquareMeters,
            WalkwayAreaDifferenceSquareMeters = rightMetrics.WalkwayOpenAreaSquareMeters - leftMetrics.WalkwayOpenAreaSquareMeters,
            OccupancyDifferencePercent = rightMetrics.OccupancyPercent - leftMetrics.OccupancyPercent,
            NavigationReadinessDifferencePercent = rightMetrics.NavigationReadinessPercent - leftMetrics.NavigationReadinessPercent
        });
    }
    public async Task<ApiResponse<MarketLayoutResponse>> ActivateAsync(
        Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipOnlyAsync(layout, actorId, cancellationToken);
        throw AppException.Conflict(
            "Individual layout activation is disabled. Publish the complete composition through MarketMap activation.",
            "LAYOUT_ACTIVATION_MANAGED_BY_MARKET_MAP");
    }

    public async Task<ApiResponse<MarketLayoutResponse>> SetDefaultViewAsync(
        Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipOnlyAsync(layout, actorId, cancellationToken);
        throw AppException.Conflict(
            "Individual layout lifecycle changes are disabled. Publish the complete composition through MarketMap activation.",
            "LAYOUT_LIFECYCLE_MANAGED_BY_MARKET_MAP");
    }

    private async Task<string?> GetPlanSlotLimitErrorAsync(
        MarketLayout layout,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        if (!actorId.HasValue)
            return null;

        var nodes = await _layouts.GetNodesByLayoutIdAsync(layout.Id, cancellationToken);
        var slotCount = nodes.Count(node => !node.IsDeleted && node.NodeType == LayoutNodeType.BoothSlot);
        return await _resourceQuota.GetBoothSlotCapacityErrorAsync(
            actorId.Value, layout.MarketMapId, layout.Id, slotCount, cancellationToken);
    }

    public async Task<ApiResponse<MarketLayoutResponse>> DeactivateAsync(
        Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipOnlyAsync(layout, actorId, cancellationToken);
        throw AppException.Conflict(
            "Individual layout lifecycle changes are disabled. Publish another complete composition through MarketMap activation.",
            "LAYOUT_LIFECYCLE_MANAGED_BY_MARKET_MAP");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        await EnsureDraftLayoutEditableAsync(layout, cancellationToken);

        var editor = await _layouts.GetEditorLayoutAsync(layoutId, cancellationToken)
            ?? throw AppException.NotFound("Market layout was not found.");
        if (editor.BoothLocations.Any(location => !location.IsDeleted && !location.ReleasedAt.HasValue))
            throw AppException.Conflict(
                "Release or move all assigned booths before archiving this layout.",
                "LAYOUT_HAS_ASSIGNED_BOOTHS");

        layout.Status = MarketLayoutStatus.Archived;
        layout.UpdatedAt = DateTime.UtcNow;
        _layouts.Delete(layout);

        await _layouts.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { layout.Id }, "Market layout archived successfully.");
    }

    private async Task<MarketLayout> GetActiveLayoutAsync(Guid id, CancellationToken cancellationToken)
        => await _layouts.GetActiveByIdAsync(id, cancellationToken)
           ?? throw AppException.NotFound("Market layout was not found.");

    private async Task EnsureDraftLayoutEditableAsync(MarketLayout layout, CancellationToken cancellationToken)
    {
        if (!await _layouts.IsEditableDraftAsync(layout.Id, cancellationToken))
            throw AppException.Conflict(
                "Only a layout in a draft MarketMap can be edited.",
                "LAYOUT_NOT_EDITABLE_DRAFT");
    }

    private async Task<NightMarket> EnsureNightMarketExistsAsync(Guid id, CancellationToken cancellationToken)
        => await _nightMarkets.GetActiveByIdAsync(id, cancellationToken)
           ?? throw AppException.NotFound("Night market was not found.");

    private static void EnsureOwnership(NightMarket market, Guid? actorId)
    {
        if (actorId.HasValue && market.MarketOwnerId != actorId)
            throw AppException.Forbidden("You do not have permission to manage this night market's layouts.");
    }

    private async Task EnsureLayoutOwnershipOnlyAsync(MarketLayout layout, Guid? actorId, CancellationToken cancellationToken)
    {
        if (!actorId.HasValue) return;
        var market = await _nightMarkets.GetActiveByIdAsync(layout.NightMarketId, cancellationToken);
        if (market is null || market.MarketOwnerId != actorId)
            throw AppException.Forbidden("You do not have permission to view this night market's layouts.");
    }

    private async Task EnsureLayoutOwnershipAsync(MarketLayout layout, Guid? actorId, CancellationToken cancellationToken)
    {
        if (!actorId.HasValue) return;   // Admin path — caller must block if needed
        var market = await _nightMarkets.GetActiveByIdAsync(layout.NightMarketId, cancellationToken);
        if (market is null || market.MarketOwnerId != actorId)
            throw AppException.Forbidden("You do not have permission to manage this night market's layouts.");

        var hasSubscription = await _entitlements.HasActiveMarketSubscriptionAsync(actorId.Value);
        if (!hasSubscription)
            throw AppException.Forbidden("An active Market subscription is required to perform this action.", "MARKET_SUBSCRIPTION_REQUIRED");
    }

    private async Task<MarketLayoutMetricsResponse> BuildMetricsAsync(
        MarketLayout layout, CancellationToken cancellationToken)
    {
        var editor = await _layouts.GetEditorLayoutAsync(layout.Id, cancellationToken)
            ?? throw AppException.NotFound("Market layout was not found.");
        var market = await EnsureNightMarketExistsAsync(layout.NightMarketId, cancellationToken);
        var blocks = await _layouts.GetBlocksByLayoutIdAsync(layout.Id, cancellationToken);
        var edges = await _layouts.GetEdgesByLayoutIdAsync(layout.Id, cancellationToken);
        var zones = await _zones.GetActiveByNightMarketIdAsync(
            layout.NightMarketId, cancellationToken: cancellationToken);
        return LayoutMetricsCalculator.Calculate(
            editor, market.BoundaryWidthMeters, market.BoundaryHeightMeters, blocks, editor.LayoutNodes.ToList(), edges,
            editor.BoothLocations.ToList(), zones);
    }

    private async Task<IReadOnlyCollection<string>> GetSectionGeometryErrorsAsync(
        MarketLayout layout, bool includePublishedOverlap, CancellationToken cancellationToken)
    {
        var market = await EnsureNightMarketExistsAsync(layout.NightMarketId, cancellationToken);
        var errors = ValidateSectionGeometry(layout, market);
        if (!includePublishedOverlap || errors.Count > 0) return errors;

        var width = layout.MarketWidthMeters ?? market.BoundaryWidthMeters ?? 0;
        var length = layout.MarketLengthMeters ?? market.BoundaryHeightMeters ?? 0;
        foreach (var other in await _layouts.GetPublishedMapsAsync(layout.NightMarketId, cancellationToken))
        {
            if (other.Id == layout.Id ||
                other.SectionCode.Equals(layout.SectionCode, StringComparison.OrdinalIgnoreCase))
                continue;

            var otherWidth = other.MarketWidthMeters ?? market.BoundaryWidthMeters ?? 0;
            var otherLength = other.MarketLengthMeters ?? market.BoundaryHeightMeters ?? 0;
            if (RectanglesOverlap(
                    layout.OffsetXMeters, layout.OffsetYMeters, width, length,
                    other.OffsetXMeters, other.OffsetYMeters, otherWidth, otherLength))
            {
                errors.Add(
                    $"Map section '{layout.SectionName}' overlaps published section '{other.SectionName}'. Adjust its physical position or dimensions.");
            }
        }
        return errors;
    }

    private static void ValidateSectionInsideMarket(MarketLayout layout, NightMarket market)
    {
        var errors = ValidateSectionGeometry(layout, market);
        if (errors.Count > 0)
            throw AppException.BadRequest(string.Join(" ", errors), "MAP_SECTION_OUTSIDE_MARKET");
    }

    private static List<string> ValidateSectionGeometry(MarketLayout layout, NightMarket market)
    {
        var errors = new List<string>();
        var marketWidth = market.BoundaryWidthMeters ?? 0;
        var marketLength = market.BoundaryHeightMeters ?? 0;
        var width = layout.MarketWidthMeters ?? marketWidth;
        var length = layout.MarketLengthMeters ?? marketLength;

        if (marketWidth <= 0 || marketLength <= 0)
            errors.Add("Set the night market boundary width and length before configuring map sections.");
        if (width <= 0 || length <= 0)
            errors.Add("Map section width and length must be greater than zero.");
        if (GeometryTolerance.IsNegative(layout.OffsetXMeters)
            || GeometryTolerance.IsNegative(layout.OffsetYMeters))
            errors.Add("Map section offsets cannot be negative.");
        if (marketWidth > 0 && GeometryTolerance.Exceeds(layout.OffsetXMeters + width, marketWidth))
            errors.Add($"Map section '{layout.SectionName}' exceeds the market width boundary.");
        if (marketLength > 0 && GeometryTolerance.Exceeds(layout.OffsetYMeters + length, marketLength))
            errors.Add($"Map section '{layout.SectionName}' exceeds the market length boundary.");
        return errors;
    }

    private static bool RectanglesOverlap(
        double leftX, double leftY, double leftWidth, double leftHeight,
        double rightX, double rightY, double rightWidth, double rightHeight)
        => GeometryTolerance.RectanglesHaveInteriorOverlap(
            leftX, leftY, leftWidth, leftHeight,
            rightX, rightY, rightWidth, rightHeight);

    private static string NormalizeSectionCode(string? code, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(code) ? fallback : code;
        var normalized = new string(source.Trim().ToUpperInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .Take(50)
            .ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "MAIN" : normalized;
    }
    private async Task ValidateIdentityAsync(
        Guid nightMarketId, string sectionCode, string name, int version, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw AppException.BadRequest("Layout name is required.");

        if (version <= 0)
            throw AppException.BadRequest("Layout version must be greater than zero.");

        if (await _layouts.ActiveNameOrVersionExistsAsync(nightMarketId, sectionCode, name, version, excludeId, cancellationToken))
            throw AppException.Conflict("Layout name already exists, or this section version already exists in the night market.");
    }

    private static MarketLayoutValidationResponse BuildValidation(MarketLayout layout)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(layout.LayoutImageUrl))
            errors.Add("Layout image is required.");

        if (layout.Width <= 0 || layout.Height <= 0)
            errors.Add("Layout width and height must be greater than zero.");

        if (layout.LayoutNodes.Count == 0)
            errors.Add("Layout must have at least one node.");

        if (layout.LayoutNodes.Any(node =>
                node.Xcoordinate < 0 || node.Xcoordinate > layout.Width ||
                node.Ycoordinate < 0 || node.Ycoordinate > layout.Height))
            errors.Add("All nodes must be inside the layout dimensions.");

        return new MarketLayoutValidationResponse { Errors = errors, Warnings = warnings };
    }
    private async Task<bool> ValidateGenerationEntitlementAsync(NightMarket market, GenerateLayoutRequest request)
    {
        var hasZoneManagement = market.MarketOwnerId == null
            || await _entitlements.CanUseZoneManagementAsync(market.MarketOwnerId.Value);

        if (hasZoneManagement)
        {
            if (request.ZoneConfigs is null || request.ZoneConfigs.Count == 0)
                throw AppException.BadRequest(
                    "At least one zone configuration is required to generate a layout.",
                    "ZONE_CONFIGS_REQUIRED");
            return true;
        }

        if (request.ZoneConfigs is { Count: > 0 })
            throw AppException.Forbidden(
                "Your current subscription does not include zone management.",
                "ZONE_MANAGEMENT_NOT_INCLUDED");

        if (request.DefaultZoneCapacity is not > 0)
            throw AppException.BadRequest(
                "Default zone capacity is required to generate a layout on your current package.",
                "DEFAULT_ZONE_CAPACITY_REQUIRED");
        return false;
    }

    private void ValidateZoneConfigs(IReadOnlyCollection<Zone> zones, IReadOnlyCollection<ZoneGenerationConfig>? configs)
    {
        configs ??= [];
        var zoneIds = zones.Select(z => z.Id).ToHashSet();
        var referencedZoneIds = configs
            .Where(c => c.ZoneId.HasValue && c.ZoneId.Value != Guid.Empty)
            .Select(c => c.ZoneId!.Value)
            .ToList();

        var invalidZones = referencedZoneIds.Except(zoneIds).ToList();
        if (invalidZones.Any())
            throw AppException.BadRequest($"Request contains invalid Zone IDs: {string.Join(", ", invalidZones)}", "INVALID_ZONE_IDS");

        if (referencedZoneIds.Count != referencedZoneIds.Distinct().Count())
            throw AppException.BadRequest("Request contains duplicate Zone IDs.", "DUPLICATE_ZONE_IDS");

        foreach (var config in configs.Where(c => !c.ZoneId.HasValue || c.ZoneId.Value == Guid.Empty))
        {
            if (string.IsNullOrWhiteSpace(config.ZoneName))
                throw AppException.BadRequest("Zone name is required for each new zone.", "ZONE_NAME_REQUIRED");

            if (config.Capacity is null or < 0)
                throw AppException.BadRequest(
                    $"A non-negative allocated capacity is required for new zone '{config.ZoneName.Trim()}'.",
                    "ZONE_CAPACITY_REQUIRED");
        }
    }

    private ZoneMaterializationPlan BuildZoneMaterializationPlan(
        NightMarket market,
        IReadOnlyCollection<Zone> zones,
        IReadOnlyCollection<LayoutNode> existingNodes,
        IReadOnlyCollection<BoothLocation> boothLocations,
        GenerateLayoutRequest request,
        bool hasZoneManagement)
    {
        var now = DateTime.UtcNow;
        var assignedNodeIds = boothLocations
            .Where(bl => !bl.IsDeleted && bl.ReleasedAt == null)
            .Select(bl => bl.LayoutNodeId).ToHashSet();
        var assignedSlots = existingNodes
            .Where(n => n.NodeType == LayoutNodeType.BoothSlot && assignedNodeIds.Contains(n.Id))
            .ToList();
        var assignedByZone = assignedSlots
            .Where(n => n.ZoneId.HasValue)
            .GroupBy(n => n.ZoneId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var assignedWithoutZone = assignedSlots.Count(n => !n.ZoneId.HasValue);

        var effectiveZones = zones.Select(LayoutZoneSnapshot.Copy).ToList();
        var zoneUpserts = new List<Zone>();
        var newZoneIds = new HashSet<Guid>();
        var capacityErrors = new List<string>();

        int AssignedCountFor(Zone zone)
        {
            var count = assignedByZone.TryGetValue(zone.Id, out var assigned) ? assigned : 0;
            if (IsGeneralAreaZone(zone))
                count += assignedWithoutZone;
            return count;
        }

        void CheckCapacity(Zone zone)
        {
            var assignedCount = AssignedCountFor(zone);
            if (zone.Capacity < assignedCount)
                capacityErrors.Add(
                    $"Zone '{zone.ZoneName}' capacity ({zone.Capacity}) is below the {assignedCount} booth slot(s) currently assigned in that zone. Release booths first.");
        }

        if (!hasZoneManagement)
        {
            var general = FindGeneralAreaZone(effectiveZones);
            if (general == null)
            {
                general = new Zone
                {
                    Id = Guid.NewGuid(),
                    NightMarketId = market.Id,
                    ZoneName = GeneralAreaZoneName,
                    ZoneCode = GeneralAreaZoneCode,
                    Capacity = request.DefaultZoneCapacity!.Value,
                    WidthMeters = request.DefaultZoneWidthMeters,
                    LengthMeters = request.DefaultZoneLengthMeters,
                    BoothWidthMeters = request.DefaultBoothWidthMeters,
                    BoothLengthMeters = request.DefaultBoothLengthMeters,
                    HorizontalGapMeters = request.DefaultHorizontalGapMeters,
                    VerticalGapMeters = request.DefaultVerticalGapMeters,
                    Status = ZoneStatus.Active,
                    IsDeleted = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                effectiveZones.Add(general);
                newZoneIds.Add(general.Id);
            }
            else
            {
                general.Capacity = request.DefaultZoneCapacity!.Value;
                general.WidthMeters = request.DefaultZoneWidthMeters;
                general.LengthMeters = request.DefaultZoneLengthMeters;
                general.BoothWidthMeters = request.DefaultBoothWidthMeters;
                general.BoothLengthMeters = request.DefaultBoothLengthMeters;
                general.HorizontalGapMeters = request.DefaultHorizontalGapMeters;
                general.VerticalGapMeters = request.DefaultVerticalGapMeters;
                general.UpdatedAt = now;
            }
            zoneUpserts.Add(general);
            CheckCapacity(general);

            var defaultConfig = new ZoneGenerationConfig { ZoneId = general.Id };
            if (request.DefaultBoothWidth.HasValue) defaultConfig.BoothWidth = request.DefaultBoothWidth.Value;
            if (request.DefaultBoothHeight.HasValue) defaultConfig.BoothHeight = request.DefaultBoothHeight.Value;
            if (request.DefaultGap.HasValue) defaultConfig.Gap = request.DefaultGap.Value;
            if (request.DefaultAisleWidth.HasValue) defaultConfig.AisleWidth = request.DefaultAisleWidth.Value;
            if (request.DefaultColumns.HasValue) defaultConfig.Columns = request.DefaultColumns;
            if (request.DefaultAisleEveryNRows.HasValue) defaultConfig.AisleEveryNRows = request.DefaultAisleEveryNRows.Value;
            defaultConfig.ZoneWidthMeters = request.DefaultZoneWidthMeters;
            defaultConfig.ZoneLengthMeters = request.DefaultZoneLengthMeters;
            defaultConfig.BoothWidthMeters = request.DefaultBoothWidthMeters;
            defaultConfig.BoothLengthMeters = request.DefaultBoothLengthMeters;
            defaultConfig.HorizontalGapMeters = request.DefaultHorizontalGapMeters;
            defaultConfig.VerticalGapMeters = request.DefaultVerticalGapMeters;

            return new ZoneMaterializationPlan(
                effectiveZones,
                CloneRequestWithConfigs(request, new List<ZoneGenerationConfig> { defaultConfig }),
                zoneUpserts, newZoneIds, capacityErrors);
        }

        var usedZoneCodes = effectiveZones
            .Where(z => !string.IsNullOrWhiteSpace(z.ZoneCode))
            .Select(z => z.ZoneCode!.Trim().ToUpperInvariant())
            .ToHashSet();
        var normalizedConfigs = new List<ZoneGenerationConfig>();
        foreach (var config in request.ZoneConfigs ?? [])
        {
            Zone zone;
            if (config.ZoneId.HasValue && config.ZoneId.Value != Guid.Empty)
            {
                zone = effectiveZones.First(z => z.Id == config.ZoneId.Value);
                var hasUpdates = false;
                if (!string.IsNullOrWhiteSpace(config.ZoneName))
                {
                    zone.ZoneName = config.ZoneName.Trim();
                    hasUpdates = true;
                }
                if (!string.IsNullOrWhiteSpace(config.ZoneType))
                {
                    zone.Description = config.ZoneType.Trim();
                    hasUpdates = true;
                }
                if (config.Capacity.HasValue)
                {
                    zone.Capacity = config.Capacity.Value;
                    hasUpdates = true;
                }
                if (config.ZoneWidthMeters.HasValue)
                {
                    zone.WidthMeters = config.ZoneWidthMeters;
                    zone.LengthMeters = config.ZoneLengthMeters;
                    zone.BoothWidthMeters = config.BoothWidthMeters;
                    zone.BoothLengthMeters = config.BoothLengthMeters;
                    zone.HorizontalGapMeters = config.HorizontalGapMeters;
                    zone.VerticalGapMeters = config.VerticalGapMeters;
                    hasUpdates = true;
                }
                if (hasUpdates)
                {
                    zone.UpdatedAt = now;
                    zoneUpserts.Add(zone);
                }
            }
            else
            {
                zone = new Zone
                {
                    Id = Guid.NewGuid(),
                    NightMarketId = market.Id,
                    ZoneName = config.ZoneName!.Trim(),
                    Description = string.IsNullOrWhiteSpace(config.ZoneType) ? null : config.ZoneType.Trim(),
                    Color = GetZoneColorPreset(config.ZoneType),
                    ZoneCode = NextZoneCode(usedZoneCodes),
                    Capacity = config.Capacity!.Value,
                    WidthMeters = config.ZoneWidthMeters,
                    LengthMeters = config.ZoneLengthMeters,
                    BoothWidthMeters = config.BoothWidthMeters,
                    BoothLengthMeters = config.BoothLengthMeters,
                    HorizontalGapMeters = config.HorizontalGapMeters,
                    VerticalGapMeters = config.VerticalGapMeters,
                    Status = ZoneStatus.Active,
                    IsDeleted = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                effectiveZones.Add(zone);
                zoneUpserts.Add(zone);
                newZoneIds.Add(zone.Id);
            }

            CheckCapacity(zone);
            normalizedConfigs.Add(new ZoneGenerationConfig
            {
                ZoneId = zone.Id,
                BoothWidth = config.BoothWidth,
                BoothHeight = config.BoothHeight,
                Gap = config.Gap,
                AisleWidth = config.AisleWidth,
                Columns = config.Columns,
                AisleEveryNRows = config.AisleEveryNRows,
                ZoneWidthMeters = config.ZoneWidthMeters,
                ZoneLengthMeters = config.ZoneLengthMeters,
                BoothWidthMeters = config.BoothWidthMeters,
                BoothLengthMeters = config.BoothLengthMeters,
                HorizontalGapMeters = config.HorizontalGapMeters,
                VerticalGapMeters = config.VerticalGapMeters,
                CustomX = config.CustomX,
                CustomY = config.CustomY
            });
        }

        return new ZoneMaterializationPlan(
            effectiveZones,
            CloneRequestWithConfigs(request, normalizedConfigs),
            zoneUpserts, newZoneIds, capacityErrors);
    }

    private static GenerateLayoutRequest CloneRequestWithConfigs(
        GenerateLayoutRequest request, List<ZoneGenerationConfig> configs) => new()
    {
        ExpectedUpdatedAt = request.ExpectedUpdatedAt,
        RequestedBoothCount = request.RequestedBoothCount,
        ZoneConfigs = configs,
        DefaultZoneCapacity = request.DefaultZoneCapacity,
        StartX = request.StartX,
        StartY = request.StartY,
        ZoneMargin = request.ZoneMargin,
        ZonesPerRow = request.ZonesPerRow,
        AutoExpandCanvas = request.AutoExpandCanvas,
        MarketWidthMeters = request.MarketWidthMeters,
        MarketLengthMeters = request.MarketLengthMeters,
        PixelsPerMeter = request.PixelsPerMeter,
        StartXMeters = request.StartXMeters,
        StartYMeters = request.StartYMeters,
        ZoneMarginMeters = request.ZoneMarginMeters,
        DefaultZoneWidthMeters = request.DefaultZoneWidthMeters,
        DefaultZoneLengthMeters = request.DefaultZoneLengthMeters,
        DefaultBoothWidthMeters = request.DefaultBoothWidthMeters,
        DefaultBoothLengthMeters = request.DefaultBoothLengthMeters,
        DefaultHorizontalGapMeters = request.DefaultHorizontalGapMeters,
        DefaultVerticalGapMeters = request.DefaultVerticalGapMeters
    };

    private static Zone? FindGeneralAreaZone(IEnumerable<Zone> zones)
        => zones.FirstOrDefault(IsGeneralAreaZone);

    private static bool IsGeneralAreaZone(Zone zone)
        => string.Equals(zone.ZoneCode?.Trim(), GeneralAreaZoneCode, StringComparison.OrdinalIgnoreCase);

    private static string NextZoneCode(HashSet<string> usedZoneCodes)
    {
        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            var code = letter.ToString();
            if (usedZoneCodes.Add(code)) return code;
        }
        for (var first = 'A'; first <= 'Z'; first++)
            for (var second = 'A'; second <= 'Z'; second++)
            {
                var code = $"{first}{second}";
                if (usedZoneCodes.Add(code)) return code;
            }
        throw AppException.Conflict("No zone code is available for this night market.", "ZONE_CODE_EXHAUSTED");
    }

    private static string? GetZoneColorPreset(string? zoneType) => zoneType?.Trim().ToLowerInvariant() switch
    {
        "food" or "food_zone" => "#F59E0B",
        "bbq_grilled" or "bbq" or "grilled" => "#EF4444",
        "beverage" or "drink" or "drinks" => "#3B82F6",
        "snack_dessert" or "snack" or "dessert" => "#EC4899",
        "mixed" or "mix" => "#8B5CF6",
        "other" or "retail" or "handicraft" or "entertainment" or "services" or "seating" => "#64748B",
        _ => null
    };

    private sealed record ZoneMaterializationPlan(
        IReadOnlyCollection<Zone> EffectiveZones,
        GenerateLayoutRequest NormalizedRequest,
        List<Zone> ZoneUpserts,
        HashSet<Guid> NewZoneIds,
        List<string> CapacityErrors);
}

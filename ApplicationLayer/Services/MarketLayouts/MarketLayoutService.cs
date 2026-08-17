using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Subscriptions;
using ApplicationLayer.Services.Realtime;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketLayouts;

public class MarketLayoutService : IMarketLayoutService
{
    private const string GeneralAreaZoneName = "General Area";
    private const string GeneralAreaZoneCode = "G";

    private readonly IMarketLayoutRepository _layouts;
    private readonly IZoneRepository _zones;
    private readonly INightMarketRepository _nightMarkets;
    private readonly IMapper _mapper;
    private readonly ILayoutGraphValidationService _graphValidation;
    private readonly ISubscriptionEntitlementService _entitlements;
    private readonly ILayoutGeneratorService _generator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeEventPublisher _eventPublisher;

    public MarketLayoutService(
        IMarketLayoutRepository layouts, IZoneRepository zones,
        INightMarketRepository nightMarkets, IMapper mapper, ILayoutGraphValidationService graphValidation,
        ISubscriptionEntitlementService entitlements, ILayoutGeneratorService generator, IUnitOfWork unitOfWork,
        IRealtimeEventPublisher eventPublisher)
    {
        _layouts = layouts;
        _zones = zones;
        _nightMarkets = nightMarkets;
        _mapper = mapper;
        _graphValidation = graphValidation;
        _entitlements = entitlements;
        _generator = generator;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
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
        int? maxLayouts = null;
        if (market.MarketOwnerId.HasValue)
        {
            var hasSubscription = await _entitlements.HasActiveMarketSubscriptionAsync(market.MarketOwnerId.Value);
            if (!hasSubscription)
                throw AppException.Forbidden("An active Market subscription is required to perform this action.", "MARKET_SUBSCRIPTION_REQUIRED");

            maxLayouts = await _entitlements.GetMaxLayoutsPerMarketAsync(market.MarketOwnerId.Value);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize layout creation per market. This makes both the package-limit
            // check and MAX(version) + 1 allocation atomic across app instances.
            await _layouts.AcquireMarketLockAsync(nightMarketId, cancellationToken);

            if (maxLayouts.HasValue)
            {
                var currentLayouts = await _layouts.CountAsync(
                    layout => layout.NightMarketId == nightMarketId && !layout.IsDeleted);
                if (currentLayouts >= maxLayouts.Value)
                    throw AppException.Forbidden(
                        $"Your current package allows a maximum of {maxLayouts.Value} layout(s) per market. Please upgrade to create more.",
                        "LAYOUT_LIMIT_REACHED");
            }

            var resolvedVersion = await _layouts.GetNextVersionAsync(nightMarketId, cancellationToken);
            await ValidateIdentityAsync(
                nightMarketId, request.LayoutName, resolvedVersion, null, cancellationToken);

            var now = DateTime.UtcNow;
            var layout = _mapper.Map<MarketLayout>(request);
            layout.Id = Guid.NewGuid();
            layout.NightMarketId = nightMarketId;
            layout.Version = resolvedVersion;
            layout.Status = MarketLayoutStatus.Inactive;
            layout.IsDeleted = false;
            layout.CreatedAt = now;
            layout.UpdatedAt = now;

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
        if (layout.Status == MarketLayoutStatus.Active)
            throw AppException.Conflict("Deactivate the market layout before changing its information.");

        await ValidateIdentityAsync(layout.NightMarketId, request.LayoutName, layout.Version, layoutId, cancellationToken);
        _mapper.Map(request, layout);
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
        if (layout.Status == MarketLayoutStatus.Active)
            throw AppException.Conflict("Deactivate the market layout before replacing its image.");

        if (!Uri.TryCreate(request.LayoutImageUrl, UriKind.RelativeOrAbsolute, out var imageUri)
            || (imageUri.IsAbsoluteUri && imageUri.Scheme is not ("http" or "https"))
            || (!imageUri.IsAbsoluteUri && !request.LayoutImageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)))
            throw AppException.BadRequest(
                "The uploaded layout image URL is invalid.",
                "LAYOUT_IMAGE_URL_INVALID");

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
        if (layout.Status == MarketLayoutStatus.Active)
            throw AppException.Conflict("Deactivate the market layout before updating its dimensions.");

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

    public async Task<ApiResponse<object>> SaveGraphTransactionalAsync(
        Guid layoutId, SaveGraphRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        if (layout.Status == MarketLayoutStatus.Active)
            throw AppException.Conflict("Deactivate the market layout before modifying the graph.");

        var market = await EnsureNightMarketExistsAsync(layout.NightMarketId, cancellationToken);
        var existingNodes = await _layouts.GetNodesByLayoutIdAsync(layoutId, cancellationToken);
        var activeZones = await _zones.GetActiveByNightMarketIdAsync(
            layout.NightMarketId, cancellationToken: cancellationToken);
        if (market.MarketOwnerId != null)
        {
            var maxSlots = await _entitlements.GetMaxSlotsPerMarketAsync(market.MarketOwnerId.Value);
            var boothSlotsCount = request.Nodes.Count(n => n.NodeType == LayoutNodeType.BoothSlot);
            if (boothSlotsCount > maxSlots)
            {
                throw AppException.Forbidden(
                    $"Your current package allows a maximum of {maxSlots} booth slots per market layout. Please upgrade to add more.",
                    "PLAN_LIMIT_REACHED");
            }

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
                        "Zone management is available with the Pro Market package.",
                        "ZONE_MANAGEMENT_NOT_INCLUDED");
                }
            }
        }

        if (Math.Abs((layout.UpdatedAt - request.ExpectedUpdatedAt).TotalSeconds) > 1)
            throw AppException.Conflict(
                "This layout was changed in another session. Reload it before saving.",
                "LAYOUT_VERSION_CONFLICT");

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
        var edges = request.Edges.Select(edge => new LayoutEdge
        {
            Id = edge.Id,
            LayoutId = layoutId,
            FromNodeId = edge.FromNodeId,
            ToNodeId = edge.ToNodeId,
            Distance = edge.Distance ?? CalculateDistance(nodes, edge.FromNodeId, edge.ToNodeId),
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

        await _layouts.SaveGraphTransactionalAsync(layoutId, blocks, nodes, edges, null, cancellationToken);
        var saved = await _layouts.GetActiveByIdAsync(layoutId, cancellationToken)
            ?? throw AppException.NotFound("Market layout was not found.");
        return ApiResponse<object>.SuccessResponse(new
        {
            LayoutId = layoutId,
            BlockCount = blocks.Count,
            NodeCount = nodes.Count,
            EdgeCount = edges.Count,
            saved.UpdatedAt
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
        if (layout.Status == MarketLayoutStatus.Active)
            throw AppException.Conflict("Deactivate the market layout before modifying the graph.", "LAYOUT_IS_ACTIVE");

        var market = await EnsureNightMarketExistsAsync(layout.NightMarketId, cancellationToken);
        if (!market.BoundaryWidthMeters.HasValue || !market.BoundaryHeightMeters.HasValue || market.BoundaryWidthMeters.Value <= 0 || market.BoundaryHeightMeters.Value <= 0)
            throw AppException.BadRequest("Khu chợ chưa được thiết lập kích thước (chiều rộng và chiều dài). Vui lòng cập nhật kích thước khu chợ trước.", "MARKET_BOUNDARY_MISSING");

        request.MarketWidthMeters = market.BoundaryWidthMeters.Value;
        request.MarketLengthMeters = market.BoundaryHeightMeters.Value;
        request.ZoneConfigs ??= [];
        var physicalPreparation = PhysicalGridCalculator.Prepare(request);
        var hasZoneManagement = await ValidateGenerationEntitlementAsync(market, request);

        var zones = await _zones.GetActiveByNightMarketIdAsync(layout.NightMarketId, cancellationToken: cancellationToken);
        ValidateZoneConfigs(zones, request.ZoneConfigs);

        var existingNodes = await _layouts.GetNodesByLayoutIdAsync(layoutId, cancellationToken);
        var editorData = await _layouts.GetEditorLayoutAsync(layoutId, cancellationToken)
                         ?? throw AppException.NotFound("Market layout was not found.");

        var boothLocations = editorData.BoothLocations.ToList();
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
            var maxSlots = await _entitlements.GetMaxSlotsPerMarketAsync(market.MarketOwnerId.Value);
            var slotCount = generationResult.Nodes.Count(n => n.NodeType == LayoutNodeType.BoothSlot);
            if (slotCount > maxSlots)
            {
                result.Errors.Add(
                    $"This layout would contain {slotCount} booth slots, but your current package allows a maximum of {maxSlots}. Please upgrade or reduce capacity.");
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
        if (layout.Status == MarketLayoutStatus.Active)
            throw AppException.Conflict("Deactivate the market layout before modifying the graph.", "LAYOUT_IS_ACTIVE");

        if (Math.Abs((layout.UpdatedAt - request.ExpectedUpdatedAt).TotalSeconds) > 1)
            throw AppException.Conflict(
                "This layout was changed in another session. Reload it before generating.",
                "LAYOUT_VERSION_CONFLICT");

        var market = await EnsureNightMarketExistsAsync(layout.NightMarketId, cancellationToken);
        if (!market.BoundaryWidthMeters.HasValue || !market.BoundaryHeightMeters.HasValue || market.BoundaryWidthMeters.Value <= 0 || market.BoundaryHeightMeters.Value <= 0)
            throw AppException.BadRequest("Khu chợ chưa được thiết lập kích thước (chiều rộng và chiều dài). Vui lòng cập nhật kích thước khu chợ trước.", "MARKET_BOUNDARY_MISSING");

        request.MarketWidthMeters = market.BoundaryWidthMeters.Value;
        request.MarketLengthMeters = market.BoundaryHeightMeters.Value;
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
        var plan = BuildZoneMaterializationPlan(market, zones, existingNodes, boothLocations, request, hasZoneManagement);
        if (plan.CapacityErrors.Count > 0)
            throw AppException.Conflict(plan.CapacityErrors[0], "CAPACITY_BELOW_ASSIGNED");

        // Conflict check is now fully handled by _generator.ComputeGeneration (which calls ComputePreview)
        // It accurately detects both reduced capacities and omitted zones that drop assigned booths.
        var result = _generator.ComputeGeneration(
            layout, plan.EffectiveZones, existingNodes, boothLocations, plan.NormalizedRequest);
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
            var maxSlots = await _entitlements.GetMaxSlotsPerMarketAsync(market.MarketOwnerId.Value);
            var slotCount = result.Nodes.Count(n => n.NodeType == LayoutNodeType.BoothSlot);
            if (slotCount > maxSlots)
                throw AppException.Forbidden(
                    $"Your current package allows a maximum of {maxSlots} booth slots per market layout. Please upgrade to add more.",
                    "PLAN_LIMIT_REACHED");
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
        var finalNodeIds = result.Nodes.Select(n => n.Id).ToHashSet();
        var utilityEdges = existingEdges
            .Where(e => !e.IsDeleted
                && finalNodeIds.Contains(e.FromNodeId)
                && finalNodeIds.Contains(e.ToNodeId)
                && !generatedSlotNodeIds.Contains(e.FromNodeId)
                && !generatedSlotNodeIds.Contains(e.ToNodeId))
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
        var entrances = nodes.Where(node => node.NodeType == LayoutNodeType.Entrance).ToList();
        var junctions = nodes.Where(node => node.NodeType == LayoutNodeType.Junction).ToList();
        var slots = nodes.Where(node => node.NodeType == LayoutNodeType.BoothSlot).ToList();

        if (slots.Count == 0)
            return;

        if (entrances.Count == 0)
            throw AppException.Conflict(
                "Add at least one entrance before generating booth slots.",
                "LAYOUT_ENTRANCE_REQUIRED");

        if (junctions.Count == 0)
            throw AppException.Conflict(
                "The generated layout has no walkway junction. Generate the layout again.",
                "LAYOUT_JUNCTION_REQUIRED");

        var nodeIds = nodes.Select(node => node.Id).ToHashSet();
        edges.RemoveAll(edge => edge.IsDeleted
                                || edge.FromNodeId == edge.ToNodeId
                                || !nodeIds.Contains(edge.FromNodeId)
                                || !nodeIds.Contains(edge.ToNodeId));

        var pairs = edges
            .Select(edge => CanonicalEdgePair(edge.FromNodeId, edge.ToNodeId))
            .ToHashSet();
        var junctionIds = junctions.Select(junction => junction.Id).ToHashSet();

        // Every slot must first be attached to the junction belonging to its zone/block.
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
                .FirstOrDefault()
                ?? junctions.OrderBy(candidate => SquaredDistance(slot, candidate)).First();

            AddConnectivityEdge(layoutId, slot, junction, edges, pairs);
        }

        var reachable = ReachableFromEntrances(entrances.Select(node => node.Id), edges);

        // Join each disconnected zone walkway to the closest already-reachable corridor.
        foreach (var junction in junctions.Where(node => !reachable.Contains(node.Id)).ToList())
        {
            var anchor = entrances.Concat(junctions)
                .Where(node => reachable.Contains(node.Id))
                .OrderBy(node => SquaredDistance(node, junction))
                .FirstOrDefault();

            if (anchor is null)
                break;

            AddConnectivityEdge(layoutId, anchor, junction, edges, pairs);
            reachable = ReachableFromEntrances(entrances.Select(node => node.Id), edges);
        }

        reachable = ReachableFromEntrances(entrances.Select(node => node.Id), edges);
        var unreachableSlots = slots
            .Where(slot => !reachable.Contains(slot.Id))
            .Select(slot => slot.SlotCode ?? slot.NodeName ?? "Unnamed slot")
            .ToList();

        if (unreachableSlots.Count > 0)
            throw AppException.Conflict(
                $"The generated paths could not reach these booth slots: {string.Join(", ", unreachableSlots)}. Please regenerate the layout.",
                "LAYOUT_GENERATED_PATH_DISCONNECTED");
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

    public async Task<ApiResponse<MarketLayoutValidationResponse>> ValidateAsync(
        Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipOnlyAsync(layout, actorId, cancellationToken);
        var graphResult = await _graphValidation.ValidateAsync(layoutId, cancellationToken);
        var planLimitError = await GetPlanSlotLimitErrorAsync(layout, actorId, cancellationToken);
        var result = planLimitError is null
            ? graphResult
            : new MarketLayoutValidationResponse
            {
                Errors = graphResult.Errors.Concat([planLimitError]).ToArray(),
                Warnings = graphResult.Warnings
            };
        return ApiResponse<MarketLayoutValidationResponse>.SuccessResponse(result,
            result.IsValid ? "Market layout is valid." : "Market layout validation failed.");
    }

    public async Task<ApiResponse<MarketLayoutResponse>> ActivateAsync(
        Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await _layouts.GetEditorLayoutAsync(layoutId, cancellationToken)
                     ?? throw AppException.NotFound("Market layout was not found.");
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        var graphValidation = await _graphValidation.ValidateAsync(layoutId, cancellationToken);
        var planLimitError = await GetPlanSlotLimitErrorAsync(layout, actorId, cancellationToken);
        var validation = planLimitError is null
            ? graphValidation
            : new MarketLayoutValidationResponse
            {
                Errors = graphValidation.Errors.Concat([planLimitError]).ToArray(),
                Warnings = graphValidation.Warnings
            };
        if (!validation.IsValid)
        {
            var fieldErrors = new Dictionary<string, string[]>
            {
                ["layout"] = validation.Errors.ToArray()
            };
            throw AppException.Validation(
                "This layout cannot be activated yet. " + string.Join(" ", validation.Errors),
                fieldErrors,
                "LAYOUT_VALIDATION_FAILED");
        }

        layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        var now = DateTime.UtcNow;
        await _layouts.ActivateExclusiveAsync(layout.NightMarketId, layout.Id, now, cancellationToken);

        layout.Status = MarketLayoutStatus.Active;
        layout.UpdatedAt = now;
        try
        {
            await _eventPublisher.PublishAsync(new RealtimeEvent
            {
                EventType = "LayoutActivated",
                GroupName = RealtimeGroups.Layout(layout.Id),
                Payload = new { layoutId = layout.Id, nightMarketId = layout.NightMarketId }
            });
            if (layout.NightMarketId != Guid.Empty)
                await _eventPublisher.PublishAsync(new RealtimeEvent
                {
                    EventType = "LayoutActivated",
                    GroupName = RealtimeGroups.Market(layout.NightMarketId),
                    Payload = new { layoutId = layout.Id, nightMarketId = layout.NightMarketId }
                });
        }
        catch { }
        return ApiResponse<MarketLayoutResponse>.SuccessResponse(_mapper.Map<MarketLayoutResponse>(layout), "Market layout activated successfully.");
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
        var maxSlots = await _entitlements.GetMaxSlotsPerMarketAsync(actorId.Value);
        return slotCount > maxSlots
            ? $"This layout contains {slotCount} booth slots, but your current package allows a maximum of {maxSlots}. Reduce the layout capacity or upgrade the package."
            : null;
    }

    public async Task<ApiResponse<MarketLayoutResponse>> DeactivateAsync(
        Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        if (layout.Status != MarketLayoutStatus.Active)
            throw AppException.Conflict("Only an active market layout can be deactivated.");

        layout.Status = MarketLayoutStatus.Inactive;
        layout.UpdatedAt = DateTime.UtcNow;
        _layouts.Update(layout);

        await _layouts.SaveChangesAsync();
        try
        {
            await _eventPublisher.PublishAsync(new RealtimeEvent
            {
                EventType = "LayoutDeactivated",
                GroupName = RealtimeGroups.Layout(layout.Id),
                Payload = new { layoutId = layout.Id, nightMarketId = layout.NightMarketId }
            });
            if (layout.NightMarketId != Guid.Empty)
                await _eventPublisher.PublishAsync(new RealtimeEvent
                {
                    EventType = "LayoutDeactivated",
                    GroupName = RealtimeGroups.Market(layout.NightMarketId),
                    Payload = new { layoutId = layout.Id, nightMarketId = layout.NightMarketId }
                });
        }
        catch { }
        return ApiResponse<MarketLayoutResponse>.SuccessResponse(_mapper.Map<MarketLayoutResponse>(layout), "Market layout deactivated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        if (layout.Status == MarketLayoutStatus.Active)
            throw AppException.Conflict("Deactivate the market layout before archiving it.");

        layout.Status = MarketLayoutStatus.Archived;
        layout.UpdatedAt = DateTime.UtcNow;
        _layouts.Delete(layout);

        await _layouts.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { layout.Id }, "Market layout archived successfully.");
    }

    private async Task<MarketLayout> GetActiveLayoutAsync(Guid id, CancellationToken cancellationToken)
        => await _layouts.GetActiveByIdAsync(id, cancellationToken)
           ?? throw AppException.NotFound("Market layout was not found.");

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

    private async Task ValidateIdentityAsync(
        Guid nightMarketId, string name, int version, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw AppException.BadRequest("Layout name is required.");

        if (version <= 0)
            throw AppException.BadRequest("Layout version must be greater than zero.");

        if (await _layouts.ActiveNameOrVersionExistsAsync(nightMarketId, name, version, excludeId, cancellationToken))
            throw AppException.Conflict("Layout name or version already exists in this night market.");
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
                "Zone management is available with the Pro Market package.",
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

            if (config.Capacity is not > 0)
                throw AppException.BadRequest(
                    $"Capacity greater than zero is required for new zone '{config.ZoneName.Trim()}'.",
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

        var effectiveZones = zones.ToList();
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
        "food" => "#F59E0B",
        "drink" => "#3B82F6",
        "dessert" => "#EC4899",
        _ => null
    };

    private sealed record ZoneMaterializationPlan(
        IReadOnlyCollection<Zone> EffectiveZones,
        GenerateLayoutRequest NormalizedRequest,
        List<Zone> ZoneUpserts,
        HashSet<Guid> NewZoneIds,
        List<string> CapacityErrors);
}

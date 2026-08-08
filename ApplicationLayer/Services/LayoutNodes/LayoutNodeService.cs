using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Subscriptions;
using ApplicationLayer.Services.MarketLayouts;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.LayoutNodes;

public class LayoutNodeService : ILayoutNodeService
{
    private readonly ILayoutNodeRepository _nodes;
    private readonly ILayoutEdgeRepository _edges;
    private readonly IBoothLocationRepository _locations;
    private readonly IMarketLayoutRepository _layouts;
    private readonly IZoneRepository _zones;
    private readonly INightMarketRepository _nightMarkets;
    private readonly ISubscriptionEntitlementService _entitlements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public LayoutNodeService(ILayoutNodeRepository nodes, ILayoutEdgeRepository edges,
        IBoothLocationRepository locations, IMarketLayoutRepository layouts, IZoneRepository zones,
        INightMarketRepository nightMarkets, ISubscriptionEntitlementService entitlements,
        IUnitOfWork unitOfWork, IMapper mapper)
        => (_nodes, _edges, _locations, _layouts, _zones, _nightMarkets, _entitlements, _unitOfWork, _mapper)
            = (nodes, edges, locations, layouts, zones, nightMarkets, entitlements, unitOfWork, mapper);

    public async Task<ApiResponse<PaginationResp<LayoutNodeResponse>>> GetAllAsync(Guid layoutId, MapListRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipOnlyAsync(layout, actorId, cancellationToken);
        var page = await _nodes.GetPagedAsync(layoutId, request.Keyword, request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<LayoutNodeResponse>>.SuccessResponse(
            _mapper.MapPage<LayoutNode, LayoutNodeResponse>(page, request));
    }

    public async Task<ApiResponse<LayoutNodeResponse>> GetAsync(Guid id, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var node = await GetNodeAsync(id, cancellationToken);
        var layout = await GetLayoutAsync(node.LayoutId, cancellationToken);
        await EnsureLayoutOwnershipOnlyAsync(layout, actorId, cancellationToken);
        return ApiResponse<LayoutNodeResponse>.SuccessResponse(_mapper.Map<LayoutNodeResponse>(node));
    }

    public async Task<ApiResponse<LayoutNodeResponse>> CreateAsync(Guid layoutId, CreateLayoutNodeRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var layout = await GetLayoutAsync(layoutId, cancellationToken);
        EnsureLayoutEditable(layout);
        var market = await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);

        if (request.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot && market.MarketOwnerId.HasValue)
        {
            var maxSlots = await _entitlements.GetMaxSlotsPerMarketAsync(market.MarketOwnerId.Value);
            var existingNodes = await _nodes.FindAsync(n => n.LayoutId == layoutId && n.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot && !n.IsDeleted);
            if (existingNodes.Count() >= maxSlots)
                throw AppException.Forbidden($"Your current package allows a maximum of {maxSlots} booth slots per market layout. Please upgrade to add more.", "PLAN_LIMIT_REACHED");
        }

        ValidatePosition(layout, request.XCoordinate, request.YCoordinate);
        await ValidateZoneAsync(layout, request.ZoneId, cancellationToken);
        await ValidateAndNormalizeCinemaMetadataAsync(layoutId, request, null);
        var node = _mapper.Map<LayoutNode>(request);
        var now = DateTime.UtcNow;
        node.Id = Guid.NewGuid(); node.LayoutId = layoutId; node.IsDeleted = false; node.CreatedAt = now; node.UpdatedAt = now;
        await _nodes.AddAsync(node); await _nodes.SaveChangesAsync();
        return ApiResponse<LayoutNodeResponse>.SuccessResponse(_mapper.Map<LayoutNodeResponse>(node), "Layout node created successfully.");
    }

    public async Task<ApiResponse<IReadOnlyCollection<LayoutNodeResponse>>> CreateBatchAsync(Guid layoutId, IReadOnlyCollection<CreateLayoutNodeRequest> requests, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        if (requests.Count == 0) throw AppException.BadRequest("At least one node is required.");
        var layout = await GetLayoutAsync(layoutId, cancellationToken);
        EnsureLayoutEditable(layout);
        var market = await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);

        var newBoothSlots = requests.Count(r => r.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot);
        if (newBoothSlots > 0 && market.MarketOwnerId.HasValue)
        {
            var maxSlots = await _entitlements.GetMaxSlotsPerMarketAsync(market.MarketOwnerId.Value);
            var existingNodes = await _nodes.FindAsync(n => n.LayoutId == layoutId && n.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot && !n.IsDeleted);
            if (existingNodes.Count() + newBoothSlots > maxSlots)
                throw AppException.Forbidden($"Your current package allows a maximum of {maxSlots} booth slots per market layout. Please upgrade to add more.", "PLAN_LIMIT_REACHED");
        }

        foreach (var request in requests)
        {
            await ValidateZoneAsync(layout, request.ZoneId, cancellationToken);
            await ValidateAndNormalizeCinemaMetadataAsync(layoutId, request, null);
        }

        var duplicateCodes = requests
            .Where(x => x.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot)
            .Select(x => x.SlotCode!)
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();
        if (duplicateCodes.Count > 0)
            throw AppException.Conflict(
                $"Booth slot code '{duplicateCodes[0]}' is duplicated in this request.",
                "LAYOUT_SLOT_CODE_DUPLICATED");

        var now = DateTime.UtcNow;
        var nodes = requests.Select(request =>
        {
            ValidatePosition(layout, request.XCoordinate, request.YCoordinate);
            var node = _mapper.Map<LayoutNode>(request);
            node.Id = Guid.NewGuid(); node.LayoutId = layoutId; node.IsDeleted = false; node.CreatedAt = now; node.UpdatedAt = now;
            return node;
        }).ToList();
        await _nodes.AddRangeAsync(nodes); await _nodes.SaveChangesAsync();
        return ApiResponse<IReadOnlyCollection<LayoutNodeResponse>>.SuccessResponse(_mapper.Map<List<LayoutNodeResponse>>(nodes), "Layout nodes created successfully.");
    }

    public async Task<ApiResponse<LayoutNodeResponse>> UpdateAsync(Guid id, UpdateLayoutNodeRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var node = await GetNodeAsync(id, cancellationToken);
        var layout = await GetLayoutAsync(node.LayoutId, cancellationToken);
        EnsureLayoutEditable(layout);
        var market = await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);

        if (request.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot && node.NodeType != DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot && market.MarketOwnerId.HasValue)
        {
            var maxSlots = await _entitlements.GetMaxSlotsPerMarketAsync(market.MarketOwnerId.Value);
            var existingNodes = await _nodes.FindAsync(n => n.LayoutId == node.LayoutId && n.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot && !n.IsDeleted);
            if (existingNodes.Count() >= maxSlots)
                throw AppException.Forbidden($"Your current package allows a maximum of {maxSlots} booth slots per market layout. Please upgrade to add more.", "PLAN_LIMIT_REACHED");
        }

        ValidatePosition(layout, request.XCoordinate, request.YCoordinate);
        await ValidateZoneAsync(layout, request.ZoneId, cancellationToken);
        await ValidateAndNormalizeCinemaMetadataAsync(node.LayoutId, request, node.Id);

        var positionChanged = request.XCoordinate != node.Xcoordinate || request.YCoordinate != node.Ycoordinate;
        _mapper.Map(request, node); node.UpdatedAt = DateTime.UtcNow;
        _nodes.Update(node);
        if (positionChanged)
        {
            await RecalculateConnectedEdgesAsync(node.LayoutId, node.Id, cancellationToken);
        }
        await _nodes.SaveChangesAsync();
        await _edges.SaveChangesAsync();
        return ApiResponse<LayoutNodeResponse>.SuccessResponse(_mapper.Map<LayoutNodeResponse>(node), "Layout node updated successfully.");
    }

    public async Task<ApiResponse<LayoutNodeResponse>> UpdatePositionAsync(Guid id, UpdateLayoutNodePositionRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var node = await GetNodeAsync(id, cancellationToken);
        var layout = await GetLayoutAsync(node.LayoutId, cancellationToken);
        EnsureLayoutEditable(layout);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        // A booth-slot swap is identified by the two slot IDs. Its request
        // coordinates are display hints and may be stale after a prior drag,
        // so they must not reject an otherwise valid same-zone swap.
        if (!request.SwapWithNodeId.HasValue)
            ValidatePosition(layout, request.XCoordinate, request.YCoordinate);
        if (node.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot)
        {
            var layoutNodes = await _nodes.GetByLayoutAsync(node.LayoutId, cancellationToken: cancellationToken);
            var layoutBlocks = await _layouts.GetBlocksByLayoutIdAsync(node.LayoutId, cancellationToken);
            if (request.SwapWithNodeId.HasValue)
            {
                var swapNode = layoutNodes.FirstOrDefault(candidate =>
                    candidate.Id == request.SwapWithNodeId.Value && !candidate.IsDeleted);
                if (swapNode is null || swapNode.NodeType != DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot)
                    throw AppException.BadRequest("The booth slot selected for swapping was not found.", "LAYOUT_SWAP_TARGET_INVALID");

                var belongsToSameBlock = node.LayoutBlockId.HasValue
                    && node.LayoutBlockId == swapNode.LayoutBlockId;
                var belongsToSameLegacyZone = !node.LayoutBlockId.HasValue
                    && node.ZoneId.HasValue
                    && node.ZoneId == swapNode.ZoneId;
                if (!belongsToSameBlock && !belongsToSameLegacyZone)
                    throw AppException.BadRequest("Booth slots can only be swapped inside the same zone.", "LAYOUT_SWAP_OUTSIDE_ZONE");

                var nodeOldX = node.Xcoordinate;
                var nodeOldY = node.Ycoordinate;
                var nodeOldRow = node.RowIndex;
                var nodeOldColumn = node.ColumnIndex;

                // A swap only exchanges two persisted positions inside the same
                // Zone. Both positions have already passed geometry validation
                // when created, so revalidating each slot while the other one
                // is still in place incorrectly reports a collision.

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
                    var now = DateTime.UtcNow;
                    node.Xcoordinate = swapNode.Xcoordinate;
                    node.Ycoordinate = swapNode.Ycoordinate;
                    node.RowIndex = swapNode.RowIndex;
                    node.ColumnIndex = swapNode.ColumnIndex;
                    node.UpdatedAt = now;

                    swapNode.Xcoordinate = nodeOldX;
                    swapNode.Ycoordinate = nodeOldY;
                    swapNode.RowIndex = nodeOldRow;
                    swapNode.ColumnIndex = nodeOldColumn;
                    swapNode.UpdatedAt = now;

                    _nodes.Update(node);
                    _nodes.Update(swapNode);
                    await RecalculateConnectedEdgesAsync(node.LayoutId, node.Id, cancellationToken);
                    await RecalculateConnectedEdgesAsync(swapNode.LayoutId, swapNode.Id, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _unitOfWork.CommitTransactionAsync(cancellationToken);
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    throw;
                }

                return ApiResponse<LayoutNodeResponse>.SuccessResponse(
                    _mapper.Map<LayoutNodeResponse>(node),
                    "Booth slots swapped successfully.");
            }

            var geometryErrors = LayoutGeometryValidator.ValidateNodeMove(
                layout,
                node,
                request.XCoordinate,
                request.YCoordinate,
                layoutNodes,
                layoutBlocks);
            if (geometryErrors.Count > 0)
                throw AppException.BadRequest(geometryErrors[0], "LAYOUT_NODE_POSITION_INVALID");

            var block = layoutBlocks.FirstOrDefault(candidate =>
                candidate.Id == node.LayoutBlockId
                || (!node.LayoutBlockId.HasValue && node.ZoneId.HasValue && candidate.ZoneId == node.ZoneId));
            if (block is not null)
            {
                const int columns = 4;
                const double gap = 12;
                const double paddingX = 16;
                // The Market Owner canvas reserves the top of each zone for
                // its label.  Use the same geometry when resolving a manual
                // move so the persisted grid and the rendered grid agree.
                const double paddingTop = 52;
                const double paddingBottom = 16;
                var slotCount = layoutNodes.Count(candidate =>
                    candidate.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot
                    && !candidate.IsDeleted
                    && (candidate.LayoutBlockId == block.Id
                        || (!candidate.LayoutBlockId.HasValue && candidate.ZoneId.HasValue && candidate.ZoneId == block.ZoneId)));
                var rows = Math.Max(1, (int)Math.Ceiling(Math.Max(slotCount, 1) / (double)columns));
                var gridWidth = Math.Max(1, block.Width - paddingX * 2);
                var gridHeight = Math.Max(1, block.Height - paddingTop - paddingBottom);
                var cellWidth = Math.Max(1, (gridWidth - gap * (columns - 1)) / columns);
                var cellHeight = Math.Max(1, (gridHeight - gap * (rows - 1)) / rows);
                var firstCenterX = block.X + paddingX + cellWidth / 2;
                var firstCenterY = block.Y + paddingTop + cellHeight / 2;
                node.ColumnIndex = Math.Clamp(
                    (int)Math.Round(((double)request.XCoordinate - firstCenterX) / (cellWidth + gap)),
                    0,
                    columns - 1);
                node.RowIndex = Math.Clamp(
                    (int)Math.Round(((double)request.YCoordinate - firstCenterY) / (cellHeight + gap)),
                    0,
                    rows - 1);
            }
        }
        node.Xcoordinate = request.XCoordinate; node.Ycoordinate = request.YCoordinate; node.UpdatedAt = DateTime.UtcNow;
        _nodes.Update(node);
        await RecalculateConnectedEdgesAsync(node.LayoutId, node.Id, cancellationToken);
        await _nodes.SaveChangesAsync();
        await _edges.SaveChangesAsync();
        return ApiResponse<LayoutNodeResponse>.SuccessResponse(_mapper.Map<LayoutNodeResponse>(node), "Node position updated successfully.");
    }

    public async Task<ApiResponse<LayoutNodeResponse>> UpdateAccessibilityAsync(Guid id, UpdateAccessibilityRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var node = await GetNodeAsync(id, cancellationToken);
        var layout = await GetLayoutAsync(node.LayoutId, cancellationToken);
        EnsureLayoutEditable(layout);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        node.IsAccessible = request.IsAccessible; node.UpdatedAt = DateTime.UtcNow;
        _nodes.Update(node); await _nodes.SaveChangesAsync();
        return ApiResponse<LayoutNodeResponse>.SuccessResponse(_mapper.Map<LayoutNodeResponse>(node), "Node accessibility updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var node = await GetNodeAsync(id, cancellationToken);
        var layout = await GetLayoutAsync(node.LayoutId, cancellationToken);
        EnsureLayoutEditable(layout);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        if (await _locations.GetCurrentByNodeAsync(id, cancellationToken) is not null)
            throw AppException.Conflict("Release the booth location before deleting this node.");

        if (node.NodeType is DomainLayer.Enums.GeneralEnum.LayoutNodeType.Entrance or DomainLayer.Enums.GeneralEnum.LayoutNodeType.Exit)
        {
            var existingOfSameType = await _nodes.FindAsync(n => n.LayoutId == node.LayoutId && n.NodeType == node.NodeType && !n.IsDeleted);
            if (existingOfSameType.Count() <= 1)
            {
                var typeName = node.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.Entrance ? "Entrance" : "Exit";
                var errorCode = node.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.Entrance
                    ? "LAYOUT_LAST_ENTRANCE_DELETE_FORBIDDEN"
                    : "LAYOUT_LAST_EXIT_DELETE_FORBIDDEN";
                throw AppException.BadRequest($"Cannot remove the last {typeName} from the layout.", errorCode);
            }
        }

        var edges = await _edges.GetByLayoutAsync(node.LayoutId, cancellationToken: cancellationToken);
        _edges.DeleteRange(edges.Where(x => x.FromNodeId == id || x.ToNodeId == id));
        _nodes.Delete(node); node.UpdatedAt = DateTime.UtcNow;
        await _nodes.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { node.Id }, "Layout node deleted successfully.");
    }

    private async Task RecalculateConnectedEdgesAsync(Guid layoutId, Guid movedNodeId, CancellationToken cancellationToken)
    {
        var edges = await _edges.GetByLayoutAsync(layoutId, cancellationToken: cancellationToken);
        var connectedEdges = edges.Where(x => x.FromNodeId == movedNodeId || x.ToNodeId == movedNodeId).ToList();
        if (connectedEdges.Count == 0) return;

        var allNodes = await _nodes.GetByLayoutAsync(layoutId, cancellationToken: cancellationToken);
        var nodesDict = allNodes.ToDictionary(n => n.Id);

        foreach (var edge in connectedEdges)
        {
            if (nodesDict.TryGetValue(edge.FromNodeId, out var fromNode) && nodesDict.TryGetValue(edge.ToNodeId, out var toNode))
            {
                var dist = Math.Max((int)Math.Round(Math.Sqrt(Math.Pow((double)(fromNode.Xcoordinate - toNode.Xcoordinate), 2) + Math.Pow((double)(fromNode.Ycoordinate - toNode.Ycoordinate), 2))), 1);
                if (edge.Distance != dist)
                {
                    edge.Distance = dist;
                    edge.UpdatedAt = DateTime.UtcNow;
                    _edges.Update(edge);
                }
            }
        }
    }

    private async Task<LayoutNode> GetNodeAsync(Guid id, CancellationToken token)
        => await _nodes.GetActiveByIdAsync(id, token) ?? throw AppException.NotFound("Layout node was not found.");
    private async Task<MarketLayout> GetLayoutAsync(Guid id, CancellationToken token)
        => await _layouts.GetActiveByIdAsync(id, token) ?? throw AppException.NotFound("Market layout was not found.");
    private static void EnsureLayoutEditable(MarketLayout layout)
    {
        if (layout.Status == DomainLayer.Enums.GeneralEnum.MarketLayoutStatus.Active)
            throw AppException.Conflict(
                "Deactivate the active layout before editing its map.",
                "LAYOUT_ACTIVE_EDIT_FORBIDDEN");
    }
    private async Task<NightMarket> EnsureLayoutOwnershipAsync(MarketLayout layout, Guid? actorId, CancellationToken cancellationToken)
    {
        var market = await _nightMarkets.GetActiveByIdAsync(layout.NightMarketId, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");
        if (actorId.HasValue && market.MarketOwnerId != actorId)
            throw AppException.Forbidden("You do not have permission to manage this night market's layouts.");

        if (actorId.HasValue)
        {
            var hasSubscription = await _entitlements.HasActiveMarketSubscriptionAsync(actorId.Value);
            if (!hasSubscription)
                throw AppException.Forbidden("An active Market subscription is required to perform this action.", "MARKET_SUBSCRIPTION_REQUIRED");
        }

        return market;
    }
    private async Task EnsureLayoutOwnershipOnlyAsync(MarketLayout layout, Guid? actorId, CancellationToken cancellationToken)
    {
        if (!actorId.HasValue) return;
        var market = await _nightMarkets.GetActiveByIdAsync(layout.NightMarketId, cancellationToken);
        if (market is null || market.MarketOwnerId != actorId)
            throw AppException.Forbidden("You do not have permission to view this night market's layouts.");
    }
    private static void ValidatePosition(MarketLayout layout, decimal x, decimal y)
    {
        if (layout.Width <= 0 || layout.Height <= 0) throw AppException.BadRequest("Upload a valid layout image before adding nodes.");
        if (x < 0 || x > layout.Width || y < 0 || y > layout.Height) throw AppException.BadRequest("Node coordinates must be inside the layout dimensions.");
    }
    private async Task ValidateZoneAsync(MarketLayout layout, Guid? zoneId, CancellationToken token)
    {
        if (!zoneId.HasValue) return;
        var zone = await _zones.GetActiveByIdAsync(zoneId.Value, token);
        if (zone?.NightMarketId != layout.NightMarketId)
            throw AppException.BadRequest("Zone does not belong to the layout's night market.");
    }

    private async Task ValidateAndNormalizeCinemaMetadataAsync(
        Guid layoutId,
        CreateLayoutNodeRequest request,
        Guid? excludedNodeId)
    {
        if (request.NodeType != DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot)
        {
            request.SlotCode = null;
            request.RowIndex = null;
            request.ColumnIndex = null;
            request.LayoutBlockId = null;
            return;
        }

        var normalizedSlotCode = request.SlotCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSlotCode))
            throw AppException.BadRequest(
                "A booth slot code is required.",
                "LAYOUT_SLOT_CODE_REQUIRED");

        var existingSlots = await _nodes.FindAsync(x =>
            x.LayoutId == layoutId &&
            x.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot &&
            !x.IsDeleted);

        if (existingSlots.Any(x =>
                x.Id != excludedNodeId &&
                string.Equals(x.SlotCode?.Trim(), normalizedSlotCode, StringComparison.OrdinalIgnoreCase)))
            throw AppException.Conflict(
                $"Booth slot code '{normalizedSlotCode}' already exists in this layout.",
                "LAYOUT_SLOT_CODE_DUPLICATED");

        request.SlotCode = normalizedSlotCode;
    }
}

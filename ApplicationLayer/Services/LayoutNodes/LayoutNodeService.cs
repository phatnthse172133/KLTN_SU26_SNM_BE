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
    private readonly IMarketResourceQuotaService _resourceQuota;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public LayoutNodeService(ILayoutNodeRepository nodes, ILayoutEdgeRepository edges,
        IBoothLocationRepository locations, IMarketLayoutRepository layouts, IZoneRepository zones,
        INightMarketRepository nightMarkets, ISubscriptionEntitlementService entitlements,
        IMarketResourceQuotaService resourceQuota, IUnitOfWork unitOfWork, IMapper mapper)
        => (_nodes, _edges, _locations, _layouts, _zones, _nightMarkets, _entitlements, _resourceQuota, _unitOfWork, _mapper)
            = (nodes, edges, locations, layouts, zones, nightMarkets, entitlements, resourceQuota, unitOfWork, mapper);

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
        await EnsureLayoutEditableAsync(layout, cancellationToken);
        var market = await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);

        if (request.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot && market.MarketOwnerId.HasValue)
            await _resourceQuota.EnsureCanAddBoothSlotsAsync(
                market.MarketOwnerId.Value, layout.MarketMapId, 1, cancellationToken);

        ValidatePosition(layout, request.XCoordinate, request.YCoordinate);
        await ValidateZoneAsync(layout, request.ZoneId, cancellationToken);
        await ValidateAndNormalizeCinemaMetadataAsync(layoutId, request, null);
        var node = _mapper.Map<LayoutNode>(request);
        var now = DateTime.UtcNow;
        node.Id = Guid.NewGuid(); node.LayoutId = layoutId; node.IsDeleted = false; node.CreatedAt = now; node.UpdatedAt = now;
        if (node.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot)
        {
            var blocks = await _layouts.GetBlocksByLayoutIdAsync(layoutId, cancellationToken);
            var existingNodes = await _nodes.GetByLayoutAsync(layoutId, cancellationToken: cancellationToken);
            if (!node.LayoutBlockId.HasValue && node.ZoneId.HasValue)
            {
                var matches = blocks.Where(block => !block.IsDeleted && block.ZoneId == node.ZoneId).ToList();
                if (matches.Count == 1) node.LayoutBlockId = matches[0].Id;
            }
            if (!node.LayoutBlockId.HasValue)
                throw AppException.BadRequest("Select a zone block before adding a booth slot.", "LAYOUT_SLOT_BLOCK_REQUIRED");
            var errors = LayoutGeometryValidator.ValidateNodeMove(layout, node,
                node.Xcoordinate, node.Ycoordinate, existingNodes, blocks);
            if (errors.Count > 0)
                throw AppException.BadRequest(string.Join(" ", errors), "LAYOUT_NODE_GEOMETRY_INVALID");
        }
        await _nodes.AddAsync(node); await _nodes.SaveChangesAsync();

        // A user-facing Gate is stored as an Entrance for backwards-compatible
        // routing. Link it to the nearest junction when it is placed so the
        // new gate works immediately without manual edge editing.
        if (request.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.Entrance)
        {
            var layoutNodes = await _nodes.GetByLayoutAsync(layoutId, cancellationToken: cancellationToken);
            var nearestJunction = layoutNodes
                .Where(candidate => !candidate.IsDeleted
                    && candidate.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.Junction)
                .OrderBy(candidate => SquaredDistance(node, candidate))
                .FirstOrDefault();

            if (nearestJunction is not null)
            {
                await _edges.AddAsync(new LayoutEdge
                {
                    Id = Guid.NewGuid(),
                    LayoutId = layoutId,
                    FromNodeId = node.Id,
                    ToNodeId = nearestJunction.Id,
                    Distance = (decimal)Math.Sqrt((double)SquaredDistance(node, nearestJunction)),
                    IsBidirectional = true,
                    IsAccessible = true,
                    IsDeleted = false,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                await _edges.SaveChangesAsync();
            }
        }
        return ApiResponse<LayoutNodeResponse>.SuccessResponse(_mapper.Map<LayoutNodeResponse>(node), "Layout node created successfully.");
    }

    public async Task<ApiResponse<IReadOnlyCollection<LayoutNodeResponse>>> CreateBatchAsync(Guid layoutId, IReadOnlyCollection<CreateLayoutNodeRequest> requests, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        if (requests.Count == 0) throw AppException.BadRequest("At least one node is required.");
        var layout = await GetLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutEditableAsync(layout, cancellationToken);
        var market = await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);

        var newBoothSlots = requests.Count(r => r.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot);
        if (newBoothSlots > 0 && market.MarketOwnerId.HasValue)
            await _resourceQuota.EnsureCanAddBoothSlotsAsync(
                market.MarketOwnerId.Value, layout.MarketMapId, newBoothSlots, cancellationToken);

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

        var blocks = await _layouts.GetBlocksByLayoutIdAsync(layoutId, cancellationToken);
        var existingNodes = await _nodes.GetByLayoutAsync(layoutId, cancellationToken: cancellationToken);
        foreach (var node in nodes.Where(node =>
                     node.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot))
        {
            if (!node.LayoutBlockId.HasValue
                && BoothSlotGeometry.TryResolveOwningBlock(
                    layoutId, node, blocks, out var resolvedBlock, out _))
                node.LayoutBlockId = resolvedBlock!.Id;
        }

        var allNodes = existingNodes.Concat(nodes).ToList();
        foreach (var node in nodes)
        {
            var geometryErrors = LayoutGeometryValidator.ValidateNodeMove(
                layout, node, node.Xcoordinate, node.Ycoordinate, allNodes, blocks);
            if (geometryErrors.Count > 0)
                throw AppException.Validation(
                    $"Map point '{node.SlotCode ?? node.NodeName ?? node.Id.ToString()}': {string.Join(" ", geometryErrors)}",
                    new Dictionary<string, string[]> { ["nodes"] = geometryErrors.ToArray() },
                    "LAYOUT_NODE_GEOMETRY_INVALID");
        }

        // One AddRange + SaveChanges is atomic in the current EF unit of work:
        // validation completes for the full incoming batch before any INSERT.
        await _nodes.AddRangeAsync(nodes); await _nodes.SaveChangesAsync();
        return ApiResponse<IReadOnlyCollection<LayoutNodeResponse>>.SuccessResponse(_mapper.Map<List<LayoutNodeResponse>>(nodes), "Layout nodes created successfully.");
    }

    public async Task<ApiResponse<LayoutNodeResponse>> UpdateAsync(Guid id, UpdateLayoutNodeRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var node = await GetNodeAsync(id, cancellationToken);
        var layout = await GetLayoutAsync(node.LayoutId, cancellationToken);
        await EnsureLayoutEditableAsync(layout, cancellationToken);
        var market = await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);

        if (request.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot && node.NodeType != DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot && market.MarketOwnerId.HasValue)
            await _resourceQuota.EnsureCanAddBoothSlotsAsync(
                market.MarketOwnerId.Value, layout.MarketMapId, 1, cancellationToken);

        ValidatePosition(layout, request.XCoordinate, request.YCoordinate);
        await ValidateZoneAsync(layout, request.ZoneId, cancellationToken);
        await ValidateAndNormalizeCinemaMetadataAsync(node.LayoutId, request, node.Id);

        var positionChanged = request.XCoordinate != node.Xcoordinate || request.YCoordinate != node.Ycoordinate;
        _mapper.Map(request, node); node.UpdatedAt = DateTime.UtcNow;
        if (node.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot)
        {
            var layoutNodes = await _nodes.GetByLayoutAsync(node.LayoutId, cancellationToken: cancellationToken);
            var layoutBlocks = await _layouts.GetBlocksByLayoutIdAsync(node.LayoutId, cancellationToken);
            var geometryErrors = LayoutGeometryValidator.ValidateNodeMove(
                layout, node, node.Xcoordinate, node.Ycoordinate, layoutNodes, layoutBlocks);
            if (geometryErrors.Count > 0)
                throw AppException.BadRequest(geometryErrors[0], "LAYOUT_NODE_GEOMETRY_INVALID");
        }
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
        await EnsureLayoutEditableAsync(layout, cancellationToken);
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

                if (!BoothSlotGeometry.TryResolveOwningBlock(
                        layout.Id, node, layoutBlocks, out var nodeBlock, out var nodeOwnershipError))
                    throw AppException.BadRequest(nodeOwnershipError!, "LAYOUT_SLOT_BLOCK_REQUIRED");
                if (!BoothSlotGeometry.TryResolveOwningBlock(
                        layout.Id, swapNode, layoutBlocks, out var swapBlock, out var swapOwnershipError))
                    throw AppException.BadRequest(swapOwnershipError!, "LAYOUT_SLOT_BLOCK_REQUIRED");
                if (nodeBlock!.Id != swapBlock!.Id)
                    throw AppException.BadRequest("Booth slots can only be swapped inside the same zone.", "LAYOUT_SWAP_OUTSIDE_ZONE");

                // Normalize legacy Zone-only ownership only after it resolves
                // unambiguously to one LayoutBlock in this layout.
                node.LayoutBlockId = nodeBlock.Id;
                swapNode.LayoutBlockId = swapBlock.Id;

                var nodeOldX = node.Xcoordinate;
                var nodeOldY = node.Ycoordinate;
                var nodeOldRow = node.RowIndex;
                var nodeOldColumn = node.ColumnIndex;
                var swapOldX = swapNode.Xcoordinate;
                var swapOldY = swapNode.Ycoordinate;
                var swapOldRow = swapNode.RowIndex;
                var swapOldColumn = swapNode.ColumnIndex;

                var now = DateTime.UtcNow;
                node.Xcoordinate = swapOldX;
                node.Ycoordinate = swapOldY;
                node.RowIndex = swapOldRow;
                node.ColumnIndex = swapOldColumn;
                node.UpdatedAt = now;

                swapNode.Xcoordinate = nodeOldX;
                swapNode.Ycoordinate = nodeOldY;
                swapNode.RowIndex = nodeOldRow;
                swapNode.ColumnIndex = nodeOldColumn;
                swapNode.UpdatedAt = now;

                var proposedNodes = layoutNodes
                    .Where(candidate => candidate.Id != node.Id && candidate.Id != swapNode.Id)
                    .Concat([node, swapNode])
                    .ToList();
                var nodeErrors = LayoutGeometryValidator.ValidateNodeMove(
                    layout, node, node.Xcoordinate, node.Ycoordinate, proposedNodes, layoutBlocks);
                var swapErrors = LayoutGeometryValidator.ValidateNodeMove(
                    layout, swapNode, swapNode.Xcoordinate, swapNode.Ycoordinate, proposedNodes, layoutBlocks);
                if (nodeErrors.Count > 0 || swapErrors.Count > 0)
                {
                    node.Xcoordinate = nodeOldX;
                    node.Ycoordinate = nodeOldY;
                    node.RowIndex = nodeOldRow;
                    node.ColumnIndex = nodeOldColumn;
                    swapNode.Xcoordinate = swapOldX;
                    swapNode.Ycoordinate = swapOldY;
                    swapNode.RowIndex = swapOldRow;
                    swapNode.ColumnIndex = swapOldColumn;
                    throw AppException.BadRequest(
                        (nodeErrors.Count > 0 ? nodeErrors : swapErrors)[0],
                        "LAYOUT_NODE_POSITION_INVALID");
                }

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
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

            // A free-form move is no longer represented as a synthetic four-column
            // grid cell. The canonical footprint validator above is authoritative.
            node.RowIndex = null;
            node.ColumnIndex = null;
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
        await EnsureLayoutEditableAsync(layout, cancellationToken);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        node.IsAccessible = request.IsAccessible; node.UpdatedAt = DateTime.UtcNow;
        _nodes.Update(node); await _nodes.SaveChangesAsync();
        return ApiResponse<LayoutNodeResponse>.SuccessResponse(_mapper.Map<LayoutNodeResponse>(node), "Node accessibility updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var node = await GetNodeAsync(id, cancellationToken);
        var layout = await GetLayoutAsync(node.LayoutId, cancellationToken);
        await EnsureLayoutEditableAsync(layout, cancellationToken);
        await EnsureLayoutOwnershipAsync(layout, actorId, cancellationToken);
        if (await _locations.GetCurrentByNodeAsync(id, cancellationToken) is not null)
            throw AppException.Conflict("Release the booth location before deleting this node.");

        if (node.NodeType == DomainLayer.Enums.GeneralEnum.LayoutNodeType.Entrance)
        {
            var existingOfSameType = await _nodes.FindAsync(n => n.LayoutId == node.LayoutId && n.NodeType == node.NodeType && !n.IsDeleted);
            if (existingOfSameType.Count() <= 1)
            {
                throw AppException.BadRequest("Cannot remove the last gate from the layout.", "LAYOUT_LAST_ENTRANCE_DELETE_FORBIDDEN");
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
        => await _nodes.GetActiveByIdAsync(id, token) ?? throw AppException.NotFound("Layout node was not found.", "LAYOUT_NODE_NOT_FOUND");
    private async Task<MarketLayout> GetLayoutAsync(Guid id, CancellationToken token)
        => await _layouts.GetActiveByIdAsync(id, token) ?? throw AppException.NotFound("Market layout was not found.", "LAYOUT_NOT_FOUND");
    private async Task EnsureLayoutEditableAsync(MarketLayout layout, CancellationToken cancellationToken)
    {
        if (!await _layouts.IsEditableDraftAsync(layout.Id, cancellationToken))
            throw AppException.Conflict(
                "Only a layout in a draft MarketMap can be edited.",
                "LAYOUT_NOT_EDITABLE_DRAFT");
    }
    private async Task<NightMarket> EnsureLayoutOwnershipAsync(MarketLayout layout, Guid? actorId, CancellationToken cancellationToken)
    {
        var market = await _nightMarkets.GetActiveByIdAsync(layout.NightMarketId, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.", "MARKET_NOT_FOUND");
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

    private static decimal SquaredDistance(LayoutNode first, LayoutNode second)
    {
        var dx = first.Xcoordinate - second.Xcoordinate;
        var dy = first.Ycoordinate - second.Ycoordinate;
        return dx * dx + dy * dy;
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

using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using ApplicationLayer.Services.MarketLayouts;
using ApplicationLayer.Services.Subscriptions;

namespace ApplicationLayer.Services.LayoutEdges;

public class LayoutEdgeService : ILayoutEdgeService
{
    private readonly ILayoutEdgeRepository _edges;
    private readonly ILayoutNodeRepository _nodes;
    private readonly IMarketLayoutRepository _layouts;
    private readonly INightMarketRepository _nightMarkets;
    private readonly IMapper _mapper;
    private readonly ISubscriptionEntitlementService _entitlements;
    public LayoutEdgeService(ILayoutEdgeRepository edges, ILayoutNodeRepository nodes, IMarketLayoutRepository layouts, INightMarketRepository nightMarkets, IMapper mapper, ISubscriptionEntitlementService entitlements)
        => (_edges, _nodes, _layouts, _nightMarkets, _mapper, _entitlements) = (edges, nodes, layouts, nightMarkets, mapper, entitlements);

    public async Task<ApiResponse<PaginationResp<LayoutEdgeResponse>>> GetAllAsync(Guid layoutId, PaginationReq request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        await EnsureLayoutAsync(layoutId, cancellationToken);
        await EnsureLayoutOwnershipOnlyAsync(layoutId, actorId, cancellationToken);
        var page = await _edges.GetPagedAsync(layoutId, request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<LayoutEdgeResponse>>.SuccessResponse(
            _mapper.MapPage<LayoutEdge, LayoutEdgeResponse>(page, request));
    }

    public async Task<ApiResponse<LayoutEdgeResponse>> GetAsync(Guid id, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var edge = await GetEdgeAsync(id, cancellationToken);
        await EnsureLayoutOwnershipOnlyAsync(edge.LayoutId, actorId, cancellationToken);
        return ApiResponse<LayoutEdgeResponse>.SuccessResponse(_mapper.Map<LayoutEdgeResponse>(edge));
    }

    public async Task<ApiResponse<LayoutEdgeResponse>> CreateAsync(Guid layoutId, CreateLayoutEdgeRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var edge = await BuildAsync(layoutId, request, null, cancellationToken, actorId);
        await _edges.AddAsync(edge);
        await BumpGraphRevisionAndSaveAsync(layoutId, cancellationToken);
        return ApiResponse<LayoutEdgeResponse>.SuccessResponse(_mapper.Map<LayoutEdgeResponse>(edge), "Layout edge created successfully.");
    }

    public async Task<ApiResponse<IReadOnlyCollection<LayoutEdgeResponse>>> CreateBatchAsync(Guid layoutId, IReadOnlyCollection<CreateLayoutEdgeRequest> requests, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        if (requests.Count == 0) throw AppException.BadRequest("At least one edge is required.");
        var edges = new List<LayoutEdge>();
        foreach (var request in requests) edges.Add(await BuildAsync(layoutId, request, null, cancellationToken, actorId));
        if (edges.GroupBy(x => new { A = x.FromNodeId, B = x.ToNodeId }).Any(x => x.Count() > 1))
            throw AppException.Conflict("The batch contains duplicate edges.");
        await _edges.AddRangeAsync(edges);
        await BumpGraphRevisionAndSaveAsync(layoutId, cancellationToken);
        return ApiResponse<IReadOnlyCollection<LayoutEdgeResponse>>.SuccessResponse(_mapper.Map<List<LayoutEdgeResponse>>(edges), "Layout edges created successfully.");
    }

    public async Task<ApiResponse<LayoutEdgeResponse>> UpdateAsync(Guid id, UpdateLayoutEdgeRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var edge = await GetEdgeAsync(id, cancellationToken);
        await EnsureLayoutEditableAsync(edge.LayoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(edge.LayoutId, actorId, cancellationToken);
        var replacement = await BuildAsync(edge.LayoutId, request, id, cancellationToken);
        edge.FromNodeId = replacement.FromNodeId; edge.ToNodeId = replacement.ToNodeId; edge.Distance = replacement.Distance;
        edge.IsBidirectional = request.IsBidirectional; edge.IsAccessible = request.IsAccessible; edge.UpdatedAt = DateTime.UtcNow;
        _edges.Update(edge);
        await BumpGraphRevisionAndSaveAsync(edge.LayoutId, cancellationToken);
        return ApiResponse<LayoutEdgeResponse>.SuccessResponse(_mapper.Map<LayoutEdgeResponse>(edge), "Layout edge updated successfully.");
    }

    public async Task<ApiResponse<LayoutEdgeResponse>> UpdateAccessibilityAsync(Guid id, UpdateAccessibilityRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var edge = await GetEdgeAsync(id, cancellationToken);
        await EnsureLayoutEditableAsync(edge.LayoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(edge.LayoutId, actorId, cancellationToken);
        edge.IsAccessible = request.IsAccessible; edge.UpdatedAt = DateTime.UtcNow;
        _edges.Update(edge);
        await BumpGraphRevisionAndSaveAsync(edge.LayoutId, cancellationToken);
        return ApiResponse<LayoutEdgeResponse>.SuccessResponse(_mapper.Map<LayoutEdgeResponse>(edge), "Edge accessibility updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var edge = await GetEdgeAsync(id, cancellationToken);
        await EnsureLayoutEditableAsync(edge.LayoutId, cancellationToken);
        await EnsureLayoutOwnershipAsync(edge.LayoutId, actorId, cancellationToken);
        edge.UpdatedAt = DateTime.UtcNow; _edges.Delete(edge);
        await BumpGraphRevisionAndSaveAsync(edge.LayoutId, cancellationToken);
        return ApiResponse<object>.SuccessResponse(new { edge.Id }, "Layout edge deleted successfully.");
    }

    private async Task<LayoutEdge> BuildAsync(Guid layoutId, CreateLayoutEdgeRequest request, Guid? excludeId, CancellationToken token, Guid? actorId = null)
    {
        await EnsureLayoutAsync(layoutId, token);
        await EnsureLayoutEditableAsync(layoutId, token);
        await EnsureLayoutOwnershipAsync(layoutId, actorId, token);
        if (request.FromNodeId == request.ToNodeId)
            throw AppException.BadRequest("An edge cannot connect a node to itself.");

        var from = await _nodes.GetActiveByIdAsync(request.FromNodeId, token);
        var to = await _nodes.GetActiveByIdAsync(request.ToNodeId, token);

        if (from is null || to is null || from.LayoutId != layoutId || to.LayoutId != layoutId)
            throw AppException.BadRequest("Both edge nodes must belong to the requested layout.");

        if (await _edges.ExistsAsync(layoutId, from.Id, to.Id, excludeId, token))
            throw AppException.Conflict("This edge already exists.");

        var requestedMetres = request.DistanceMeters ?? request.Distance;
        decimal distance;
        if (requestedMetres.HasValue)
        {
            distance = requestedMetres.Value;
        }
        else
        {
            var layout = await _layouts.GetActiveByIdAsync(layoutId, token)
                ?? throw AppException.NotFound("Market layout was not found.");
            var market = await _nightMarkets.GetActiveByIdAsync(layout.NightMarketId, token);
            var scale = LayoutPhysicalCalibration.TryResolve(layout, market);
            if (scale is null)
                throw AppException.BadRequest(
                    "Layout distance is not calibrated. Provide a physical distance in metres or calibrate the layout first.",
                    "LAYOUT_DISTANCE_UNCALIBRATED");
            distance = LayoutDistance.Between(from, to, scale);
        }
        if (distance <= 0)
            throw AppException.BadRequest("Edge distance must be greater than zero.");

        var now = DateTime.UtcNow;
        return new LayoutEdge { Id = Guid.NewGuid(), LayoutId = layoutId, FromNodeId = from.Id, ToNodeId = to.Id,
            Distance = distance, IsBidirectional = request.IsBidirectional, IsAccessible = request.IsAccessible,
            IsDeleted = false, CreatedAt = now, UpdatedAt = now };
    }

    private async Task EnsureLayoutAsync(Guid id, CancellationToken token)
    {
        if (await _layouts.GetActiveByIdAsync(id, token) is null) throw AppException.NotFound("Market layout was not found.");
    }
    private async Task EnsureLayoutEditableAsync(Guid layoutId, CancellationToken token)
    {
        var layout = await _layouts.GetActiveByIdAsync(layoutId, token)
            ?? throw AppException.NotFound("Market layout was not found.");
        if (layout.Status == DomainLayer.Enums.GeneralEnum.MarketLayoutStatus.Active)
            throw AppException.Conflict(
                "Deactivate the active layout before editing its map.",
                "LAYOUT_ACTIVE_EDIT_FORBIDDEN");
    }
    private async Task EnsureLayoutOwnershipOnlyAsync(Guid layoutId, Guid? actorId, CancellationToken token)
    {
        if (!actorId.HasValue) return;
        var layout = await _layouts.GetActiveByIdAsync(layoutId, token);
        if (layout is null) throw AppException.NotFound("Market layout was not found.");
        var market = await _nightMarkets.GetActiveByIdAsync(layout.NightMarketId, token);
        if (market is null || market.MarketOwnerId != actorId)
            throw AppException.Forbidden("You do not have permission to view this night market's layouts.");
    }
    private async Task EnsureLayoutOwnershipAsync(Guid layoutId, Guid? actorId, CancellationToken token)
    {
        if (!actorId.HasValue) return;
        var layout = await _layouts.GetActiveByIdAsync(layoutId, token);
        if (layout is null) throw AppException.NotFound("Market layout was not found.");
        var market = await _nightMarkets.GetActiveByIdAsync(layout.NightMarketId, token);
        if (market is null || market.MarketOwnerId != actorId)
            throw AppException.Forbidden("You do not have permission to manage this night market's layouts.");

        var hasSubscription = await _entitlements.HasActiveMarketSubscriptionAsync(actorId.Value);
        if (!hasSubscription)
            throw AppException.Forbidden("An active Market subscription is required to perform this action.", "MARKET_SUBSCRIPTION_REQUIRED");
    }
    private async Task BumpGraphRevisionAndSaveAsync(Guid layoutId, CancellationToken token)
    {
        var layout = await _layouts.GetActiveByIdAsync(layoutId, token)
            ?? throw AppException.NotFound("Market layout was not found.");
        layout.GraphRevision = checked(layout.GraphRevision + 1);
        layout.UpdatedAt = DateTime.UtcNow;
        _layouts.Update(layout);
        await _edges.SaveChangesAsync();
    }

    private async Task<LayoutEdge> GetEdgeAsync(Guid id, CancellationToken token)
        => await _edges.GetActiveByIdAsync(id, token) ?? throw AppException.NotFound("Layout edge was not found.");
}

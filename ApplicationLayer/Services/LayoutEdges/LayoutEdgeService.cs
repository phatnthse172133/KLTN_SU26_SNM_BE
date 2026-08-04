using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.LayoutEdges;

public class LayoutEdgeService : ILayoutEdgeService
{
    private readonly ILayoutEdgeRepository _edges;
    private readonly ILayoutNodeRepository _nodes;
    private readonly IMarketLayoutRepository _layouts;
    private readonly IMapper _mapper;
    public LayoutEdgeService(ILayoutEdgeRepository edges, ILayoutNodeRepository nodes, IMarketLayoutRepository layouts, IMapper mapper)
        => (_edges, _nodes, _layouts, _mapper) = (edges, nodes, layouts, mapper);

    public async Task<ApiResponse<PaginationResp<LayoutEdgeResponse>>> GetAllAsync(Guid layoutId, PaginationReq request, CancellationToken cancellationToken = default)
    {
        await EnsureLayoutAsync(layoutId, cancellationToken);
        var page = await _edges.GetPagedAsync(layoutId, request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<LayoutEdgeResponse>>.SuccessResponse(
            _mapper.MapPage<LayoutEdge, LayoutEdgeResponse>(page, request));
    }

    public async Task<ApiResponse<LayoutEdgeResponse>> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => ApiResponse<LayoutEdgeResponse>.SuccessResponse(_mapper.Map<LayoutEdgeResponse>(await GetEdgeAsync(id, cancellationToken)));

    public async Task<ApiResponse<LayoutEdgeResponse>> CreateAsync(Guid layoutId, CreateLayoutEdgeRequest request, CancellationToken cancellationToken = default)
    {
        var edge = await BuildAsync(layoutId, request, null, cancellationToken);
        await TouchGraphAsync(layoutId, cancellationToken);
        await _edges.AddAsync(edge); await _edges.SaveChangesAsync();
        return ApiResponse<LayoutEdgeResponse>.SuccessResponse(_mapper.Map<LayoutEdgeResponse>(edge), "Layout edge created successfully.");
    }

    public async Task<ApiResponse<IReadOnlyCollection<LayoutEdgeResponse>>> CreateBatchAsync(Guid layoutId, IReadOnlyCollection<CreateLayoutEdgeRequest> requests, CancellationToken cancellationToken = default)
    {
        if (requests.Count == 0) throw AppException.BadRequest("At least one edge is required.");
        var edges = new List<LayoutEdge>();
        foreach (var request in requests) edges.Add(await BuildAsync(layoutId, request, null, cancellationToken));
        if (edges.GroupBy(x => new { A = x.FromNodeId, B = x.ToNodeId }).Any(x => x.Count() > 1))
            throw AppException.Conflict("The batch contains duplicate edges.");
        await TouchGraphAsync(layoutId, cancellationToken);
        await _edges.AddRangeAsync(edges); await _edges.SaveChangesAsync();
        return ApiResponse<IReadOnlyCollection<LayoutEdgeResponse>>.SuccessResponse(_mapper.Map<List<LayoutEdgeResponse>>(edges), "Layout edges created successfully.");
    }

    public async Task<ApiResponse<LayoutEdgeResponse>> UpdateAsync(Guid id, UpdateLayoutEdgeRequest request, CancellationToken cancellationToken = default)
    {
        var edge = await GetEdgeAsync(id, cancellationToken);
        var replacement = await BuildAsync(edge.LayoutId, request, id, cancellationToken);
        edge.FromNodeId = replacement.FromNodeId; edge.ToNodeId = replacement.ToNodeId; edge.Distance = replacement.Distance;
        edge.IsBidirectional = request.IsBidirectional; edge.IsAccessible = request.IsAccessible; edge.UpdatedAt = DateTime.UtcNow;
        await TouchGraphAsync(edge.LayoutId, cancellationToken);
        _edges.Update(edge); await _edges.SaveChangesAsync();
        return ApiResponse<LayoutEdgeResponse>.SuccessResponse(_mapper.Map<LayoutEdgeResponse>(edge), "Layout edge updated successfully.");
    }

    public async Task<ApiResponse<LayoutEdgeResponse>> UpdateAccessibilityAsync(Guid id, UpdateAccessibilityRequest request, CancellationToken cancellationToken = default)
    {
        var edge = await GetEdgeAsync(id, cancellationToken);
        await TouchGraphAsync(edge.LayoutId, cancellationToken);
        edge.IsAccessible = request.IsAccessible; edge.UpdatedAt = DateTime.UtcNow;
        _edges.Update(edge); await _edges.SaveChangesAsync();
        return ApiResponse<LayoutEdgeResponse>.SuccessResponse(_mapper.Map<LayoutEdgeResponse>(edge), "Edge accessibility updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var edge = await GetEdgeAsync(id, cancellationToken);
        await TouchGraphAsync(edge.LayoutId, cancellationToken);
        edge.UpdatedAt = DateTime.UtcNow; _edges.Delete(edge); await _edges.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { edge.Id }, "Layout edge deleted successfully.");
    }

    private async Task<LayoutEdge> BuildAsync(Guid layoutId, CreateLayoutEdgeRequest request, Guid? excludeId, CancellationToken token)
    {
        var layout = await EnsureLayoutAsync(layoutId, token);
        EnsureEditable(layout);
        if (request.FromNodeId == request.ToNodeId) 
            throw AppException.BadRequest("An edge cannot connect a node to itself.");

        var from = await _nodes.GetActiveByIdAsync(request.FromNodeId, token);
        var to = await _nodes.GetActiveByIdAsync(request.ToNodeId, token);

        if (from is null || to is null || from.LayoutId != layoutId || to.LayoutId != layoutId)
            throw AppException.BadRequest("Both edge nodes must belong to the requested layout.");

        if (await _edges.ExistsAsync(layoutId, from.Id, to.Id, excludeId, token))
            throw AppException.Conflict("This edge already exists.");

        if (request.Distance.HasValue && request.DistanceMeters.HasValue && request.Distance.Value != request.DistanceMeters.Value)
            throw AppException.BadRequest("Distance and distanceMeters must match when both compatibility fields are supplied.", "EDGE_DISTANCE_CONFLICT");

        var suppliedDistanceMeters = request.DistanceMeters ?? request.Distance;
        decimal distance;
        if (suppliedDistanceMeters.HasValue)
        {
            distance = suppliedDistanceMeters.Value;
        }
        else
        {
            if (layout.DistanceCalibrationStatus != DistanceCalibrationStatus.Calibrated ||
                !layout.MetersPerLayoutUnit.HasValue || layout.MetersPerLayoutUnit.Value <= 0)
                throw AppException.BadRequest(
                    "Physical distanceMeters is required until the layout has a valid metres-per-layout-unit calibration.",
                    "LAYOUT_DISTANCE_UNCALIBRATED");

            var layoutDistance = (decimal)Math.Sqrt(Math.Pow((double)(from.Xcoordinate - to.Xcoordinate), 2) + Math.Pow((double)(from.Ycoordinate - to.Ycoordinate), 2));
            distance = decimal.Round(layoutDistance * layout.MetersPerLayoutUnit.Value, 2, MidpointRounding.AwayFromZero);
        }

        if (distance <= 0 || distance > 99_999_999.99m)
            throw AppException.BadRequest("Edge distanceMeters must be greater than zero and fit the supported physical-distance range.", "INVALID_EDGE_DISTANCE_METERS");

        var now = DateTime.UtcNow;
        return new LayoutEdge { Id = Guid.NewGuid(), LayoutId = layoutId, FromNodeId = from.Id, ToNodeId = to.Id,
            Distance = distance, IsBidirectional = request.IsBidirectional, IsAccessible = request.IsAccessible,
            IsDeleted = false, CreatedAt = now, UpdatedAt = now };
    }

    private async Task<MarketLayout> EnsureLayoutAsync(Guid id, CancellationToken token)
    {
        return await _layouts.GetActiveByIdAsync(id, token) ?? throw AppException.NotFound("Market layout was not found.");
    }
    private async Task<LayoutEdge> GetEdgeAsync(Guid id, CancellationToken token)
        => await _edges.GetActiveByIdAsync(id, token) ?? throw AppException.NotFound("Layout edge was not found.");
    private async Task TouchGraphAsync(Guid layoutId, CancellationToken token)
    {
        var layout = await EnsureLayoutAsync(layoutId, token);
        EnsureEditable(layout);
        layout.GraphRevision = checked(layout.GraphRevision + 1);
        layout.UpdatedAt = DateTime.UtcNow;
    }
    private static void EnsureEditable(MarketLayout layout)
    {
        if (layout.Status == MarketLayoutStatus.Active)
            throw AppException.Conflict("Clone the active layout to a draft before editing its graph.", "ACTIVE_LAYOUT_IMMUTABLE");
    }
}

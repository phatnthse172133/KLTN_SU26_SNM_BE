using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

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
        var (items, total) = await _edges.GetPagedAsync(layoutId, request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<LayoutEdgeResponse>>.SuccessResponse(new()
        {
            Items = _mapper.Map<List<LayoutEdgeResponse>>(items), Page = request.Page, PageSize = request.PageSize, Total = total
        });
    }

    public async Task<ApiResponse<LayoutEdgeResponse>> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => ApiResponse<LayoutEdgeResponse>.SuccessResponse(_mapper.Map<LayoutEdgeResponse>(await GetEdgeAsync(id, cancellationToken)));

    public async Task<ApiResponse<LayoutEdgeResponse>> CreateAsync(Guid layoutId, CreateLayoutEdgeRequest request, CancellationToken cancellationToken = default)
    {
        var edge = await BuildAsync(layoutId, request, null, cancellationToken);
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
        await _edges.AddRangeAsync(edges); await _edges.SaveChangesAsync();
        return ApiResponse<IReadOnlyCollection<LayoutEdgeResponse>>.SuccessResponse(_mapper.Map<List<LayoutEdgeResponse>>(edges), "Layout edges created successfully.");
    }

    public async Task<ApiResponse<LayoutEdgeResponse>> UpdateAsync(Guid id, UpdateLayoutEdgeRequest request, CancellationToken cancellationToken = default)
    {
        var edge = await GetEdgeAsync(id, cancellationToken);
        var replacement = await BuildAsync(edge.LayoutId, request, id, cancellationToken);
        edge.FromNodeId = replacement.FromNodeId; edge.ToNodeId = replacement.ToNodeId; edge.Distance = replacement.Distance;
        edge.IsBidirectional = request.IsBidirectional; edge.IsAccessible = request.IsAccessible; edge.UpdatedAt = DateTime.UtcNow;
        _edges.Update(edge); await _edges.SaveChangesAsync();
        return ApiResponse<LayoutEdgeResponse>.SuccessResponse(_mapper.Map<LayoutEdgeResponse>(edge), "Layout edge updated successfully.");
    }

    public async Task<ApiResponse<LayoutEdgeResponse>> UpdateAccessibilityAsync(Guid id, UpdateAccessibilityRequest request, CancellationToken cancellationToken = default)
    {
        var edge = await GetEdgeAsync(id, cancellationToken);
        edge.IsAccessible = request.IsAccessible; edge.UpdatedAt = DateTime.UtcNow;
        _edges.Update(edge); await _edges.SaveChangesAsync();
        return ApiResponse<LayoutEdgeResponse>.SuccessResponse(_mapper.Map<LayoutEdgeResponse>(edge), "Edge accessibility updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var edge = await GetEdgeAsync(id, cancellationToken);
        edge.UpdatedAt = DateTime.UtcNow; _edges.Delete(edge); await _edges.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { edge.Id }, "Layout edge deleted successfully.");
    }

    private async Task<LayoutEdge> BuildAsync(Guid layoutId, CreateLayoutEdgeRequest request, Guid? excludeId, CancellationToken token)
    {
        await EnsureLayoutAsync(layoutId, token);
        if (request.FromNodeId == request.ToNodeId) throw AppException.BadRequest("An edge cannot connect a node to itself.");
        var from = await _nodes.GetActiveByIdAsync(request.FromNodeId, token);
        var to = await _nodes.GetActiveByIdAsync(request.ToNodeId, token);
        if (from is null || to is null || from.LayoutId != layoutId || to.LayoutId != layoutId)
            throw AppException.BadRequest("Both edge nodes must belong to the requested layout.");
        if (await _edges.ExistsAsync(layoutId, from.Id, to.Id, excludeId, token))
            throw AppException.Conflict("This edge already exists.");
        var distance = request.Distance ?? (decimal)Math.Sqrt(Math.Pow((double)(from.Xcoordinate - to.Xcoordinate), 2) + Math.Pow((double)(from.Ycoordinate - to.Ycoordinate), 2));
        if (distance <= 0) throw AppException.BadRequest("Edge distance must be greater than zero.");
        var now = DateTime.UtcNow;
        return new LayoutEdge { Id = Guid.NewGuid(), LayoutId = layoutId, FromNodeId = from.Id, ToNodeId = to.Id,
            Distance = distance, IsBidirectional = request.IsBidirectional, IsAccessible = request.IsAccessible,
            IsDeleted = false, CreatedAt = now, UpdatedAt = now };
    }

    private async Task EnsureLayoutAsync(Guid id, CancellationToken token)
    {
        if (await _layouts.GetActiveByIdAsync(id, token) is null) throw AppException.NotFound("Market layout was not found.");
    }
    private async Task<LayoutEdge> GetEdgeAsync(Guid id, CancellationToken token)
        => await _edges.GetActiveByIdAsync(id, token) ?? throw AppException.NotFound("Layout edge was not found.");
}

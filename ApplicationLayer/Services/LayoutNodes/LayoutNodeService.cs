using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
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
    private readonly IMapper _mapper;

    public LayoutNodeService(ILayoutNodeRepository nodes, ILayoutEdgeRepository edges,
        IBoothLocationRepository locations, IMarketLayoutRepository layouts, IZoneRepository zones, IMapper mapper)
        => (_nodes, _edges, _locations, _layouts, _zones, _mapper) = (nodes, edges, locations, layouts, zones, mapper);

    public async Task<ApiResponse<PaginationResp<LayoutNodeResponse>>> GetAllAsync(Guid layoutId, MapListRequest request, CancellationToken cancellationToken = default)
    {
        await GetLayoutAsync(layoutId, cancellationToken);
        var (items, total) = await _nodes.GetPagedAsync(layoutId, request.Keyword, request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<LayoutNodeResponse>>.SuccessResponse(new()
        {
            Items = _mapper.Map<List<LayoutNodeResponse>>(items), Page = request.Page, PageSize = request.PageSize, Total = total
        });
    }

    public async Task<ApiResponse<LayoutNodeResponse>> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => ApiResponse<LayoutNodeResponse>.SuccessResponse(_mapper.Map<LayoutNodeResponse>(await GetNodeAsync(id, cancellationToken)));

    public async Task<ApiResponse<LayoutNodeResponse>> CreateAsync(Guid layoutId, CreateLayoutNodeRequest request, CancellationToken cancellationToken = default)
    {
        var layout = await GetLayoutAsync(layoutId, cancellationToken);
        ValidatePosition(layout, request.XCoordinate, request.YCoordinate);
        await ValidateZoneAsync(layout, request.ZoneId, cancellationToken);
        var node = _mapper.Map<LayoutNode>(request);
        var now = DateTime.UtcNow;
        node.Id = Guid.NewGuid(); node.LayoutId = layoutId; node.IsDeleted = false; node.CreatedAt = now; node.UpdatedAt = now;
        await _nodes.AddAsync(node); await _nodes.SaveChangesAsync();
        return ApiResponse<LayoutNodeResponse>.SuccessResponse(_mapper.Map<LayoutNodeResponse>(node), "Layout node created successfully.");
    }

    public async Task<ApiResponse<IReadOnlyCollection<LayoutNodeResponse>>> CreateBatchAsync(Guid layoutId, IReadOnlyCollection<CreateLayoutNodeRequest> requests, CancellationToken cancellationToken = default)
    {
        if (requests.Count == 0) throw AppException.BadRequest("At least one node is required.");
        var layout = await GetLayoutAsync(layoutId, cancellationToken);
        foreach (var request in requests) await ValidateZoneAsync(layout, request.ZoneId, cancellationToken);
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

    public async Task<ApiResponse<LayoutNodeResponse>> UpdateAsync(Guid id, UpdateLayoutNodeRequest request, CancellationToken cancellationToken = default)
    {
        var node = await GetNodeAsync(id, cancellationToken);
        var layout = await GetLayoutAsync(node.LayoutId, cancellationToken);
        ValidatePosition(layout, request.XCoordinate, request.YCoordinate);
        await ValidateZoneAsync(layout, request.ZoneId, cancellationToken);
        _mapper.Map(request, node); node.UpdatedAt = DateTime.UtcNow;
        _nodes.Update(node); await _nodes.SaveChangesAsync();
        return ApiResponse<LayoutNodeResponse>.SuccessResponse(_mapper.Map<LayoutNodeResponse>(node), "Layout node updated successfully.");
    }

    public async Task<ApiResponse<LayoutNodeResponse>> UpdatePositionAsync(Guid id, UpdateLayoutNodePositionRequest request, CancellationToken cancellationToken = default)
    {
        var node = await GetNodeAsync(id, cancellationToken);
        ValidatePosition(await GetLayoutAsync(node.LayoutId, cancellationToken), request.XCoordinate, request.YCoordinate);
        node.Xcoordinate = request.XCoordinate; node.Ycoordinate = request.YCoordinate; node.UpdatedAt = DateTime.UtcNow;
        _nodes.Update(node); await _nodes.SaveChangesAsync();
        return ApiResponse<LayoutNodeResponse>.SuccessResponse(_mapper.Map<LayoutNodeResponse>(node), "Node position updated successfully.");
    }

    public async Task<ApiResponse<LayoutNodeResponse>> UpdateAccessibilityAsync(Guid id, UpdateAccessibilityRequest request, CancellationToken cancellationToken = default)
    {
        var node = await GetNodeAsync(id, cancellationToken);
        node.IsAccessible = request.IsAccessible; node.UpdatedAt = DateTime.UtcNow;
        _nodes.Update(node); await _nodes.SaveChangesAsync();
        return ApiResponse<LayoutNodeResponse>.SuccessResponse(_mapper.Map<LayoutNodeResponse>(node), "Node accessibility updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var node = await GetNodeAsync(id, cancellationToken);
        if (await _locations.GetCurrentByNodeAsync(id, cancellationToken) is not null)
            throw AppException.Conflict("Release the booth location before deleting this node.");
        var edges = await _edges.GetByLayoutAsync(node.LayoutId, cancellationToken: cancellationToken);
        _edges.DeleteRange(edges.Where(x => x.FromNodeId == id || x.ToNodeId == id));
        _nodes.Delete(node); node.UpdatedAt = DateTime.UtcNow;
        await _nodes.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { node.Id }, "Layout node deleted successfully.");
    }

    private async Task<LayoutNode> GetNodeAsync(Guid id, CancellationToken token)
        => await _nodes.GetActiveByIdAsync(id, token) ?? throw AppException.NotFound("Layout node was not found.");
    private async Task<MarketLayout> GetLayoutAsync(Guid id, CancellationToken token)
        => await _layouts.GetActiveByIdAsync(id, token) ?? throw AppException.NotFound("Market layout was not found.");
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
}

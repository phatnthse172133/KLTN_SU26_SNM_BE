using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.LayoutEdges;

public interface ILayoutEdgeService
{
    Task<ApiResponse<PaginationResp<LayoutEdgeResponse>>> GetAllAsync(Guid layoutId, PaginationReq request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<LayoutEdgeResponse>> GetAsync(Guid id, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<LayoutEdgeResponse>> CreateAsync(Guid layoutId, CreateLayoutEdgeRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<IReadOnlyCollection<LayoutEdgeResponse>>> CreateBatchAsync(Guid layoutId, IReadOnlyCollection<CreateLayoutEdgeRequest> requests, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<LayoutEdgeResponse>> UpdateAsync(Guid id, UpdateLayoutEdgeRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<LayoutEdgeResponse>> UpdateAccessibilityAsync(Guid id, UpdateAccessibilityRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken = default, Guid? actorId = null);
}

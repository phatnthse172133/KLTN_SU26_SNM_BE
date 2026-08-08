using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.LayoutNodes;

public interface ILayoutNodeService
{
    Task<ApiResponse<PaginationResp<LayoutNodeResponse>>> GetAllAsync(Guid layoutId, MapListRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<LayoutNodeResponse>> GetAsync(Guid id, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<LayoutNodeResponse>> CreateAsync(Guid layoutId, CreateLayoutNodeRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<IReadOnlyCollection<LayoutNodeResponse>>> CreateBatchAsync(Guid layoutId, IReadOnlyCollection<CreateLayoutNodeRequest> requests, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<LayoutNodeResponse>> UpdateAsync(Guid id, UpdateLayoutNodeRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<LayoutNodeResponse>> UpdatePositionAsync(Guid id, UpdateLayoutNodePositionRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<LayoutNodeResponse>> UpdateAccessibilityAsync(Guid id, UpdateAccessibilityRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken = default, Guid? actorId = null);
}

using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.BoothLocations;

public interface IBoothLocationService
{
    Task<ApiResponse<PaginationResp<BoothLocationResponse>>> GetByLayoutAsync(Guid layoutId, Guid? zoneId, PaginationReq request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<LayoutNodeResponse>>> GetAvailableAsync(Guid layoutId, Guid? zoneId, PaginationReq request, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothLocationResponse>> GetByBoothAsync(Guid boothId, CancellationToken cancellationToken = default);
    Task<ApiResponse<NodeAvailabilityResponse>> GetAvailabilityAsync(Guid nodeId, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothLocationResponse>> AssignAsync(Guid boothId, AssignBoothLocationRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothLocationResponse>> MoveAsync(Guid boothId, AssignBoothLocationRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> ReleaseAsync(Guid boothId, Guid? layoutId = null, CancellationToken cancellationToken = default);
}

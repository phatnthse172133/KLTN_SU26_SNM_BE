using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.IndoorPositioning;

public interface IIndoorPositioningService
{
    Task<ApiResponse<IndoorPositionEstimateResponse>> SnapAsync(Guid layoutId, SnapIndoorPositionRequest request, CancellationToken token = default);
    Task<ApiResponse<VirtualOriginRouteResponse>> RouteFromSnappedPositionAsync(Guid layoutId, RouteFromSnappedPositionRequest request, CancellationToken token = default);
}

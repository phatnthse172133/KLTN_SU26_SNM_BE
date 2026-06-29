using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.MapNavigation;

public interface IMapNavigationService
{
    Task<ApiResponse<NightMarketMapResponse>> GetMapAsync(Guid nightMarketId, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyCollection<LayoutNodeResponse>>> GetStartingPointsAsync(Guid layoutId, CancellationToken cancellationToken = default);
    Task<ApiResponse<NearestNodeResponse>> FindNearestNodeAsync(Guid layoutId, NearestNodeRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<ShortestPathResponse>> FindRouteToBoothAsync(Guid layoutId, Guid fromNodeId, Guid boothId, CancellationToken cancellationToken = default);
}

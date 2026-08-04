using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.NavigationAnchors;

public interface INavigationAnchorService
{
    Task<ApiResponse<NavigationEntranceCollectionResponse>> GetEntrancesAsync(Guid marketId, Guid? targetBoothId = null, int? expectedLayoutVersion = null, int? expectedGraphRevision = null, CancellationToken token = default);
    Task<ApiResponse<NavigationEntranceCollectionResponse>> GetNearestEntrancesAsync(Guid marketId, double latitude, double longitude, Guid? targetBoothId = null, int? expectedLayoutVersion = null, int? expectedGraphRevision = null, CancellationToken token = default);
    Task<ApiResponse<NavigationAnchorAdminResponse>> CreateAsync(Guid layoutId, SaveNavigationAnchorRequest request, CancellationToken token = default);
    Task<ApiResponse<NavigationAnchorAdminResponse>> UpdateAsync(Guid anchorId, SaveNavigationAnchorRequest request, CancellationToken token = default);
    Task<ApiResponse<object>> DeleteAsync(Guid anchorId, CancellationToken token = default);
}

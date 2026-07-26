using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.NightMarkets;

public interface INightMarketService
{
    Task<ApiResponse<PaginationResp<NightMarketResponse>>> GetAllAsync(NightMarketListRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<NightMarketResponse>>> GetMineAsync(Guid marketOwnerId, CancellationToken cancellationToken = default);
    Task<ApiResponse<NightMarketResponse>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<NightMarketResponse>> CreateAsync(CreateNightMarketRequest request, Guid marketOwnerId, CancellationToken cancellationToken = default);
    Task<ApiResponse<NightMarketResponse>> UpdateAsync(Guid id, UpdateNightMarketRequest request, Guid? currentUserId, string currentUserRole, CancellationToken cancellationToken = default);
    Task<ApiResponse<NightMarketResponse>> UpdateGeographicLocationAsync(Guid id, UpdateNightMarketGeographicLocationRequest request, Guid? currentUserId, string currentUserRole, CancellationToken cancellationToken = default);
    Task<ApiResponse<NightMarketNavigationInfoResponse>> GetNavigationInfoAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteAsync(Guid id, Guid? currentUserId, string currentUserRole, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> GetDeletionImpactAsync(Guid id, Guid? currentUserId, string currentUserRole, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<NightMarketOptionDto>>> GetOptionsAsync(CancellationToken cancellationToken = default);
}

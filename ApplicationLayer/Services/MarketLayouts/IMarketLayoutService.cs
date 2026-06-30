using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.MarketLayouts;

public interface IMarketLayoutService
{
    Task<ApiResponse<PaginationResp<MarketLayoutResponse>>> GetAllAsync(Guid nightMarketId, MarketLayoutListRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketLayoutResponse>> GetByIdAsync(Guid layoutId, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketLayoutResponse>> CreateAsync(Guid nightMarketId, CreateMarketLayoutRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketLayoutResponse>> UpdateAsync(Guid layoutId, UpdateMarketLayoutRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketLayoutResponse>> UpdateImageAsync(Guid layoutId, UpdateMarketLayoutImageRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketLayoutEditorDataResponse>> GetEditorDataAsync(Guid layoutId, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketLayoutValidationResponse>> ValidateAsync(Guid layoutId, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketLayoutResponse>> ActivateAsync(Guid layoutId, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketLayoutResponse>> DeactivateAsync(Guid layoutId, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteAsync(Guid layoutId, CancellationToken cancellationToken = default);
}

using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.MarketLayouts;

public interface IMarketLayoutService
{
    Task<ApiResponse<PaginationResp<MarketLayoutResponse>>> GetAllAsync(Guid nightMarketId, MarketLayoutListRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<MarketLayoutResponse>> GetByIdAsync(Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<MarketLayoutResponse>> CreateAsync(Guid nightMarketId, CreateMarketLayoutRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<MarketLayoutResponse>> UpdateAsync(Guid layoutId, UpdateMarketLayoutRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<MarketLayoutResponse>> UpdateImageAsync(Guid layoutId, UpdateMarketLayoutImageRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<MarketLayoutResponse>> UpdateDimensionsAsync(Guid layoutId, UpdateMarketLayoutDimensionsRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<MarketLayoutEditorDataResponse>> GetEditorDataAsync(Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<object>> SaveGraphTransactionalAsync(Guid layoutId, SaveGraphRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<MarketLayoutValidationResponse>> ValidateAsync(Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<MarketLayoutResponse>> ActivateAsync(Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<MarketLayoutResponse>> DeactivateAsync(Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<object>> DeleteAsync(Guid layoutId, CancellationToken cancellationToken = default, Guid? actorId = null);

    // Grid Generation API
    Task<ApiResponse<GenerationPreviewResponse>> GenerationPreviewAsync(Guid layoutId, GenerateLayoutRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<object>> ApplyGenerationAsync(Guid layoutId, GenerateLayoutRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
}

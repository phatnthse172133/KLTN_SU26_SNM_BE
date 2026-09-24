using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.MarketMaps;

public interface IMarketMapService
{
    Task<ApiResponse<IReadOnlyCollection<MarketMapSummaryResponse>>> GetAllAsync(
        Guid nightMarketId, Guid actorId, CancellationToken cancellationToken = default);

    Task<ApiResponse<MarketMapDetailResponse>> GetByIdAsync(
        Guid nightMarketId, Guid marketMapId, Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyCollection<EligibleMarketLayoutResponse>>> GetEligibleLayoutsAsync(
        Guid nightMarketId, Guid actorId, CancellationToken cancellationToken = default);

    Task<ApiResponse<MarketMapDetailResponse>> CreateDraftAsync(
        Guid nightMarketId, CreateMarketMapDraftRequest request, Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MarketMapDetailResponse>> ArrangeAsync(
        Guid nightMarketId, Guid marketMapId, ArrangeMarketMapRequest request, Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MarketMapDetailResponse>> SetDefaultLayoutAsync(
        Guid nightMarketId, Guid marketMapId, SetMarketMapDefaultLayoutRequest request, Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MarketMapPreviewResponse>> GetPreviewAsync(
        Guid nightMarketId, Guid marketMapId, Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MarketMapValidationResponse>> ValidateAsync(
        Guid nightMarketId, Guid marketMapId, Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MarketMapDetailResponse>> ActivateAsync(
        Guid nightMarketId, Guid marketMapId, Guid actorId,
        CancellationToken cancellationToken = default);
}

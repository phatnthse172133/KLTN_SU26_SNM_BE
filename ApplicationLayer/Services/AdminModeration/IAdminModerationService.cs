using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.AdminModeration;

public interface IAdminModerationService
{
    // Night Market
    Task<ApiResponse<PaginationResp<MarketModerationOverviewResponse>>> GetMarketsAsync(
        AdminMarketModerationQueryRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<MarketModerationDetailResponse>> GetMarketDetailAsync(
        Guid marketId, CancellationToken cancellationToken = default);

    Task<ApiResponse<ModerationActionResponse>> ChangeMarketModerationStatusAsync(
        Guid adminId, string adminName, Guid marketId, ChangeModerationStatusRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<PaginationResp<ModerationActionHistoryResponse>>> GetMarketHistoryAsync(
        Guid marketId, int page, int pageSize, CancellationToken cancellationToken = default);

    // Booth
    Task<ApiResponse<PaginationResp<BoothModerationOverviewResponse>>> GetBoothsAsync(
        AdminBoothModerationQueryRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<BoothModerationDetailResponse>> GetBoothDetailAsync(
        Guid boothId, CancellationToken cancellationToken = default);

    Task<ApiResponse<ModerationActionResponse>> ChangeBoothStatusAsync(
        Guid adminId, string adminName, Guid boothId, ChangeModerationStatusRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<PaginationResp<ModerationActionHistoryResponse>>> GetBoothHistoryAsync(
        Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default);
}

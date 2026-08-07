using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Booths;

public interface IBoothService
{
    Task<ApiResponse<BoothResponse>> GetMyBoothAsync(
        Guid ownerId, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothResponse>> UpdateMyBoothAsync(
        Guid ownerId, UpdateMyBoothRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothResponse>> TogglePauseMyBoothAsync(
        Guid ownerId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<BoothResponse>>> GetAllAsync(PaginationReq pagination, Guid? nightMarketId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothResponse>> UpdateByAdminAsync(Guid boothId, AdminUpdateBoothRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothNavigationInfoResponse>> GetBoothNavigationInfoAsync(Guid boothId, CancellationToken cancellationToken = default);

    Task<ApiResponse<PaginationResp<MarketOwnerBoothResponse>>> GetByMarketOwnerAsync(
        Guid marketOwnerId, Guid? marketId, string? keyword, string? status,
        PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketOwnerBoothResponse>> CreateByMarketOwnerAsync(
        Guid marketOwnerId, Guid marketId, MarketOwnerCreateBoothRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketOwnerBoothResponse>> UpdateByMarketOwnerAsync(
        Guid marketOwnerId, Guid boothId, MarketOwnerUpdateBoothRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketOwnerBoothResponse>> ChangeStatusByMarketOwnerAsync(
        Guid marketOwnerId, Guid boothId, MarketOwnerChangeBoothStatusRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketOwnerBoothResponse>> CreateAndAssignBoothAsync(
        Guid marketOwnerId, Guid marketId, Guid layoutId, Guid nodeId, MarketOwnerCreateBoothRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketOwnerBoothResponse>> AssignBoothAsync(
        Guid marketOwnerId, Guid marketId, Guid layoutId, Guid nodeId, Guid boothId, CancellationToken cancellationToken = default);
    Task<ApiResponse<MarketOwnerBoothResponse>> ReleaseSlotBoothAsync(
        Guid marketOwnerId, Guid marketId, Guid layoutId, Guid nodeId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteByMarketOwnerAsync(
        Guid marketOwnerId, Guid boothId, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<BoothOwnerSearchResponse>>> SearchBoothOwnersAsync(
        string? keyword, CancellationToken cancellationToken = default);
}

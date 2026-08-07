using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.BoothRegistrations;

public interface IBoothRegistrationService
{
    Task<ApiResponse<BoothRegistrationResponse>> CreateAsync(Guid ownerId, CreateBoothRegistrationRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<BoothRegistrationResponse>>> GetMineAsync(
        Guid ownerId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<BoothRegistrationResponse>>> GetPendingAsync(PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<BoothRegistrationResponse>>> GetByMarketOwnerAsync(
        Guid marketOwnerId, Guid? marketId, string? status, string? keyword, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<RegistrationCountsResponse>> GetCountsByMarketOwnerAsync(
        Guid marketOwnerId, Guid? marketId, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothRegistrationResponse>> ReviewAsync(Guid registrationId, ReviewBoothRegistrationRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
}

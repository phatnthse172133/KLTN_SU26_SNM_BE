using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.BoothRegistrations;

public interface IBoothRegistrationService
{
    Task<ApiResponse<BoothRegistrationResponse>> CreateAsync(Guid ownerId, CreateBoothRegistrationRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> GetMineAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<BoothRegistrationResponse>>> GetPendingAsync(PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothRegistrationResponse>> ReviewAsync(Guid registrationId, ReviewBoothRegistrationRequest request, CancellationToken cancellationToken = default);
}

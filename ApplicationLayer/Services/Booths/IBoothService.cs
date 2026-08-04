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
    Task<ApiResponse<PaginationResp<BoothResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothResponse>> UpdateByAdminAsync(Guid boothId, AdminUpdateBoothRequest request, CancellationToken cancellationToken = default);
}

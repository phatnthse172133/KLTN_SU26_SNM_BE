using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Booths;

public interface IBoothService
{
    Task<ApiResponse<PaginationResp<BoothResponse>>> GetMyBoothsAsync(
        Guid ownerId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothResponse>> UpdateMyBoothAsync(Guid ownerId, Guid boothId, UpdateMyBoothRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<BoothResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothResponse>> UpdateByAdminAsync(Guid boothId, AdminUpdateBoothRequest request, CancellationToken cancellationToken = default);
}

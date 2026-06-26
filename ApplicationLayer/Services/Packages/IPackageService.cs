using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Packages;

public interface IPackageService
{
    Task<ApiResponse<PaginationResp<PackageResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PackageResponse>> GetByIdAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PackageResponse>> CreateAsync(CreatePackageRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PackageResponse>> UpdateAsync(Guid packageId, UpdatePackageRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteAsync(Guid packageId, CancellationToken cancellationToken = default);
}

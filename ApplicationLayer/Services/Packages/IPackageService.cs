using ApplicationLayer.DTOs;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Packages;

public interface IPackageService
{
    Task<ApiResponse<PaginationResp<PackageResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PackageResponse>> GetByIdAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PackageResponse>> CreateAsync(CreatePackageRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PackageResponse>> UpdateAsync(Guid packageId, UpdatePackageRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<PackageResponse>>> GetActiveByTypeAsync(PackageType type, CancellationToken cancellationToken = default);
    Task<ApiResponse<PackageResponse>> GetActiveByIdAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> StopSellingAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> ResumeSellingAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PackageResponse>> UploadImageAsync(Guid packageId, Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteImageAsync(Guid packageId, CancellationToken cancellationToken = default);
    ApiResponse<List<PackageTemplateResponse>> GetTemplates();


}

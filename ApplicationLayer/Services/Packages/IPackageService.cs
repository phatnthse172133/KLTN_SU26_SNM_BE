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
    ApiResponse<List<PackageTemplateResponse>> GetTemplates();

    Task<ApiResponse<PackagePolicyResponse>> CreatePolicyVersionAsync(Guid packageId, CreatePackagePolicyRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> ActivatePolicyVersionAsync(Guid packageId, Guid policyId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PackagePolicyResponse>> GetActivePolicyAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<PackagePolicyResponse>>> GetPoliciesAsync(Guid packageId, CancellationToken cancellationToken = default);

}

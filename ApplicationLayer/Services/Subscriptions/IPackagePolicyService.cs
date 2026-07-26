using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using DomainLayer.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.Subscriptions
{
    public interface IPackagePolicyService
    {
        Task<ApiResponse<PackagePolicyResponse>> CreatePolicyAsync(Guid packageId, CreatePackagePolicyRequest request, CancellationToken ct = default);
        Task<ApiResponse<List<PackagePolicyResponse>>> GetPolicyVersionsAsync(Guid packageId, CancellationToken ct = default);
        Task<ApiResponse<PackagePolicyResponse>> GetActivePolicyAsync(Guid packageId, CancellationToken ct = default);
        Task<ApiResponse<PackagePolicyResponse>> ActivatePolicyAsync(Guid packageId, Guid policyId, CancellationToken ct = default);
    }
}

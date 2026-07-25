using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Subscriptions
{
    public class PackagePolicyService : IPackagePolicyService
    {
        private readonly ISubscriptionRepository _repo;

        public PackagePolicyService(ISubscriptionRepository repo)
        {
            _repo = repo;
        }

        public async Task<ApiResponse<PackagePolicyResponse>> CreatePolicyAsync(Guid packageId, CreatePackagePolicyRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Version))
                throw AppException.BadRequest("Policy version is required.", "POLICY_VERSION_REQUIRED");
            if (string.IsNullOrWhiteSpace(request.Title))
                throw AppException.BadRequest("Policy title is required.", "POLICY_TITLE_REQUIRED");
            if (string.IsNullOrWhiteSpace(request.ContentJson))
                throw AppException.BadRequest("Policy content is required.", "POLICY_CONTENT_REQUIRED");
            if (request.EffectiveFrom == default)
                throw AppException.BadRequest("Effective date is required.", "POLICY_EFFECTIVE_DATE_REQUIRED");

            var package = await _repo.GetPackageByIdAsync(packageId, ct);
            if (package is null)
                throw AppException.NotFound("Package not found.");
            if (package.Status != PackageStatus.Active)
                throw AppException.BadRequest("Cannot create policy for an inactive or deleted package.", "PACKAGE_NOT_ACTIVE");

            var existingVersions = await _repo.GetPackagePoliciesByPackageAsync(packageId, ct);
            if (existingVersions.Any(p => p.Version == request.Version))
                throw AppException.Conflict($"Policy version '{request.Version}' already exists for this package.", "POLICY_VERSION_DUPLICATE");

            var policy = new PackagePolicy
            {
                PackageId = packageId,
                Version = request.Version,
                Title = request.Title,
                ContentJson = request.ContentJson,
                ContentMarkdown = request.ContentMarkdown,
                EffectiveFrom = request.EffectiveFrom.UtcDateTime,
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repo.AddPackagePolicyAsync(policy, ct);
            await _repo.SaveChangesAsync(ct);

            return ApiResponse<PackagePolicyResponse>.SuccessResponse(MapToResponse(policy), "Policy version created successfully.");
        }

        public async Task<ApiResponse<List<PackagePolicyResponse>>> GetPolicyVersionsAsync(Guid packageId, CancellationToken ct = default)
        {
            var policies = await _repo.GetPackagePoliciesByPackageAsync(packageId, ct);
            return ApiResponse<List<PackagePolicyResponse>>.SuccessResponse(
                policies.Select(MapToResponse).ToList());
        }

        public async Task<ApiResponse<PackagePolicyResponse>> GetActivePolicyAsync(Guid packageId, CancellationToken ct = default)
        {
            var policy = await _repo.GetActivePackagePolicyAsync(packageId, ct);
            if (policy is null)
                throw AppException.NotFound("No active policy found for this package.", "POLICY_NOT_FOUND");

            return ApiResponse<PackagePolicyResponse>.SuccessResponse(MapToResponse(policy));
        }

        public async Task<ApiResponse<PackagePolicyResponse>> ActivatePolicyAsync(Guid packageId, Guid policyId, CancellationToken ct = default)
        {
            var policy = await _repo.GetPackagePolicyByIdAsync(policyId, ct);
            if (policy is null || policy.PackageId != packageId)
                throw AppException.NotFound("Policy not found for this package.");

            if (policy.IsDeleted)
                throw AppException.BadRequest("Cannot activate a deleted policy.", "POLICY_DELETED");

            var package = await _repo.GetPackageByIdAsync(packageId, ct);
            if (package is null || package.Status != PackageStatus.Active)
                throw AppException.BadRequest("Cannot activate policy for an inactive or deleted package.", "PACKAGE_NOT_ACTIVE");

            if (policy.EffectiveFrom > DateTime.UtcNow)
                throw AppException.BadRequest("A policy cannot be activated before its effective date.", "POLICY_NOT_EFFECTIVE");

            await _repo.BeginTransactionAsync(ct);
            try
            {
                await _repo.DeactivateActivePolicyAsync(packageId, ct);
                var activatedRows = await _repo.ActivatePolicyAsync(policyId, ct);
                if (activatedRows != 1)
                    throw AppException.Conflict("The policy could not be activated. Please refresh and try again.", "POLICY_ACTIVATION_CONFLICT");

                await _repo.SaveChangesAsync(ct);
                await _repo.CommitTransactionAsync(ct);
            }
            catch
            {
                await _repo.RollbackTransactionAsync(ct);
                throw;
            }

            policy.IsActive = true;
            return ApiResponse<PackagePolicyResponse>.SuccessResponse(MapToResponse(policy), "Policy activated successfully.");
        }

        private static PackagePolicyResponse MapToResponse(PackagePolicy policy)
        {
            return new PackagePolicyResponse
            {
                Id = policy.Id,
                PackageId = policy.PackageId,
                Version = policy.Version,
                Title = policy.Title,
                ContentJson = policy.ContentJson,
                ContentMarkdown = policy.ContentMarkdown,
                EffectiveFrom = policy.EffectiveFrom,
                IsActive = policy.IsActive,
                CreatedAt = policy.CreatedAt
            };
        }
    }
}

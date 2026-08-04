using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
            var title = request.Title?.Trim() ?? string.Empty;
            if (title.Length < 5 || title.Length > 200)
                throw AppException.BadRequest("Policy title must be between 5 and 200 characters.", "POLICY_TITLE_LENGTH");

            if (request.Terms is null || request.Terms.Count == 0)
                throw AppException.BadRequest("At least one policy term is required.", "POLICY_TERMS_REQUIRED");

            var terms = request.Terms
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .ToList();

            if (terms.Count == 0)
                throw AppException.BadRequest("At least one policy term is required.", "POLICY_TERMS_REQUIRED");

            if (terms.Distinct(StringComparer.OrdinalIgnoreCase).Count() != terms.Count)
                throw AppException.BadRequest("Duplicate policy terms are not allowed.", "POLICY_DUPLICATE_TERMS");

            if (request.EffectiveFrom == default)
                throw AppException.BadRequest("Effective date is required.", "POLICY_EFFECTIVE_DATE_REQUIRED");

            var package = await _repo.GetPackageByIdAsync(packageId, ct);
            if (package is null)
                throw AppException.NotFound("Package not found.");
            if (package.Status != PackageStatus.Active)
                throw AppException.BadRequest("Cannot create policy for an inactive or deleted package.", "PACKAGE_NOT_ACTIVE");

            var existingPolicies = await _repo.GetPackagePoliciesByPackageAsync(packageId, ct);
            var nextVersion = GenerateNextVersion(existingPolicies.Select(p => p.Version).ToList());

            var contentJson = JsonSerializer.Serialize(new { terms });
            var contentMarkdown = string.Join("\n\n", terms);

            var policy = new PackagePolicy
            {
                PackageId = packageId,
                Version = nextVersion,
                Title = title,
                ContentJson = contentJson,
                ContentMarkdown = contentMarkdown,
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
                DisplayVersion = $"Version {policy.Version}",
                Title = policy.Title,
                Terms = ParseTerms(policy.ContentJson, policy.ContentMarkdown),
                EffectiveFrom = policy.EffectiveFrom,
                IsActive = policy.IsActive,
                CreatedAt = policy.CreatedAt
            };
        }

        private static List<string> ParseTerms(string contentJson, string? contentMarkdown)
        {
            if (!string.IsNullOrWhiteSpace(contentJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(contentJson);
                    if (doc.RootElement.TryGetProperty("terms", out var termsEl) && termsEl.ValueKind == JsonValueKind.Array)
                    {
                        return termsEl.EnumerateArray()
                            .Select(t => t.GetString() ?? string.Empty)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();
                    }
                    if (doc.RootElement.TryGetProperty("Terms", out var termsEl2) && termsEl2.ValueKind == JsonValueKind.Array)
                    {
                        return termsEl2.EnumerateArray()
                            .Select(t => t.GetString() ?? string.Empty)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();
                    }
                }
                catch { }
            }
            if (!string.IsNullOrWhiteSpace(contentMarkdown))
            {
                return contentMarkdown
                    .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }
            return new List<string>();
        }

        private static string GenerateNextVersion(List<string> existingVersions)
        {
            if (existingVersions.Count == 0)
                return "1.0";

            var parsed = existingVersions
                .Select(v => ParseVersion(v))
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (parsed.Count == 0)
                return "1.0";

            var maxVersion = parsed.Max();
            return $"{maxVersion.major}.{maxVersion.minor + 1}";
        }

        private static (int major, int minor)? ParseVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return null;

            var v = version.Trim();
            if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                v = v[1..];

            var parts = v.Split('.');
            if (parts.Length < 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
                return null;

            return (major, minor);
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Exceptions;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Subscriptions;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Moq;
using Xunit;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class PackagePolicyServiceTests
{
    private readonly Mock<ISubscriptionRepository> _repo = new();
    private readonly PackagePolicyService _service;
    private readonly Guid _packageId = Guid.NewGuid();

    public PackagePolicyServiceTests()
    {
        _service = new PackagePolicyService(_repo.Object);
    }

    [Fact]
    public async Task ActivatePolicyAsync_FuturePolicy_RejectsBeforeChangingActivePolicy()
    {
        var policy = CreatePolicy(DateTime.UtcNow.AddDays(1));
        ArrangePolicyAndPackage(policy);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.ActivatePolicyAsync(_packageId, policy.Id));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("POLICY_NOT_EFFECTIVE", ex.ErrorCode);
        _repo.Verify(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.DeactivateActivePolicyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivatePolicyAsync_EffectivePolicy_ChangesPolicyAtomically()
    {
        var policy = CreatePolicy(DateTime.UtcNow.AddMinutes(-1));
        ArrangePolicyAndPackage(policy);
        _repo.Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.DeactivateActivePolicyAsync(_packageId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _repo.Setup(r => r.ActivatePolicyAsync(policy.Id, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _service.ActivatePolicyAsync(_packageId, policy.Id);

        Assert.True(result.Success);
        Assert.True(result.Data!.IsActive);
        _repo.Verify(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.DeactivateActivePolicyAsync(_packageId, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.ActivatePolicyAsync(policy.Id, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePolicyAsync_ValidRequest_CreatesInactiveVersion()
    {
        _repo.Setup(r => r.GetPackageByIdAsync(_packageId, It.IsAny<CancellationToken>())).ReturnsAsync(new Package
        {
            Id = _packageId,
            PackageName = "Market Pro",
            Status = PackageStatus.Active,
            Type = PackageType.Market
        });
        _repo.Setup(r => r.GetPackagePoliciesByPackageAsync(_packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Collections.Generic.List<PackagePolicy>());
        PackagePolicy? persisted = null;
        _repo.Setup(r => r.AddPackagePolicyAsync(It.IsAny<PackagePolicy>(), It.IsAny<CancellationToken>()))
            .Callback<PackagePolicy, CancellationToken>((policy, _) => persisted = policy)
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _service.CreatePolicyAsync(_packageId, new CreatePackagePolicyRequest
        {
            Title = "Subscription terms",
            Terms = new System.Collections.Generic.List<string> { "No refunds after activation" },
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(1)
        });

        Assert.True(result.Success);
        Assert.NotNull(persisted);
        Assert.False(persisted!.IsActive);
        Assert.Equal(_packageId, persisted.PackageId);
    }

    [Fact]
    public async Task CreatePolicyAsync_AutoIncrementsVersion_WhenPolicyExists()
    {
        var existing = CreatePolicy(DateTime.UtcNow);
        ArrangePolicyAndPackage(existing);
        _repo.Setup(r => r.GetPackagePoliciesByPackageAsync(_packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Collections.Generic.List<PackagePolicy> { existing });
        _repo.Setup(r => r.AddPackagePolicyAsync(It.IsAny<PackagePolicy>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _service.CreatePolicyAsync(_packageId,
            new CreatePackagePolicyRequest
            {
                Title = "Second policy",
                Terms = new System.Collections.Generic.List<string> { "Updated terms" },
                EffectiveFrom = DateTimeOffset.UtcNow
            });

        Assert.True(result.Success);
        Assert.NotEqual(existing.Version, result.Data?.Version);
    }

    [Fact]
    public async Task ActivatePolicyAsync_ConcurrentConflict_RollsBack()
    {
        var policy = CreatePolicy(DateTime.UtcNow.AddMinutes(-1));
        ArrangePolicyAndPackage(policy);
        _repo.Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.DeactivateActivePolicyAsync(_packageId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _repo.Setup(r => r.ActivatePolicyAsync(policy.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _repo.Setup(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.ActivatePolicyAsync(_packageId, policy.Id));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("POLICY_ACTIVATION_CONFLICT", ex.ErrorCode);
        _repo.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePolicyAsync_TitleTooShort_Rejects()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreatePolicyAsync(_packageId, new CreatePackagePolicyRequest
            {
                Title = "Hi",
                Terms = new System.Collections.Generic.List<string> { "Valid term" },
                EffectiveFrom = DateTimeOffset.UtcNow
            }));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("POLICY_TITLE_LENGTH", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePolicyAsync_TitleTooLong_Rejects()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreatePolicyAsync(_packageId, new CreatePackagePolicyRequest
            {
                Title = new string('A', 201),
                Terms = new System.Collections.Generic.List<string> { "Valid term" },
                EffectiveFrom = DateTimeOffset.UtcNow
            }));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("POLICY_TITLE_LENGTH", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePolicyAsync_WhitespaceOnlyTerms_Rejects()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreatePolicyAsync(_packageId, new CreatePackagePolicyRequest
            {
                Title = "Valid title",
                Terms = new System.Collections.Generic.List<string> { "   ", "  " },
                EffectiveFrom = DateTimeOffset.UtcNow
            }));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("POLICY_TERMS_REQUIRED", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePolicyAsync_DuplicateTermsCaseInsensitive_Rejects()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreatePolicyAsync(_packageId, new CreatePackagePolicyRequest
            {
                Title = "Valid title",
                Terms = new System.Collections.Generic.List<string> { "No refunds", "no refunds" },
                EffectiveFrom = DateTimeOffset.UtcNow
            }));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("POLICY_DUPLICATE_TERMS", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePolicyAsync_InactivePackage_Rejects()
    {
        _repo.Setup(r => r.GetPackageByIdAsync(_packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Package
            {
                Id = _packageId,
                PackageName = "Inactive Package",
                Status = PackageStatus.Inactive,
                Type = PackageType.Market
            });

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreatePolicyAsync(_packageId, new CreatePackagePolicyRequest
            {
                Title = "Valid title",
                Terms = new System.Collections.Generic.List<string> { "Valid term" },
                EffectiveFrom = DateTimeOffset.UtcNow
            }));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("PACKAGE_NOT_ACTIVE", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePolicyAsync_TrimsTitleAndTerms()
    {
        _repo.Setup(r => r.GetPackageByIdAsync(_packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Package
            {
                Id = _packageId,
                PackageName = "Market Pro",
                Status = PackageStatus.Active,
                Type = PackageType.Market
            });
        _repo.Setup(r => r.GetPackagePoliciesByPackageAsync(_packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Collections.Generic.List<PackagePolicy>());
        PackagePolicy? persisted = null;
        _repo.Setup(r => r.AddPackagePolicyAsync(It.IsAny<PackagePolicy>(), It.IsAny<CancellationToken>()))
            .Callback<PackagePolicy, CancellationToken>((policy, _) => persisted = policy)
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _service.CreatePolicyAsync(_packageId, new CreatePackagePolicyRequest
        {
            Title = "  Spaced Title  ",
            Terms = new System.Collections.Generic.List<string> { "  Trim me  " },
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(1)
        });

        Assert.True(result.Success);
        Assert.Equal("Spaced Title", persisted!.Title);
        Assert.Contains("Trim me", persisted.ContentJson);
    }

    private PackagePolicy CreatePolicy(DateTime effectiveFrom) => new()
    {
        Id = Guid.NewGuid(),
        PackageId = _packageId,
        Version = "2026.07",
        Title = "Subscription policy",
        ContentJson = "{\"terms\":[]}",
        EffectiveFrom = effectiveFrom,
        IsActive = false
    };

    private void ArrangePolicyAndPackage(PackagePolicy policy)
    {
        _repo.Setup(r => r.GetPackagePolicyByIdAsync(policy.Id, It.IsAny<CancellationToken>())).ReturnsAsync(policy);
        _repo.Setup(r => r.GetPackageByIdAsync(_packageId, It.IsAny<CancellationToken>())).ReturnsAsync(new Package
        {
            Id = _packageId,
            PackageName = "Market Pro",
            Status = PackageStatus.Active,
            Type = PackageType.Market
        });
    }
}

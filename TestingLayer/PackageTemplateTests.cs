using ApplicationLayer.DTOs;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Packages;
using ApplicationLayer.Services.Storage;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class PackageTemplateTests
{
    private readonly Mock<IGenericRepository<Package>> _mockPackages;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IPackagePriceRepository> _mockPackagePrices;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IFileStorageService> _mockFileStorage;
    private readonly Mock<ILogger<PackageService>> _mockLogger;
    private readonly PackageService _service;

    public PackageTemplateTests()
    {
        _mockPackages = new Mock<IGenericRepository<Package>>();
        _mockMapper = new Mock<IMapper>();
        _mockPackagePrices = new Mock<IPackagePriceRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockFileStorage = new Mock<IFileStorageService>();
        _mockLogger = new Mock<ILogger<PackageService>>();
        _service = new PackageService(_mockPackages.Object, _mockPackagePrices.Object, _mockUnitOfWork.Object, _mockMapper.Object, _mockFileStorage.Object, _mockLogger.Object);

        _mockPackages.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Package, bool>>>()))
            .ReturnsAsync(false);
    }

    [Fact]
    public void ResolveTemplate_MarketBasic_ReturnsCorrectEntitlements()
    {
        var (json, type, isFree) = PackageTemplateHelper.ResolveTemplate("MARKET_BASIC");

        Assert.Equal(PackageType.Market, type);
        Assert.False(isFree);
        var ent = EntitlementHelper.DeserializeMarketStrict(json);
        Assert.Equal(1, ent.MaxMarkets);
        Assert.Equal(30, ent.MaxSlotsPerMarket);
        Assert.Equal(1, ent.MaxLayoutsPerMarket);
        Assert.False(ent.ZoneManagement);
        Assert.False(ent.AdvancedBoothApproval);
        Assert.False(ent.AdvancedComplaint);
        Assert.False(ent.AdvancedReports);
        Assert.False(ent.AiInsights);
        Assert.False(ent.ExportReports);
    }

    [Fact]
    public void ResolveTemplate_MarketPro_ReturnsCorrectEntitlements()
    {
        var (json, type, isFree) = PackageTemplateHelper.ResolveTemplate("MARKET_PRO");

        Assert.Equal(PackageType.Market, type);
        Assert.False(isFree);
        var ent = EntitlementHelper.DeserializeMarketStrict(json);
        Assert.Equal(3, ent.MaxMarkets);
        Assert.Equal(150, ent.MaxSlotsPerMarket);
        Assert.Equal(5, ent.MaxLayoutsPerMarket);
        Assert.True(ent.ZoneManagement);
        Assert.True(ent.AdvancedBoothApproval);
        Assert.True(ent.AdvancedComplaint);
        Assert.True(ent.AdvancedReports);
        Assert.True(ent.AiInsights);
        Assert.True(ent.ExportReports);
    }

    [Fact]
    public void ResolveTemplate_BoothFree_ReturnsCorrectEntitlements()
    {
        var (json, type, isFree) = PackageTemplateHelper.ResolveTemplate("BOOTH_FREE");

        Assert.Equal(PackageType.Booth, type);
        Assert.True(isFree);
        var ent = EntitlementHelper.DeserializeBoothStrict(json);
        Assert.False(ent.Promotion);
        Assert.False(ent.AdvancedAnalytics);
        Assert.Equal(1, ent.RecommendationPriority);
        Assert.False(ent.FeaturedBooth);
        Assert.False(ent.FeaturedFood);
    }

    [Fact]
    public void ResolveTemplate_BoothGrowth_ReturnsCorrectEntitlements()
    {
        var (json, type, isFree) = PackageTemplateHelper.ResolveTemplate("BOOTH_GROWTH");

        Assert.Equal(PackageType.Booth, type);
        Assert.False(isFree);
        var ent = EntitlementHelper.DeserializeBoothStrict(json);
        Assert.True(ent.Promotion);
        Assert.True(ent.AdvancedAnalytics);
        Assert.Equal(1.2, ent.RecommendationPriority);
    }

    [Fact]
    public void ResolveTemplate_BoothFeatured_ReturnsCorrectEntitlements()
    {
        var (json, type, isFree) = PackageTemplateHelper.ResolveTemplate("BOOTH_FEATURED");

        Assert.Equal(PackageType.Booth, type);
        Assert.False(isFree);
        var ent = EntitlementHelper.DeserializeBoothStrict(json);
        Assert.True(ent.FeaturedBooth);
        Assert.True(ent.FeaturedFood);
        Assert.Equal(1.5, ent.RecommendationPriority);
    }

    [Fact]
    public void ResolveTemplate_InvalidCode_Throws400()
    {
        var ex = Assert.Throws<AppException>(() => PackageTemplateHelper.ResolveTemplate("INVALID_CODE"));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("INVALID_PACKAGE_TEMPLATE", ex.ErrorCode);
    }

    [Fact]
    public void ResolveTemplate_NullCode_Throws400()
    {
        var ex = Assert.Throws<AppException>(() => PackageTemplateHelper.ResolveTemplate(null));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void IsValidTemplate_ValidCodes_ReturnsTrue()
    {
        Assert.True(PackageTemplateHelper.IsValidTemplate("MARKET_BASIC"));
        Assert.True(PackageTemplateHelper.IsValidTemplate("market_pro"));
        Assert.True(PackageTemplateHelper.IsValidTemplate("BOOTH_FREE"));
        Assert.True(PackageTemplateHelper.IsValidTemplate("Booth_Growth"));
        Assert.True(PackageTemplateHelper.IsValidTemplate("BOOTH_FEATURED"));
    }

    [Fact]
    public void IsValidTemplate_InvalidCode_ReturnsFalse()
    {
        Assert.False(PackageTemplateHelper.IsValidTemplate("INVALID"));
        Assert.False(PackageTemplateHelper.IsValidTemplate(""));
        Assert.False(PackageTemplateHelper.IsValidTemplate(null));
    }

    [Fact]
    public async Task Create_MarketBasicTemplate_Succeeds()
    {
        var request = new CreatePackageRequest
        {
            PackageName = "Market Basic",
            TemplateCode = "MARKET_BASIC",
            Price = 500000,
            DurationDays = 30,
            Status = PackageStatus.Active
        };

        var result = await _service.CreateAsync(request);

        Assert.Equal("Package created successfully.", result.Message);
        _mockPackages.Verify(r => r.AddAsync(It.Is<Package>(p =>
            p.Code == "MARKET_BASIC" &&
            p.Type == PackageType.Market &&
            p.Price == 500000)), Times.Once);
        _mockPackages.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Create_BoothFreeTemplate_ZeroPrice_Succeeds()
    {
        var request = new CreatePackageRequest
        {
            PackageName = "Booth Free",
            TemplateCode = "BOOTH_FREE",
            Price = 0,
            DurationDays = 30,
            Status = PackageStatus.Active
        };

        var result = await _service.CreateAsync(request);

        Assert.Equal("Package created successfully.", result.Message);
    }

    [Fact]
    public async Task Create_MarketTemplate_ZeroPrice_Throws400()
    {
        var request = new CreatePackageRequest
        {
            PackageName = "Market Basic Free",
            TemplateCode = "MARKET_BASIC",
            Price = 0,
            DurationDays = 30,
            Status = PackageStatus.Active
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(request));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Create_BoothFreeTemplate_NonZeroPrice_Throws400()
    {
        var request = new CreatePackageRequest
        {
            PackageName = "Booth Free Paid",
            TemplateCode = "BOOTH_FREE",
            Price = 100000,
            DurationDays = 30,
            Status = PackageStatus.Active
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(request));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Create_NoTemplateCode_Throws400()
    {
        var request = new CreatePackageRequest
        {
            PackageName = "No Template",
            TemplateCode = null,
            Price = 500000,
            DurationDays = 30,
            Status = PackageStatus.Active
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(request));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidTemplateCode_Throws400()
    {
        var request = new CreatePackageRequest
        {
            PackageName = "Invalid Template",
            TemplateCode = "FAKE_PACKAGE",
            Price = 500000,
            DurationDays = 30,
            Status = PackageStatus.Active
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(request));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("INVALID_PACKAGE_TEMPLATE", ex.ErrorCode);
    }

    [Fact]
    public async Task Create_DuplicatePackageName_ReturnsSpecificConflictCode()
    {
        _mockPackages
            .Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Package, bool>>>() ))
            .ReturnsAsync(true);

        var request = new CreatePackageRequest
        {
            PackageName = "Market Basic",
            TemplateCode = "MARKET_BASIC",
            Price = 500000,
            DurationDays = 30,
            Status = PackageStatus.Active
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(request));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("PACKAGE_NAME_CONFLICT", ex.ErrorCode);
    }

    [Fact]
    public async Task Create_DuplicatePackageCode_ReturnsSpecificConflictCode()
    {
        _mockPackages
            .SetupSequence(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Package, bool>>>() ))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        var request = new CreatePackageRequest
        {
            PackageName = "Another Market Basic",
            TemplateCode = "MARKET_BASIC",
            Price = 500000,
            DurationDays = 30,
            Status = PackageStatus.Active
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(request));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("PACKAGE_CODE_CONFLICT", ex.ErrorCode);
    }

    [Fact]
    public async Task Update_DoesNotAllowEntitlementsChange()
    {
        var packageId = Guid.NewGuid();
        var existing = new Package
        {
            Id = packageId,
            PackageName = "Market Basic",
            Code = "MARKET_BASIC",
            Price = 500000,
            DurationDays = 30,
            Type = PackageType.Market,
            Entitlements = EntitlementHelper.SerializeMarket(MarketEntitlements.Basic),
            Status = PackageStatus.Active
        };

        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync(existing);

        var request = new UpdatePackageRequest
        {
            PackageName = "Market Basic Pro",
            Price = 600000,
            DurationDays = 60,
            Status = PackageStatus.Active
        };

        var result = await _service.UpdateAsync(packageId, request);

        Assert.Equal("Market Basic Pro", existing.PackageName);
        Assert.Equal(600000, existing.Price);
        Assert.Equal(60, existing.DurationDays);
        Assert.Equal(EntitlementHelper.SerializeMarket(MarketEntitlements.Basic), existing.Entitlements);
    }

    [Fact]
    public void GetTemplates_ReturnsAllFiveTemplates()
    {
        var result = _service.GetTemplates();

        Assert.Equal(5, result.Data.Count);
        Assert.Contains(result.Data, t => t.Code == "MARKET_BASIC");
        Assert.Contains(result.Data, t => t.Code == "MARKET_PRO");
        Assert.Contains(result.Data, t => t.Code == "BOOTH_FREE");
        Assert.Contains(result.Data, t => t.Code == "BOOTH_GROWTH");
        Assert.Contains(result.Data, t => t.Code == "BOOTH_FEATURED");
    }

    [Fact]
    public void GetTemplates_MarketBasic_HasCorrectFeatures()
    {
        var result = _service.GetTemplates();
        var basic = result.Data.First(t => t.Code == "MARKET_BASIC");

        Assert.Equal(PackageType.Market, basic.PackageType);
        Assert.False(basic.IsFree);
        Assert.True(basic.Features.Count > 0);
    }

    [Fact]
    public void GetTemplates_MarketPro_HasMoreFeaturesThanBasic()
    {
        var result = _service.GetTemplates();
        var basic = result.Data.First(t => t.Code == "MARKET_BASIC");
        var pro = result.Data.First(t => t.Code == "MARKET_PRO");

        Assert.True(pro.Features.Count > basic.Features.Count);
        Assert.True(pro.IsFree == false);
    }
    [Fact]
    public async Task UpdateAsync_RemovePromotion_MissingId_ThrowsBadRequest()
    {
        var packageId = Guid.NewGuid();
        var existing = new Package { Id = packageId, PackageName = "Test", DurationDays = 30, Price = 100 };
        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync(existing);
        var request = new UpdatePackageRequest { PackageName = "Test", DurationDays = 30, Price = 100, PromotionAction = "Remove", Promotion = new PackagePromotionRequest() };

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.UpdateAsync(packageId, request));
        Assert.Equal("PROMOTION_ID_REQUIRED", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_RemovePromotion_ValidId_RemovesCorrectly()
    {
        var packageId = Guid.NewGuid();
        var promoId = Guid.NewGuid();
        var existing = new Package { Id = packageId, PackageName = "Test", DurationDays = 30, Price = 100 };
        var promo = new PackagePrice { Id = promoId, PackageId = packageId, DurationDays = 30 };
        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync(existing);
        _mockPackagePrices.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PackagePrice, bool>>>())).ReturnsAsync(promo);
        var request = new UpdatePackageRequest { PackageName = "Test", DurationDays = 30, Price = 100, PromotionAction = "Remove", Promotion = new PackagePromotionRequest { Id = promoId } };

        await _service.UpdateAsync(packageId, request);

        _mockPackagePrices.Verify(r => r.Delete(promo), Times.Once);
        _mockPackagePrices.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_UpsertPromotion_MissingId_CreatesNewRecord()
    {
        var packageId = Guid.NewGuid();
        var existing = new Package { Id = packageId, PackageName = "Test", DurationDays = 30, Price = 100 };
        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync(existing);
        var request = new UpdatePackageRequest { PackageName = "Test", DurationDays = 30, Price = 100, PromotionAction = "Upsert", Promotion = new PackagePromotionRequest { Price = 80, StartDate = DateTime.UtcNow.AddDays(1), EndDate = DateTime.UtcNow.AddDays(10) } };
        _mockPackagePrices.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PackagePrice, bool>>>())).ReturnsAsync(new List<PackagePrice>());

        await _service.UpdateAsync(packageId, request);

        _mockPackagePrices.Verify(r => r.AddAsync(It.Is<PackagePrice>(p => p.Price == 80 && p.PackageId == packageId)), Times.Once);
        _mockPackagePrices.Verify(r => r.Update(It.IsAny<PackagePrice>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_UpsertPromotion_ValidId_UpdatesExistingRecord()
    {
        var packageId = Guid.NewGuid();
        var promoId = Guid.NewGuid();
        var promo = new PackagePrice { Id = promoId, PackageId = packageId, Price = 100, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(5) };
        var existing = new Package { Id = packageId, PackageName = "Test", DurationDays = 30, Price = 100 };
        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync(existing);
        _mockPackagePrices.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PackagePrice, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<PackagePrice, bool>> predicate) => new[] { promo }.FirstOrDefault(predicate.Compile()));
        var request = new UpdatePackageRequest { PackageName = "Test", DurationDays = 30, Price = 100, PromotionAction = "Upsert", Promotion = new PackagePromotionRequest { Id = promoId, Price = 80, StartDate = DateTime.UtcNow.AddDays(1), EndDate = DateTime.UtcNow.AddDays(10) } };
        _mockPackagePrices.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PackagePrice, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<PackagePrice, bool>> predicate) => new[] { promo }.Where(predicate.Compile()).ToList());

        await _service.UpdateAsync(packageId, request);

        _mockPackagePrices.Verify(r => r.AddAsync(It.IsAny<PackagePrice>()), Times.Never);
        _mockPackagePrices.Verify(r => r.Update(It.Is<PackagePrice>(p => p.Price == 80 && p.Id == promoId)), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_RollbackOnFailure()
    {
        var packageId = Guid.NewGuid();
        var existing = new Package { Id = packageId, PackageName = "Test", DurationDays = 30, Price = 100 };
        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync(existing);
        _mockPackagePrices.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new Exception("Database error"));

        var request = new UpdatePackageRequest { PackageName = "Test", DurationDays = 30, Price = 100, PromotionAction = "Remove", Promotion = new PackagePromotionRequest { Id = Guid.NewGuid() } };
        _mockPackagePrices.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PackagePrice, bool>>>())).ReturnsAsync(new PackagePrice());

        await Assert.ThrowsAsync<Exception>(() => _service.UpdateAsync(packageId, request));

        _mockUnitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("MARKET_BASIC", 8)]
    [InlineData("MARKET_PRO", 13)]
    [InlineData("BOOTH_FREE", 8)]
    [InlineData("BOOTH_GROWTH", 7)]
    [InlineData("BOOTH_FEATURED", 5)]
    public void GetFeatures_ReturnsExpectedFeaturesCount(string code, int expectedCount)
    {
        var features = PackageTemplateHelper.GetFeatures(code);
        Assert.Equal(expectedCount, features.Count);
    }

}

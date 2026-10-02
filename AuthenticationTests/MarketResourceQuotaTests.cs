using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Subscriptions;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepositories;
using DomainLayer.InterfaceRepository;
using Moq;
using Xunit;
using static DomainLayer.Enums.GeneralEnum;

namespace AuthenticationTests;

public sealed class MarketResourceQuotaTests
{
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _mapId = Guid.NewGuid();
    private readonly Guid _layoutId = Guid.NewGuid();
    private readonly Mock<ISubscriptionEntitlementService> _entitlements = new();
    private readonly Mock<IMarketLayoutRepository> _layouts = new();
    private readonly Mock<ILayoutNodeRepository> _nodes = new();
    private readonly Mock<IMarketMapRepository> _maps = new();

    private MarketResourceQuotaService Create(bool standalone)
    {
        _entitlements.Setup(x => x.GetMaxLayoutsPerMarketAsync(_owner)).ReturnsAsync(5);
        _entitlements.Setup(x => x.GetMaxSlotsPerMarketAsync(_owner)).ReturnsAsync(100);
        _maps.Setup(x => x.GetManagementDetailAsync(_mapId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketMap { Id = _mapId, Status = MarketMapStatus.Draft,
                Name = standalone ? MarketMap.LegacyDraftName : "Combined draft" });
        return new(_entitlements.Object, _layouts.Object, _nodes.Object, _maps.Object);
    }

    [Fact]
    public async Task FullStandaloneLibraryRejectsSixthSavedLayout()
    {
        var service = Create(true);
        _layouts.Setup(x => x.CountQuotaRelevantLayoutsAsync(_mapId, It.IsAny<CancellationToken>())).ReturnsAsync(5);
        await Assert.ThrowsAsync<AppException>(() => service.EnsureCanAddLayoutsAsync(_owner, _mapId, 1));
        _layouts.Verify(x => x.CountQuotaRelevantLayoutsAsync(_mapId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CombinedDraftStillRejectsSixthArea()
    {
        var service = Create(false);
        _layouts.Setup(x => x.CountQuotaRelevantLayoutsAsync(_mapId, It.IsAny<CancellationToken>())).ReturnsAsync(5);
        await Assert.ThrowsAsync<AppException>(() => service.EnsureCanAddLayoutsAsync(_owner, _mapId, 1));
    }

    [Fact]
    public async Task GeneratingStandaloneLayoutDoesNotAddSlotsFromOtherSourceVersions()
    {
        var service = Create(true);
        _nodes.Setup(x => x.CountBoothSlotsByMarketMapAsync(_mapId, _layoutId, It.IsAny<CancellationToken>())).ReturnsAsync(300);
        Assert.Null(await service.GetBoothSlotCapacityErrorAsync(_owner, _mapId, _layoutId, 100));
        Assert.NotNull(await service.GetBoothSlotCapacityErrorAsync(_owner, _mapId, _layoutId, 101));
    }

    [Fact]
    public async Task CombinedDraftCountsSlotsAcrossAllAreas()
    {
        var service = Create(false);
        _nodes.Setup(x => x.CountBoothSlotsByMarketMapAsync(_mapId, _layoutId, It.IsAny<CancellationToken>())).ReturnsAsync(60);
        Assert.Null(await service.GetBoothSlotCapacityErrorAsync(_owner, _mapId, _layoutId, 40));
        Assert.NotNull(await service.GetBoothSlotCapacityErrorAsync(_owner, _mapId, _layoutId, 41));
    }
}

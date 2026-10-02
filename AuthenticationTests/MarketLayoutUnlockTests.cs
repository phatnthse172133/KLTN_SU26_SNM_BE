using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.MarketLayouts;
using ApplicationLayer.Services.Realtime;
using ApplicationLayer.Services.Subscriptions;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepositories;
using DomainLayer.InterfaceRepository;
using Moq;
using Xunit;
using static DomainLayer.Enums.GeneralEnum;

namespace AuthenticationTests;

public sealed class MarketLayoutUnlockTests
{
    [Fact]
    public async Task UnlockReopensSameMapAndLayoutWithoutCloningOrAllocatingQuota()
    {
        var owner = Guid.NewGuid();
        var market = new NightMarket { Id = Guid.NewGuid(), MarketOwnerId = owner };
        var map = new MarketMap { Id = Guid.NewGuid(), NightMarketId = market.Id, Name = "Published map", Status = MarketMapStatus.Active, PublishedAt = DateTime.UtcNow };
        var layout = new MarketLayout { Id = Guid.NewGuid(), NightMarketId = market.Id, MarketMapId = map.Id, LayoutName = "Map", Status = MarketLayoutStatus.Active, Version = 4 };
        var assignment = new BoothLocation { Id = Guid.NewGuid(), LayoutId = layout.Id, BoothId = Guid.NewGuid() };
        layout.BoothLocations.Add(assignment);
        map.MarketLayouts.Add(layout);
        var layouts = new Mock<IMarketLayoutRepository>();
        layouts.Setup(x => x.GetActiveByIdAsync(layout.Id, It.IsAny<CancellationToken>())).ReturnsAsync(layout);
        var maps = new Mock<IMarketMapRepository>();
        maps.Setup(x => x.GetManagementDetailForUpdateAsync(map.Id, It.IsAny<CancellationToken>())).ReturnsAsync(map);
        var markets = new Mock<INightMarketRepository>();
        markets.Setup(x => x.GetActiveByIdAsync(market.Id, It.IsAny<CancellationToken>())).ReturnsAsync(market);
        var mapper = new Mock<IMapper>();
        mapper.Setup(x => x.Map<MarketLayoutResponse>(layout)).Returns(new MarketLayoutResponse { Id = layout.Id });
        var quota = new Mock<IMarketResourceQuotaService>(MockBehavior.Strict);
        var unit = new Mock<IUnitOfWork>();
        var service = new MarketLayoutService(layouts.Object, maps.Object, Mock.Of<IZoneRepository>(), markets.Object, mapper.Object,
            Mock.Of<ILayoutGraphValidationService>(), Mock.Of<ISubscriptionEntitlementService>(), quota.Object,
            Mock.Of<ILayoutGeneratorService>(), unit.Object, Mock.Of<IRealtimeEventPublisher>());

        var response = await service.DeactivateAsync(layout.Id, actorId: owner);

        Assert.Equal(layout.Id, response.Data!.Id);
        Assert.Equal(MarketLayoutStatus.Draft, layout.Status);
        Assert.Equal(MarketMapStatus.Draft, map.Status);
        Assert.Null(map.PublishedAt);
        Assert.Equal(4, layout.Version);
        Assert.Same(assignment, Assert.Single(layout.BoothLocations));
        Assert.Null(assignment.ReleasedAt);
        Assert.Empty(quota.Invocations);
        Assert.DoesNotContain(layouts.Invocations, x => x.Method.Name == "CloneToDraftAsync");
        unit.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
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

namespace LayoutAcceptanceTests;

public class FullMarketDimensionsTests
{
    [Theory]
    [InlineData(390, 600)]
    [InlineData(1130, 1140)]
    [InlineData(1, 1)]
    [InlineData(50000, 50000)]
    public async Task DimensionsUsesMarketNotLegacyClientCanvas(int width, int height)
    {
        var f = new Fixture();
        var result = await f.Service.UpdateDimensionsAsync(f.Layout.Id,
            new UpdateMarketLayoutDimensionsRequest { Width = width, Height = height }, actorId: f.Owner);
        Assert.NotNull(result.Data);
        f.AssertPreserved();
        Assert.Equal(1130, f.Layout.Width);
        Assert.Equal(1140, f.Layout.Height);
        Assert.Equal(113, f.Layout.MarketWidthMeters);
        Assert.Equal(114, f.Layout.MarketLengthMeters);
        Assert.Equal(0, f.Layout.OffsetXMeters);
        f.Layouts.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ImageUploadCannotRestoreOldPixelDimensions()
    {
        var f = new Fixture();
        await f.Service.UpdateImageAsync(f.Layout.Id, new UpdateMarketLayoutImageRequest
            { Width = 390, Height = 600, LayoutImageUrl = "/uploads/layout.png" }, actorId: f.Owner);
        Assert.Equal(1130, f.Layout.Width);
        Assert.Equal(1140, f.Layout.Height);
        Assert.Equal("/uploads/layout.png", f.Layout.LayoutImageUrl);
        f.AssertPreserved();
    }

    [Fact]
    public async Task MarketShrinkDoesNotSaveOrDeleteExistingGraph()
    {
        var f = new Fixture();
        f.Market.BoundaryWidthMeters = 20;
        await Assert.ThrowsAsync<AppException>(() => f.Service.UpdateDimensionsAsync(f.Layout.Id,
            new UpdateMarketLayoutDimensionsRequest { Width = 200, Height = 1140 }, actorId: f.Owner));
        f.Layouts.Verify(x => x.SaveChangesAsync(), Times.Never);
        f.AssertPreserved();
    }

    [Fact]
    public async Task LockedLayoutStillRejectsEditsWithoutSaving()
    {
        var f = new Fixture();
        f.Layouts.Setup(x => x.IsEditableDraftAsync(f.Layout.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await Assert.ThrowsAsync<AppException>(() => f.Service.UpdateDimensionsAsync(f.Layout.Id,
            new UpdateMarketLayoutDimensionsRequest { Width = 390, Height = 600 }, actorId: f.Owner));
        f.Layouts.Verify(x => x.SaveChangesAsync(), Times.Never);
        Assert.Equal(390, f.Layout.Width);
    }

    [Fact]
    public async Task OtherOwnerCannotUpdateDimensions()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<AppException>(() => f.Service.UpdateDimensionsAsync(f.Layout.Id,
            new UpdateMarketLayoutDimensionsRequest { Width = 390, Height = 600 }, actorId: Guid.NewGuid()));
        f.Layouts.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    private sealed class Fixture
    {
        public Guid Owner { get; } = Guid.NewGuid();
        public NightMarket Market { get; }
        public MarketLayout Layout { get; }
        public Mock<IMarketLayoutRepository> Layouts { get; } = new();
        public MarketLayoutService Service { get; }
        private readonly LayoutNode node = new() { Id = Guid.NewGuid(), Xcoordinate = 220, Ycoordinate = 150 };
        private readonly LayoutBlock block = new() { Id = Guid.NewGuid(), X = 200, Y = 80, Width = 180, Height = 300 };
        private readonly BoothLocation assignment = new() { Id = Guid.NewGuid(), BoothId = Guid.NewGuid() };
        public Fixture()
        {
            Market = new NightMarket { Id = Guid.NewGuid(), MarketOwnerId = Owner, BoundaryWidthMeters = 113, BoundaryHeightMeters = 114 };
            Layout = new MarketLayout { Id = Guid.NewGuid(), NightMarketId = Market.Id, Width = 390, Height = 600,
                PixelsPerMeter = 10, MarketWidthMeters = 39, MarketLengthMeters = 60, OffsetXMeters = 39, Status = MarketLayoutStatus.Draft };
            Layout.LayoutNodes.Add(node);
            Layout.BoothLocations.Add(assignment);
            Layouts.Setup(x => x.GetActiveByIdAsync(Layout.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Layout);
            Layouts.Setup(x => x.IsEditableDraftAsync(Layout.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Layouts.Setup(x => x.GetNodesByLayoutIdAsync(Layout.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<LayoutNode> { node });
            Layouts.Setup(x => x.GetBlocksByLayoutIdAsync(Layout.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<LayoutBlock> { block });
            var markets = new Mock<INightMarketRepository>();
            markets.Setup(x => x.GetActiveByIdAsync(Market.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Market);
            var mapper = new Mock<IMapper>();
            mapper.Setup(x => x.Map<MarketLayoutResponse>(Layout)).Returns(new MarketLayoutResponse { Id = Layout.Id });
            var entitlement = new Mock<ISubscriptionEntitlementService>();
            entitlement.Setup(x => x.HasActiveMarketSubscriptionAsync(Owner)).ReturnsAsync(true);
            Service = new MarketLayoutService(Layouts.Object, Mock.Of<IMarketMapRepository>(), Mock.Of<IZoneRepository>(), markets.Object,
                mapper.Object, Mock.Of<ILayoutGraphValidationService>(), entitlement.Object, Mock.Of<IMarketResourceQuotaService>(),
                Mock.Of<ILayoutGeneratorService>(), Mock.Of<IUnitOfWork>(), Mock.Of<IRealtimeEventPublisher>());
        }
        public void AssertPreserved()
        {
            Assert.Same(node, Assert.Single(Layout.LayoutNodes));
            Assert.Equal(220, node.Xcoordinate);
            Assert.Equal(150, node.Ycoordinate);
            Assert.Equal(200, block.X);
            Assert.Equal(80, block.Y);
            Assert.Same(assignment, Assert.Single(Layout.BoothLocations));
            Assert.Null(assignment.ReleasedAt);
        }
    }
}

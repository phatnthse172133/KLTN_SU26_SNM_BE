using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.MarketLayouts;
using ApplicationLayer.Services.MarketMaps;
using ApplicationLayer.Services.Subscriptions;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepositories;
using DomainLayer.InterfaceRepository;
using Moq;
using Xunit;
using static DomainLayer.Enums.GeneralEnum;

namespace AuthenticationTests;

public sealed class MarketMapCompositionTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid MarketId = Guid.NewGuid();

    private readonly Mock<IMarketMapRepository> _maps = new();
    private readonly Mock<IMarketLayoutRepository> _layouts = new();
    private readonly Mock<INightMarketRepository> _markets = new();
    private readonly Mock<ISubscriptionEntitlementService> _entitlements = new();
    private readonly Mock<IMarketResourceQuotaService> _quota = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILayoutGraphValidationService> _graph = new();
    private readonly Mock<IZoneRepository> _zones = new();
    private readonly Mock<ILayoutNavigationAnchorRepository> _anchors = new();
    private readonly Mock<IBoothLocationRepository> _locations = new();
    private readonly NightMarket _market = new()
    {
        Id = MarketId,
        MarketOwnerId = OwnerId,
        BoundaryWidthMeters = 100,
        BoundaryHeightMeters = 50
    };

    public MarketMapCompositionTests()
    {
        _markets.Setup(repo => repo.GetActiveByIdAsync(MarketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_market);
        _entitlements.Setup(service => service.HasActiveMarketSubscriptionAsync(OwnerId)).ReturnsAsync(true);
        _entitlements.Setup(service => service.GetMaxLayoutsPerMarketAsync(OwnerId)).ReturnsAsync(5);
        _entitlements.Setup(service => service.GetMaxSlotsPerMarketAsync(OwnerId)).ReturnsAsync(100);
        _quota.Setup(service => service.AssessMapCompositionAsync(
                OwnerId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, int layouts, int slots, CancellationToken _) =>
                new MarketMapCompositionQuotaAssessment(layouts, 5, slots, 100));
        _graph.Setup(service => service.ValidateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketLayoutValidationResponse());
        _layouts.Setup(repo => repo.GetBlocksByLayoutIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LayoutBlock>());
        _layouts.Setup(repo => repo.GetBlocksByLayoutIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LayoutBlock>());
        _zones.Setup(repo => repo.GetActiveByNightMarketIdAsync(MarketId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Zone>());
        _anchors.Setup(repo => repo.GetByLayoutAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LayoutNavigationAnchor>());
        _locations.Setup(repo => repo.GetDuplicateBoothIdsByMarketMapAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
    }

    private MarketMapService CreateService() => new(
        _maps.Object, _layouts.Object, _markets.Object, _entitlements.Object, _quota.Object,
        _unitOfWork.Object, _graph.Object, _zones.Object, _anchors.Object, _locations.Object);

    private static MarketMap Map(string name, MarketMapStatus status, int version) => new()
    {
        Id = Guid.NewGuid(), NightMarketId = MarketId, Name = name, Status = status, Version = version
    };

    private static MarketLayout Layout(
        MarketMap map, string section, MarketLayoutStatus status, double widthMeters, int slots,
        bool gate = true, bool isDefault = false, Guid? basedOn = null)
    {
        var layout = new MarketLayout
        {
            Id = Guid.NewGuid(), NightMarketId = MarketId, MarketMapId = map.Id, MarketMap = map,
            LayoutName = $"{section} v1", SectionCode = section, SectionName = section, Version = 1,
            Width = 200, Height = 100, MarketWidthMeters = widthMeters, MarketLengthMeters = 10,
            Status = status, IsDefaultView = isDefault, BasedOnLayoutId = basedOn
        };
        for (var index = 0; index < slots; index++)
            layout.LayoutNodes.Add(new LayoutNode { Id = Guid.NewGuid(), LayoutId = layout.Id, NodeType = LayoutNodeType.BoothSlot });
        if (gate)
            layout.LayoutNodes.Add(new LayoutNode { Id = Guid.NewGuid(), LayoutId = layout.Id, NodeType = LayoutNodeType.Entrance, Xcoordinate = 1, Ycoordinate = 1 });
        map.MarketLayouts.Add(layout);
        return layout;
    }

    [Fact]
    public async Task CompositionSources_reports_selectability_usage_and_package_limits()
    {
        var legacy = Map(MarketMap.LegacyDraftName, MarketMapStatus.Draft, 1);
        var active = Map("Published", MarketMapStatus.Active, 2);
        var draft = Map("Combined draft", MarketMapStatus.Draft, 3);
        var standalone = Layout(legacy, "A", MarketLayoutStatus.Draft, 20, 3);
        var published = Layout(active, "B", MarketLayoutStatus.Active, 20, 4);
        var clone = Layout(draft, "B", MarketLayoutStatus.Draft, 20, 4, basedOn: published.Id);
        var empty = Layout(legacy, "C", MarketLayoutStatus.Draft, 20, 0);
        var oversized = Layout(legacy, "D", MarketLayoutStatus.Draft, 150, 2);
        _layouts.Setup(repo => repo.GetCompositionCatalogAsync(MarketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { standalone, published, clone, empty, oversized });
        _maps.Setup(repo => repo.GetByMarketIdAsync(MarketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { draft, active, legacy });

        var response = (await CreateService().GetCompositionSourcesAsync(MarketId, OwnerId)).Data!;

        Assert.Equal(5, response.MaxLayoutsPerMarket);
        Assert.Equal(100, response.MaxSlotsPerMarket);
        Assert.DoesNotContain(response.Maps, map => map.Name == MarketMap.LegacyDraftName);
        var byId = response.Layouts.ToDictionary(layout => layout.LayoutId);
        Assert.True(byId[standalone.Id].IsSelectable);
        Assert.True(byId[standalone.Id].IsStandalone);
        Assert.True(byId[published.Id].IsSelectable);
        Assert.Equal(draft.Id, Assert.Single(byId[published.Id].UsedBy).MarketMapId);
        Assert.Equal("COMPOSITION_DRAFT_CHILD", byId[clone.Id].IneligibleCode);
        Assert.Equal("LAYOUT_NOT_GENERATED", byId[empty.Id].IneligibleCode);
        Assert.Equal("LAYOUT_LARGER_THAN_MARKET", byId[oversized.Id].IneligibleCode);
        Assert.Equal(20, byId[standalone.Id].PhysicalWidthMeters!.Value, 3);
    }

    [Fact]
    public async Task Validation_requires_a_default_view_and_at_least_one_gate()
    {
        var draft = Map("Combined draft", MarketMapStatus.Draft, 3);
        Layout(draft, "A", MarketLayoutStatus.Draft, 20, 2, gate: false);
        _maps.Setup(repo => repo.GetManagementDetailAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        var result = (await CreateService().ValidateAsync(MarketId, draft.Id, OwnerId)).Data!;

        Assert.False(result.CanActivate);
        Assert.Contains(result.Errors, issue => issue.Code == "DEFAULT_VIEW_MISSING");
        Assert.Contains(result.Errors, issue => issue.Code == "COMPOSITION_GATE_REQUIRED");
    }

    [Fact]
    public async Task Activation_rolls_back_and_keeps_previous_map_when_validation_fails()
    {
        var previous = Map("Published", MarketMapStatus.Active, 1);
        var previousLayout = Layout(previous, "A", MarketLayoutStatus.Active, 20, 2, isDefault: true);
        var draft = Map("Combined draft", MarketMapStatus.Draft, 2);
        var draftLayout = Layout(draft, "A", MarketLayoutStatus.Draft, 20, 2, isDefault: false);
        _maps.Setup(repo => repo.GetManagementDetailForUpdateAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        _maps.Setup(repo => repo.GetActiveForUpdateAsync(MarketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previous);

        var error = await Assert.ThrowsAsync<AppException>(() =>
            CreateService().ActivateAsync(MarketId, draft.Id, OwnerId));

        Assert.Equal("MARKET_MAP_VALIDATION_FAILED", error.ErrorCode);
        _unitOfWork.Verify(uow => uow.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(uow => uow.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(MarketMapStatus.Active, previous.Status);
        Assert.Equal(MarketLayoutStatus.Active, previousLayout.Status);
        Assert.Equal(MarketMapStatus.Draft, draft.Status);
        Assert.Equal(MarketLayoutStatus.Draft, draftLayout.Status);
    }

    [Fact]
    public async Task Activation_rolls_back_when_the_resulting_composition_is_inconsistent()
    {
        var draft = Map("Combined draft", MarketMapStatus.Draft, 2);
        Layout(draft, "A", MarketLayoutStatus.Draft, 20, 2, isDefault: true);
        _maps.Setup(repo => repo.GetManagementDetailForUpdateAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        _layouts.Setup(repo => repo.GetOperationalByMarketForUpdateAsync(MarketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MarketLayout>());
        _maps.Setup(repo => repo.GetActiveByMarketIdAsync(MarketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        _layouts.Setup(repo => repo.GetPublishedMapsAsync(MarketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MarketLayout>());

        var error = await Assert.ThrowsAsync<AppException>(() =>
            CreateService().ActivateAsync(MarketId, draft.Id, OwnerId));

        Assert.Equal("ACTIVE_COMPOSITION_INVARIANT_FAILED", error.ErrorCode);
        _unitOfWork.Verify(uow => uow.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(uow => uow.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Activation_archives_previous_map_and_publishes_every_child_layout()
    {
        var previous = Map("Published", MarketMapStatus.Active, 1);
        var previousLayout = Layout(previous, "A", MarketLayoutStatus.Active, 20, 2, isDefault: true);
        var draft = Map("Combined draft", MarketMapStatus.Draft, 2);
        var west = Layout(draft, "A", MarketLayoutStatus.Draft, 20, 2, isDefault: true);
        var east = Layout(draft, "B", MarketLayoutStatus.Draft, 20, 2);
        east.OffsetXMeters = 30;
        east.DisplayOrder = 1;
        _maps.Setup(repo => repo.GetManagementDetailForUpdateAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        _maps.Setup(repo => repo.GetActiveForUpdateAsync(MarketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previous);
        _layouts.Setup(repo => repo.GetOperationalByMarketForUpdateAsync(MarketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { previousLayout });
        _maps.Setup(repo => repo.GetActiveByMarketIdAsync(MarketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        _layouts.Setup(repo => repo.GetPublishedMapsAsync(MarketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { west, east });

        await CreateService().ActivateAsync(MarketId, draft.Id, OwnerId);

        Assert.Equal(MarketMapStatus.Archived, previous.Status);
        Assert.Equal(MarketLayoutStatus.Inactive, previousLayout.Status);
        Assert.Equal(MarketMapStatus.Active, draft.Status);
        Assert.All(new[] { west, east }, layout => Assert.Equal(MarketLayoutStatus.Active, layout.Status));
        _unitOfWork.Verify(uow => uow.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.LayoutEdges;
using ApplicationLayer.Services.BoothLocations;
using ApplicationLayer.Services.MapNavigation;
using ApplicationLayer.Services.MarketLayouts;
using ApplicationLayer.Services.Subscriptions;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class MarketLayoutDistanceFoundationTests
{
    [Fact]
    public async Task CreateEdge_WithoutPhysicalDistance_RejectsUncalibratedLayout()
    {
        await using var db = await CreateGraphAsync(calibrated: false);
        var service = EdgeService(db);

        var error = await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(
            LayoutId,
            new CreateLayoutEdgeRequest { FromNodeId = FromNodeId, ToNodeId = ToNodeId }));

        Assert.Equal("LAYOUT_DISTANCE_UNCALIBRATED", error.ErrorCode);
        Assert.Empty(await db.LayoutEdges.ToListAsync());
    }

    [Fact]
    public async Task CreateEdge_WithoutPhysicalDistance_UsesCalibratedScaleAndReturnsMetres()
    {
        await using var db = await CreateGraphAsync(calibrated: true);
        var service = EdgeService(db);

        var response = await service.CreateAsync(
            LayoutId,
            new CreateLayoutEdgeRequest { FromNodeId = FromNodeId, ToNodeId = ToNodeId });

        Assert.Equal(5m, response.Data!.Distance);
        Assert.Equal(5m, response.Data.DistanceMeters);
        Assert.Equal(5m, (await db.LayoutEdges.SingleAsync()).Distance);
        Assert.Equal(2, (await db.MarketLayouts.SingleAsync()).GraphRevision);
    }

    [Fact]
    public async Task CreateEdge_LegacyDistanceField_RemainsACompatibleMetresAlias()
    {
        await using var db = await CreateGraphAsync(calibrated: false);
        var response = await EdgeService(db).CreateAsync(
            LayoutId,
            new CreateLayoutEdgeRequest { FromNodeId = FromNodeId, ToNodeId = ToNodeId, Distance = 7.25m });

        Assert.Equal(7.25m, response.Data!.DistanceMeters);
    }

    [Fact]
    public async Task CreateEdge_OnActiveLayout_IsRejectedAsImmutable()
    {
        await using var db = await CreateGraphAsync(calibrated: true);
        (await db.MarketLayouts.SingleAsync()).Status = MarketLayoutStatus.Active;
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AppException>(() => EdgeService(db).CreateAsync(
            LayoutId, new CreateLayoutEdgeRequest { FromNodeId = FromNodeId, ToNodeId = ToNodeId }));

        Assert.Equal("LAYOUT_ACTIVE_EDIT_FORBIDDEN", error.ErrorCode);
        Assert.Empty(await db.LayoutEdges.ToListAsync());
    }

    private static LayoutEdgeService EdgeService(SNMDbContext db)
        => new(
            new LayoutEdgeRepository(db),
            new LayoutNodeRepository(db),
            new MarketLayoutRepository(db),
            new NightMarketRepository(db),
            new MapperConfiguration(
                configuration => configuration.AddProfile<MappingProfile>(),
                NullLoggerFactory.Instance).CreateMapper(),
            new Mock<ISubscriptionEntitlementService>().Object);

    private static async Task<SNMDbContext> CreateGraphAsync(bool calibrated)
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new SNMDbContext(options);
        var market = new NightMarket
        {
            Id = MarketId, Name = "Foundation market", Address = "Test", Status = NightMarketStatus.Active,
            ModerationStatus = ModerationStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.NightMarkets.Add(market);
        db.MarketLayouts.Add(new MarketLayout
        {
            Id = LayoutId, NightMarketId = MarketId, NightMarket = market, LayoutName = "Draft", Version = 1,
            Width = 100, Height = 100, Status = MarketLayoutStatus.Draft,
            CoordinateUnit = LayoutCoordinateUnit.LayoutUnit,
            MetersPerLayoutUnit = calibrated ? 0.1m : null,
            DistanceCalibrationStatus = calibrated ? DistanceCalibrationStatus.Calibrated : DistanceCalibrationStatus.Uncalibrated,
            GraphRevision = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.LayoutNodes.AddRange(
            new LayoutNode { Id = FromNodeId, LayoutId = LayoutId, NodeName = "From", NodeType = LayoutNodeType.Junction, Xcoordinate = 0, Ycoordinate = 0, IsAccessible = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LayoutNode { Id = ToNodeId, LayoutId = LayoutId, NodeName = "To", NodeType = LayoutNodeType.Junction, Xcoordinate = 30, Ycoordinate = 40, IsAccessible = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return db;
    }

    private static readonly Guid MarketId = Guid.Parse("51000000-0000-0000-0000-000000000001");
    private static readonly Guid LayoutId = Guid.Parse("51000000-0000-0000-0000-000000000002");
    private static readonly Guid FromNodeId = Guid.Parse("51000000-0000-0000-0000-000000000003");
    private static readonly Guid ToNodeId = Guid.Parse("51000000-0000-0000-0000-000000000004");
}

public class IndoorRouteSolverFoundationTests
{
    [Fact]
    public void Solve_UsesDirectionAccessibilityAndExactEdgeWeights()
    {
        var a = Node("a", true); var b = Node("b", true); var c = Node("c", true); var blocked = Node("d", false);
        var direct = Edge("direct", a, c, 20, false, true);
        var first = Edge("first", a, b, 3, false, true);
        var second = Edge("second", b, c, 4, false, true);
        var inaccessible = Edge("inaccessible", a, c, 1, false, false);
        var blockedNode = Edge("blocked-node", a, blocked, 1, true, true);

        var result = new DijkstraIndoorRouteSolver().Solve(
            [a, b, c, blocked], [direct, first, second, inaccessible, blockedNode], a.Id, c.Id, new IndoorRoutePolicy());

        Assert.Equal([a.Id, b.Id, c.Id], result.NodeIds);
        Assert.Equal([first.Id, second.Id], result.EdgeIds);
        Assert.Equal(7m, result.TotalDistanceMeters);
        Assert.False(new DijkstraIndoorRouteSolver().Solve([a, b], [first], b.Id, a.Id, new IndoorRoutePolicy()).Found);
    }

    private static LayoutNode Node(string key, bool accessible) => new()
    {
        Id = Id(key), LayoutId = Id("layout"), NodeType = LayoutNodeType.Junction, IsAccessible = accessible
    };

    private static LayoutEdge Edge(string key, LayoutNode from, LayoutNode to, decimal distance, bool bidirectional, bool accessible) => new()
    {
        Id = Id(key), LayoutId = Id("layout"), FromNodeId = from.Id, ToNodeId = to.Id,
        Distance = distance, IsBidirectional = bidirectional, IsAccessible = accessible
    };

    private static Guid Id(string value)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
    }
}

public class MarketLayoutCloneFoundationTests
{
    [Fact]
    public async Task CloneActiveLayout_RemapsGraphAndKeepsBoothLocationPerLayout()
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new SNMDbContext(options);
        var marketId = Guid.NewGuid(); var layoutId = Guid.NewGuid(); var fromId = Guid.NewGuid(); var boothNodeId = Guid.NewGuid(); var boothId = Guid.NewGuid();
        db.NightMarkets.Add(new NightMarket { Id = marketId, Name = "Market", Address = "Test", Status = NightMarketStatus.Active, ModerationStatus = ModerationStatus.Active });
        db.MarketLayouts.Add(new MarketLayout { Id = layoutId, NightMarketId = marketId, LayoutName = "Published", Version = 3, Width = 100, Height = 100, Status = MarketLayoutStatus.Active, GraphRevision = 9 });
        db.LayoutNodes.AddRange(
            new LayoutNode { Id = fromId, LayoutId = layoutId, NodeName = "Entrance", NodeType = LayoutNodeType.Entrance, IsAccessible = true, IsStartingPoint = true },
            new LayoutNode { Id = boothNodeId, LayoutId = layoutId, NodeName = "Booth", NodeType = LayoutNodeType.BoothAccess, IsAccessible = true });
        db.LayoutEdges.Add(new LayoutEdge { Id = Guid.NewGuid(), LayoutId = layoutId, FromNodeId = fromId, ToNodeId = boothNodeId, Distance = 12, IsAccessible = true, IsBidirectional = true });
        db.Booths.Add(new Booth { Id = boothId, NightMarketId = marketId, BoothName = "Booth", Status = BoothStatus.Active });
        db.BoothLocations.Add(new BoothLocation { Id = Guid.NewGuid(), BoothId = boothId, LayoutId = layoutId, LayoutNodeId = boothNodeId });
        await db.SaveChangesAsync();

        var clone = await new MarketLayoutRepository(db).CloneToDraftAsync(layoutId, null, DateTime.UtcNow);

        Assert.Equal(MarketLayoutStatus.Draft, clone.Status);
        Assert.Equal(4, clone.Version);
        Assert.Equal(1, clone.GraphRevision);
        var cloneNodes = await db.LayoutNodes.Where(x => x.LayoutId == clone.Id).ToListAsync();
        var cloneEdge = await db.LayoutEdges.SingleAsync(x => x.LayoutId == clone.Id);
        var cloneLocation = await db.BoothLocations.SingleAsync(x => x.LayoutId == clone.Id);
        Assert.Equal(2, cloneNodes.Count);
        Assert.Contains(cloneNodes, x => x.Id == cloneEdge.FromNodeId);
        Assert.Contains(cloneNodes, x => x.Id == cloneEdge.ToNodeId);
        Assert.Contains(cloneNodes, x => x.Id == cloneLocation.LayoutNodeId);
        Assert.NotEqual(boothNodeId, cloneLocation.LayoutNodeId);
        Assert.Equal(2, await db.BoothLocations.CountAsync(x => x.BoothId == boothId && !x.IsDeleted));
    }
}

public class MapRouteVersionFoundationTests
{
    [Fact]
    public async Task RouteRequest_WithStaleGraphRevision_ReturnsTypedConflictBeforeSolving()
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new SNMDbContext(options);
        var marketId = Guid.NewGuid(); var layoutId = Guid.NewGuid();
        db.NightMarkets.Add(new NightMarket
        {
            Id = marketId, Name = "Market", Address = "Test", Status = NightMarketStatus.Open,
            ModerationStatus = ModerationStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.MarketLayouts.Add(new MarketLayout
        {
            Id = layoutId, NightMarketId = marketId, LayoutName = "Active", Version = 5,
            GraphRevision = 8, Width = 100, Height = 100, Status = MarketLayoutStatus.Active
        });
        await db.SaveChangesAsync();
        var mapper = new MapperConfiguration(configuration => configuration.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var service = new MapNavigationService(
            new NightMarketRepository(db), new MarketLayoutRepository(db), new ZoneRepository(db),
            new LayoutNodeRepository(db), new LayoutEdgeRepository(db), new BoothLocationRepository(db),
            new BoothRepository(db), mapper, new DijkstraIndoorRouteSolver());

        var error = await Assert.ThrowsAsync<AppException>(() => service.FindRouteToBoothAsync(
            layoutId, Guid.NewGuid(), Guid.NewGuid(), expectedLayoutVersion: 5, expectedGraphRevision: 7));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("MAP_LAYOUT_VERSION_MISMATCH", error.ErrorCode);
    }
}

public class LayoutGraphValidationFoundationTests
{
    [Fact]
    public async Task Validate_UsesTheSameAccessibleDirectedGraphPolicyAsRouting()
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new SNMDbContext(options);
        var marketId = Guid.NewGuid(); var layoutId = Guid.NewGuid(); var entranceId = Guid.NewGuid(); var boothNodeId = Guid.NewGuid(); var boothId = Guid.NewGuid(); var invalidBoothId = Guid.NewGuid();
        db.NightMarkets.Add(new NightMarket { Id = marketId, Name = "Market", Address = "Test" });
        db.MarketLayouts.Add(new MarketLayout { Id = layoutId, NightMarketId = marketId, LayoutName = "Draft", LayoutImageUrl = "layout.svg", Version = 1, GraphRevision = 1, Width = 100, Height = 100, Status = MarketLayoutStatus.Draft });
        db.LayoutNodes.AddRange(
            new LayoutNode { Id = entranceId, LayoutId = layoutId, NodeName = "Entrance", NodeType = LayoutNodeType.Entrance, IsAccessible = true, IsStartingPoint = true },
            new LayoutNode { Id = boothNodeId, LayoutId = layoutId, NodeName = "Booth", NodeType = LayoutNodeType.BoothAccess, IsAccessible = false },
            new LayoutNode { Id = Guid.NewGuid(), LayoutId = layoutId, NodeName = "Blocked entrance", NodeType = LayoutNodeType.Entrance, IsAccessible = false, IsStartingPoint = true });
        db.LayoutEdges.AddRange(
            new LayoutEdge { Id = Guid.NewGuid(), LayoutId = layoutId, FromNodeId = entranceId, ToNodeId = boothNodeId, Distance = 5, IsAccessible = true, IsBidirectional = true },
            new LayoutEdge { Id = Guid.NewGuid(), LayoutId = layoutId, FromNodeId = boothNodeId, ToNodeId = entranceId, Distance = 5, IsAccessible = true, IsBidirectional = true },
            new LayoutEdge { Id = Guid.NewGuid(), LayoutId = layoutId, FromNodeId = entranceId, ToNodeId = entranceId, Distance = 0, IsAccessible = true, IsBidirectional = false },
            new LayoutEdge { Id = Guid.NewGuid(), LayoutId = layoutId, FromNodeId = entranceId, ToNodeId = Guid.NewGuid(), Distance = 1, IsAccessible = true, IsBidirectional = false });
        db.Booths.AddRange(
            new Booth { Id = boothId, NightMarketId = marketId, BoothName = "Booth" },
            new Booth { Id = invalidBoothId, NightMarketId = marketId, BoothName = "Invalid booth" });
        db.BoothLocations.AddRange(
            new BoothLocation { Id = Guid.NewGuid(), BoothId = boothId, LayoutId = layoutId, LayoutNodeId = boothNodeId },
            new BoothLocation { Id = Guid.NewGuid(), BoothId = invalidBoothId, LayoutId = layoutId, LayoutNodeId = Guid.NewGuid() });
        await db.SaveChangesAsync();

        var result = await new LayoutGraphValidationService(
            new MarketLayoutRepository(db), new LayoutNodeRepository(db), new LayoutEdgeRepository(db), new BoothLocationRepository(db))
            .ValidateAsync(layoutId);

        Assert.Contains(result.Errors, error => error.Contains("duplicate edges"));
        Assert.Contains(result.Errors, error => error.Contains("cannot connect a node to itself"));
        Assert.Contains(result.Errors, error => error.Contains("distance greater than zero"));
        Assert.Contains(result.Errors, error => error.Contains("no connected walkway"));
        Assert.Contains(result.Errors, error => error.Contains("invalid node"));
    }
}

public class BoothLocationImmutabilityFoundationTests
{
    [Fact]
    public async Task AssignLocation_OnActiveLayout_IsRejected()
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new SNMDbContext(options);
        var marketId = Guid.NewGuid(); var layoutId = Guid.NewGuid(); var nodeId = Guid.NewGuid(); var boothId = Guid.NewGuid();
        db.NightMarkets.Add(new NightMarket { Id = marketId, Name = "Market", Address = "Test" });
        db.MarketLayouts.Add(new MarketLayout { Id = layoutId, NightMarketId = marketId, LayoutName = "Active", Version = 1, GraphRevision = 1, Width = 100, Height = 100, Status = MarketLayoutStatus.Active });
        db.LayoutNodes.Add(new LayoutNode { Id = nodeId, LayoutId = layoutId, NodeType = LayoutNodeType.BoothAccess, IsAccessible = true });
        db.Booths.Add(new Booth { Id = boothId, NightMarketId = marketId, BoothName = "Booth" });
        await db.SaveChangesAsync();
        var mapper = new MapperConfiguration(configuration => configuration.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var service = new BoothLocationService(
            new BoothLocationRepository(db), new LayoutNodeRepository(db), new MarketLayoutRepository(db),
            new BoothRepository(db), new ZoneRepository(db), mapper);

        var error = await Assert.ThrowsAsync<AppException>(() => service.AssignAsync(
            boothId, new AssignBoothLocationRequest { LayoutId = layoutId, LayoutNodeId = nodeId }));

        Assert.Equal("ACTIVE_LAYOUT_IMMUTABLE", error.ErrorCode);
        Assert.Empty(await db.BoothLocations.ToListAsync());
    }
}

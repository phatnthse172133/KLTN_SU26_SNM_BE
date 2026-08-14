using System.Text.Json;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.MapNavigation;
using ApplicationLayer.Services.NavigationAnchors;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class IndoorRouteInstructionBuilderTests
{
    [Fact]
    public void TurnClassification_UsesTopLeftYAxisAndCoversCanonicalCodes()
    {
        var previous = Node(0, 0); var current = Node(10, 0);
        Assert.Equal("STRAIGHT", IndoorRouteInstructionBuilder.ClassifyTurn(previous, current, Node(20, 0)));
        Assert.Equal("SLIGHT_RIGHT", IndoorRouteInstructionBuilder.ClassifyTurn(previous, current, Node(20, 5)));
        Assert.Equal("TURN_RIGHT", IndoorRouteInstructionBuilder.ClassifyTurn(previous, current, Node(10, 10)));
        Assert.Equal("SLIGHT_LEFT", IndoorRouteInstructionBuilder.ClassifyTurn(previous, current, Node(20, -5)));
        Assert.Equal("TURN_LEFT", IndoorRouteInstructionBuilder.ClassifyTurn(previous, current, Node(10, -10)));
        Assert.Equal("UTURN", IndoorRouteInstructionBuilder.ClassifyTurn(previous, current, Node(0, 0)));
    }

    [Fact]
    public void Build_MergesConsecutiveStraightSegmentsAndSkipsNoise()
    {
        var a = Node(0, 0); var b = Node(10, 0); var c = Node(20, 0); var d = Node(20.2m, 0);
        var e1 = Edge(a, b, 4); var e2 = Edge(b, c, 6); var noise = Edge(c, d, .2m);
        var result = new IndoorRouteInstructionBuilder().Build(
            [a.Id, b.Id, c.Id, d.Id], [e1.Id, e2.Id, noise.Id],
            new[] { a, b, c, d }.ToDictionary(x => x.Id), new[] { e1, e2, noise }.ToDictionary(x => x.Id), 1m).ToList();
        Assert.Equal(["START", "STRAIGHT", "ARRIVE"], result.Select(x => x.InstructionCode));
        Assert.Equal(10m, result[1].DistanceMeters);
    }

    [Fact]
    public void Build_UsesDestinationBoothForArrivalSide()
    {
        var start = Node(0, 0); var access = Node(10, 0); var booth = Node(10, 10);
        booth.SlotCode = "A-01";
        var edge = Edge(start, access, 4);
        var result = new IndoorRouteInstructionBuilder().Build(
            [start.Id, access.Id], [edge.Id],
            new[] { start, access }.ToDictionary(x => x.Id), new[] { edge }.ToDictionary(x => x.Id),
            1m, booth).ToList();
        Assert.Equal(["START", "STRAIGHT", "ARRIVE_RIGHT"], result.Select(x => x.InstructionCode));
        Assert.Equal("A-01", result[^1].ReferenceName);
    }

    private static LayoutNode Node(decimal x, decimal y) => new() { Id = Guid.NewGuid(), Xcoordinate = x, Ycoordinate = y };
    private static LayoutEdge Edge(LayoutNode from, LayoutNode to, decimal distance) => new()
        { Id = Guid.NewGuid(), FromNodeId = from.Id, ToNodeId = to.Id, Distance = distance };
}

public class NavigationEntranceServiceTests
{
    [Fact]
    public void NavigationRuntimeContract_DoesNotExposeQrResolveOrQrAdminFields()
    {
        var serviceMethods = typeof(INavigationAnchorService).GetMethods().Select(method => method.Name).ToArray();
        Assert.DoesNotContain("ResolveAsync", serviceMethods);
        Assert.DoesNotContain("GetLandmarksAsync", serviceMethods);

        var requestProperties = typeof(SaveNavigationAnchorRequest).GetProperties().Select(property => property.Name).ToArray();
        Assert.DoesNotContain("QrToken", requestProperties);
        Assert.DoesNotContain("IsQrEnabled", requestProperties);
    }

    [Fact]
    public void Haversine_RanksNearbyCoordinatesAndRejectsInvalidCoordinates()
    {
        var near = NavigationAnchorService.HaversineMeters(10, 106, 10.0001, 106);
        var far = NavigationAnchorService.HaversineMeters(10, 106, 10.01, 106);
        Assert.True(near < far);
        Assert.Throws<AppException>(() => NavigationAnchorService.ValidateCoordinates(91, 106));
        Assert.Throws<AppException>(() => NavigationAnchorService.ValidateCoordinates(10, double.NaN));
    }

    [Fact]
    public async Task PublicEntrances_FilterExitBlockedAndUnreachable_AndSerializeVersionedContract()
    {
        await using var db = await DatabaseAsync();
        var service = Service(db);
        var response = await service.GetNearestEntrancesAsync(MarketId, 10.0001, 106.0001, BoothId);

        var entrance = Assert.Single(response.Data!.Entrances);
        Assert.Equal(GoodEntranceNodeId, entrance.NodeId);
        Assert.NotNull(entrance.OutdoorDistanceMeters);
        Assert.Equal(5m, entrance.IndoorDistanceMeters);
        Assert.Equal(1, entrance.IndoorEstimatedWalkingMinutes);
        Assert.Equal(1, response.Data.LayoutVersion);
        Assert.Equal(1, response.Data.GraphRevision);
        var json = JsonSerializer.Serialize(response.Data);
        Assert.Contains("LayoutVersion", json);
        Assert.Contains("GraphRevision", json);
        Assert.DoesNotContain("IsDeleted", json);
    }

    [Fact]
    public async Task UncalibratedLayout_DoesNotInventIndoorEta()
    {
        await using var db = await DatabaseAsync();
        var layout = await db.MarketLayouts.SingleAsync(x => x.Id == LayoutId);
        layout.DistanceCalibrationStatus = DistanceCalibrationStatus.Uncalibrated;
        layout.MetersPerLayoutUnit = null;
        await db.SaveChangesAsync();

        var entrance = Assert.Single((await Service(db).GetEntrancesAsync(MarketId, BoothId)).Data!.Entrances);
        Assert.Null(entrance.IndoorDistanceMeters);
        Assert.Null(entrance.IndoorEstimatedWalkingMinutes);
    }

    [Fact]
    public async Task StaleHandoffAndActiveAnchorMutation_AreRejectedWithTypedConflicts()
    {
        await using var db = await DatabaseAsync();
        var service = Service(db);
        var stale = await Assert.ThrowsAsync<AppException>(() => service.GetEntrancesAsync(
            MarketId, expectedLayoutVersion: 1, expectedGraphRevision: 0));
        Assert.Equal("MAP_LAYOUT_VERSION_MISMATCH", stale.ErrorCode);
        var mutation = await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(LayoutId, new SaveNavigationAnchorRequest
        {
            LayoutNodeId = GoodEntranceNodeId, AnchorType = NavigationAnchorType.Entrance,
            AnchorCode = "NEW", DisplayName = "New", Latitude = 10, Longitude = 106
        }));
        Assert.Equal("ACTIVE_LAYOUT_IMMUTABLE", mutation.ErrorCode);
    }

    [Fact]
    public async Task DraftAnchor_RejectsCrossLayoutNode()
    {
        await using var db = await DatabaseAsync();
        var draft = new MarketLayout { Id = Guid.NewGuid(), NightMarketId = MarketId, LayoutName = "Draft", Version = 2,
            GraphRevision = 1, Width = 100, Height = 100, Status = MarketLayoutStatus.Draft };
        db.MarketLayouts.Add(draft); await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<AppException>(() => Service(db).CreateAsync(draft.Id, new SaveNavigationAnchorRequest
        {
            LayoutNodeId = GoodEntranceNodeId, AnchorType = NavigationAnchorType.Entrance,
            AnchorCode = "CROSS", DisplayName = "Cross", Latitude = 10, Longitude = 106
        }));
        Assert.Equal("ANCHOR_NODE_LAYOUT_MISMATCH", error.ErrorCode);
    }

    private static NavigationAnchorService Service(SNMDbContext db) => new(
        new LayoutNavigationAnchorRepository(db), new MarketLayoutRepository(db), new LayoutNodeRepository(db),
        new LayoutEdgeRepository(db), new BoothLocationRepository(db), new BoothRepository(db),
        new NightMarketRepository(db), new DijkstraIndoorRouteSolver());

    private static async Task<SNMDbContext> DatabaseAsync()
    {
        var db = new SNMDbContext(new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        db.NightMarkets.Add(new NightMarket { Id = MarketId, Name = "Market", Address = "Test", Status = NightMarketStatus.Open,
            ModerationStatus = ModerationStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.MarketLayouts.Add(new MarketLayout { Id = LayoutId, NightMarketId = MarketId, LayoutName = "Active", Version = 1,
            GraphRevision = 1, Width = 100, Height = 100, Status = MarketLayoutStatus.Active,
            DistanceCalibrationStatus = DistanceCalibrationStatus.Calibrated, MetersPerLayoutUnit = .1m });
        var good = Node(GoodEntranceNodeId, LayoutNodeType.Entrance, true, 0, 0);
        var exit = Node(Guid.NewGuid(), LayoutNodeType.Exit, true, 0, 10);
        var blocked = Node(Guid.NewGuid(), LayoutNodeType.Entrance, false, 0, 20);
        var unreachable = Node(Guid.NewGuid(), LayoutNodeType.Entrance, true, 0, 30);
        var boothNode = Node(BoothNodeId, LayoutNodeType.BoothAccess, true, 10, 0);
        db.LayoutNodes.AddRange(good, exit, blocked, unreachable, boothNode);
        db.LayoutEdges.Add(new LayoutEdge { Id = Guid.NewGuid(), LayoutId = LayoutId, FromNodeId = good.Id, ToNodeId = boothNode.Id,
            Distance = 5, IsAccessible = true, IsBidirectional = true });
        db.Booths.Add(new Booth { Id = BoothId, NightMarketId = MarketId, BoothName = "Booth", Status = BoothStatus.Active });
        db.BoothLocations.Add(new BoothLocation { Id = Guid.NewGuid(), BoothId = BoothId, LayoutId = LayoutId,
            LayoutNodeId = boothNode.Id, Xcoordinate = 10, Ycoordinate = 0 });
        db.LayoutNavigationAnchors.AddRange(
            Anchor(good, NavigationAnchorType.Entrance, true, true, "GOOD", 10, 106),
            Anchor(exit, NavigationAnchorType.Exit, true, true, "EXIT", 10, 106.001m),
            Anchor(blocked, NavigationAnchorType.Entrance, true, true, "BLOCKED", 10, 106.002m),
            Anchor(unreachable, NavigationAnchorType.Entrance, true, true, "UNREACHABLE", 10, 106.003m));
        await db.SaveChangesAsync();
        return db;
    }
    private static LayoutNode Node(Guid id, LayoutNodeType type, bool accessible, decimal x, decimal y) => new()
        { Id = id, LayoutId = LayoutId, NodeType = type, IsAccessible = accessible, Xcoordinate = x, Ycoordinate = y };
    private static LayoutNavigationAnchor Anchor(LayoutNode node, NavigationAnchorType type, bool accessible, bool active,
        string code, decimal latitude, decimal longitude) => new()
        { Id = Guid.NewGuid(), LayoutId = LayoutId, LayoutNodeId = node.Id, LayoutNode = node, AnchorType = type,
          AnchorCode = code, DisplayName = code, Latitude = latitude, Longitude = longitude,
          IsCustomerAccessible = accessible, IsActive = active };
    private static readonly Guid MarketId = Guid.Parse("61000000-0000-0000-0000-000000000001");
    private static readonly Guid LayoutId = Guid.Parse("61000000-0000-0000-0000-000000000002");
    private static readonly Guid GoodEntranceNodeId = Guid.Parse("61000000-0000-0000-0000-000000000003");
    private static readonly Guid BoothNodeId = Guid.Parse("61000000-0000-0000-0000-000000000004");
    private static readonly Guid BoothId = Guid.Parse("61000000-0000-0000-0000-000000000005");
}

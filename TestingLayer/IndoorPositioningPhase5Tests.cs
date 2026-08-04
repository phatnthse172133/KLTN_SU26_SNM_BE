using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.IndoorPositioning;
using ApplicationLayer.Services.MapNavigation;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

[CollectionDefinition("Phase5", DisableParallelization = true)]
public class Phase5Collection { }

[Collection("Phase5")]
public class EdgeProjectionPhase5Tests
{
    [Theory]
    [InlineData(5, 2, 5, 0, .5)]
    [InlineData(-5, 1, 0, 0, 0)]
    [InlineData(15, 1, 10, 0, 1)]
    public void Projection_ClampsToSegment(decimal x, decimal y, decimal expectedX, decimal expectedY, decimal progress)
    {
        var from = Node(Guid.NewGuid(), 0, 0); var to = Node(Guid.NewGuid(), 10, 0);
        var result = IndoorPositioningService.Project(x, y, Edge(from, to, true), from, to);
        Assert.Equal(expectedX, result.X); Assert.Equal(expectedY, result.Y); Assert.Equal(progress, result.Progress);
    }

    [Fact]
    public void Projection_HandlesZeroLengthAndTopLeftCoordinates()
    {
        var point = Node(Guid.NewGuid(), 4, 8);
        var zero = IndoorPositioningService.Project(7, 12, Edge(point, point, true), point, point);
        Assert.Equal(0, zero.Progress); Assert.Equal(5, zero.DistanceLayoutUnits);
        var right = Node(Guid.NewGuid(), 10, 10); var down = Node(Guid.NewGuid(), 10, 20);
        var vertical = IndoorPositioningService.Project(12, 15, Edge(right, down, true), right, down);
        Assert.Equal(10, vertical.X); Assert.Equal(15, vertical.Y); Assert.Equal(.5m, vertical.Progress);
    }

    internal static LayoutNode Node(Guid id, decimal x, decimal y, Guid? layout = null) => new()
        { Id = id, LayoutId = layout ?? Phase5Fixture.LayoutId, Xcoordinate = x, Ycoordinate = y, IsAccessible = true };
    internal static LayoutEdge Edge(LayoutNode from, LayoutNode to, bool bidirectional, decimal distance = 100) => new()
        { Id = Guid.NewGuid(), LayoutId = from.LayoutId, FromNodeId = from.Id, ToNodeId = to.Id, Distance = distance, IsAccessible = true, IsBidirectional = bidirectional };
}

[Collection("Phase5")]
public class IndoorPositioningServicePhase5Tests
{
    [Fact]
    public async Task Snap_SelectsNearestAccessibleEdge_AndReturnsCalibratedDistance()
    {
        await using var db = await Phase5Fixture.CreateAsync();
        var blocked = EdgeProjectionPhase5Tests.Edge(Phase5Fixture.A, Phase5Fixture.B, true); blocked.Id = Guid.NewGuid(); blocked.IsAccessible = false;
        var duplicate = EdgeProjectionPhase5Tests.Edge(Phase5Fixture.A, Phase5Fixture.B, true); duplicate.Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var crossLayout = EdgeProjectionPhase5Tests.Edge(Phase5Fixture.A, Phase5Fixture.B, true); crossLayout.Id = Guid.NewGuid(); crossLayout.LayoutId = Guid.NewGuid();
        db.LayoutEdges.AddRange(blocked, duplicate, crossLayout); await db.SaveChangesAsync();
        var response = await Phase5Fixture.Positioning(db).SnapAsync(Phase5Fixture.LayoutId, new()
        { X = 25, Y = 2, Source = IndoorPositionSource.MapTap, ExpectedLayoutVersion = 1, ExpectedGraphRevision = 1, MaximumSnapDistanceMeters = 8 });
        Assert.Equal(.25m, response.Data!.EdgeProgress); Assert.Equal(2m, response.Data.DistanceFromGraphMeters);
        Assert.Equal("MAP_TAP", response.Data.Source); Assert.Equal(Phase5Fixture.OriginEdgeId, response.Data.SnappedEdgeId);
    }

    [Fact]
    public async Task Snap_RejectsTooFar_AndUncalibratedDoesNotClaimMeters()
    {
        await using var db = await Phase5Fixture.CreateAsync();
        var error = await Assert.ThrowsAsync<AppException>(() => Phase5Fixture.Positioning(db).SnapAsync(Phase5Fixture.LayoutId, new()
        { X = 25, Y = 20, Source = IndoorPositionSource.MapTap, ExpectedLayoutVersion = 1, ExpectedGraphRevision = 1, MaximumSnapDistanceMeters = 8 }));
        Assert.Equal("SNAP_TOO_FAR", error.ErrorCode);
        var layout = await db.MarketLayouts.SingleAsync(); layout.DistanceCalibrationStatus = DistanceCalibrationStatus.Uncalibrated; layout.MetersPerLayoutUnit = null; await db.SaveChangesAsync();
        var result = await Phase5Fixture.Positioning(db).SnapAsync(Phase5Fixture.LayoutId, new()
        { X = 25, Y = 2, Source = IndoorPositionSource.MapTap, ExpectedLayoutVersion = 1, ExpectedGraphRevision = 1 });
        Assert.Null(result.Data!.DistanceFromGraphMeters); Assert.Equal(2m, result.Data.DistanceFromGraphLayoutUnits);
    }

    [Theory]
    [InlineData(.25, 125)]
    [InlineData(.50, 100)]
    [InlineData(.75, 75)]
    public async Task VirtualOrigin_BidirectionalPartialCostAndPath(decimal progress, decimal total)
    {
        await using var db = await Phase5Fixture.CreateAsync();
        var response = await Phase5Fixture.Positioning(db).RouteFromSnappedPositionAsync(Phase5Fixture.LayoutId, new()
        { SnappedEdgeId = Phase5Fixture.OriginEdgeId, EdgeProgress = progress, SnappedX = progress * 100, SnappedY = 0,
          BoothId = Phase5Fixture.BoothId, ExpectedLayoutVersion = 1, ExpectedGraphRevision = 1 });
        Assert.Equal(total, response.Data!.TotalDistanceMeters); Assert.True(response.Data.Path.First().IsVirtualOrigin);
        Assert.Null(response.Data.Path.First().NodeId); Assert.Equal(progress * 100, response.Data.Path.First().XCoordinate);
    }

    [Fact]
    public async Task VirtualOrigin_OneWayUsesForwardEndpointAndCannotReachReverseDestination()
    {
        await using var db = await Phase5Fixture.CreateAsync(bidirectional: false);
        var forward = await Phase5Fixture.Positioning(db).RouteFromSnappedPositionAsync(Phase5Fixture.LayoutId, new()
        { SnappedEdgeId = Phase5Fixture.OriginEdgeId, EdgeProgress = .5m, SnappedX = 50, SnappedY = 0,
          BoothId = Phase5Fixture.BoothId, ExpectedLayoutVersion = 1, ExpectedGraphRevision = 1 });
        Assert.Equal(100m, forward.Data!.TotalDistanceMeters);
        var location = await db.BoothLocations.SingleAsync(); location.LayoutNodeId = Phase5Fixture.A.Id; await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<AppException>(() => Phase5Fixture.Positioning(db).RouteFromSnappedPositionAsync(Phase5Fixture.LayoutId, new()
        { SnappedEdgeId = Phase5Fixture.OriginEdgeId, EdgeProgress = .5m, SnappedX = 50, SnappedY = 0,
          BoothId = Phase5Fixture.BoothId, ExpectedLayoutVersion = 1, ExpectedGraphRevision = 1 }));
        Assert.Equal("ROUTE_NOT_FOUND", error.ErrorCode);
    }

    [Fact]
    public async Task PositioningRejectsStaleRevision()
    {
        await using var db = await Phase5Fixture.CreateAsync();
        var error = await Assert.ThrowsAsync<AppException>(() => Phase5Fixture.Positioning(db).SnapAsync(Phase5Fixture.LayoutId, new()
        { X = 5, Y = 0, Source = IndoorPositionSource.MapTap, ExpectedLayoutVersion = 1, ExpectedGraphRevision = 2 }));
        Assert.Equal("MAP_LAYOUT_VERSION_MISMATCH", error.ErrorCode);
    }
}

internal static class Phase5Fixture
{
    internal static readonly Guid MarketId = Guid.Parse("65000000-0000-0000-0000-000000000001");
    internal static readonly Guid LayoutId = Guid.Parse("65000000-0000-0000-0000-000000000002");
    internal static readonly Guid BoothId = Guid.Parse("65000000-0000-0000-0000-000000000003");
    internal static readonly Guid OriginEdgeId = Guid.Parse("65000000-0000-0000-0000-000000000004");
    internal static LayoutNode A = null!; internal static LayoutNode B = null!; internal static LayoutNode Destination = null!;

    internal static async Task<SNMDbContext> CreateAsync(bool bidirectional = true)
    {
        var db = new SNMDbContext(new DbContextOptionsBuilder<SNMDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.NightMarkets.Add(new NightMarket { Id = MarketId, Name = "Phase 5", Address = "Test", Status = NightMarketStatus.Open, ModerationStatus = ModerationStatus.Active });
        db.MarketLayouts.Add(new MarketLayout { Id = LayoutId, NightMarketId = MarketId, LayoutName = "Active", Version = 1, GraphRevision = 1,
            Width = 200, Height = 200, Status = MarketLayoutStatus.Active, DistanceCalibrationStatus = DistanceCalibrationStatus.Calibrated, MetersPerLayoutUnit = 1 });
        A = EdgeProjectionPhase5Tests.Node(Guid.Parse("65000000-0000-0000-0000-000000000010"), 0, 0);
        B = EdgeProjectionPhase5Tests.Node(Guid.Parse("65000000-0000-0000-0000-000000000011"), 100, 0);
        Destination = EdgeProjectionPhase5Tests.Node(Guid.Parse("65000000-0000-0000-0000-000000000012"), 150, 0);
        db.LayoutNodes.AddRange(A, B, Destination);
        var origin = EdgeProjectionPhase5Tests.Edge(A, B, bidirectional, 100); origin.Id = OriginEdgeId;
        db.LayoutEdges.AddRange(origin, EdgeProjectionPhase5Tests.Edge(B, Destination, true, 50));
        db.Booths.Add(new Booth { Id = BoothId, NightMarketId = MarketId, BoothName = "Target", Status = BoothStatus.Active });
        db.BoothLocations.Add(new BoothLocation { Id = Guid.NewGuid(), BoothId = BoothId, LayoutId = LayoutId, LayoutNodeId = Destination.Id, Xcoordinate = 150, Ycoordinate = 0 });
        await db.SaveChangesAsync(); return db;
    }
    internal static IndoorPositioningService Positioning(SNMDbContext db) => new(new MarketLayoutRepository(db), new NightMarketRepository(db),
        new LayoutNodeRepository(db), new LayoutEdgeRepository(db), new BoothLocationRepository(db), new BoothRepository(db), new DijkstraIndoorRouteSolver());
}

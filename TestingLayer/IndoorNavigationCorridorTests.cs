using ApplicationLayer.Exceptions;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.MapNavigation;
using ApplicationLayer.Services.MarketLayouts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using AutoMapper;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class IndoorNavigationCorridorTests
{
    [Fact]
    public void Builder_DoesNotMutatePersistedGeometry()
    {
        var (layoutId, block, slots, gate) = StandardFixture();
        var originalBlock = (block.X, block.Y, block.Width, block.Height, block.ConfigJson);
        var originalSlots = slots.Select(slot => (slot.Id, slot.Xcoordinate, slot.Ycoordinate, slot.SlotCode)).ToList();
        var originalGate = (gate.Id, gate.Xcoordinate, gate.Ycoordinate);

        var graph = NavigationGraphBuilder.Build(
            layoutId, 200, 200, [block], [.. slots, gate],
            10, 0.40, 0.80, DateTime.UtcNow);

        Assert.True(graph.IsValid);
        Assert.Equal(originalBlock, (block.X, block.Y, block.Width, block.Height, block.ConfigJson));
        Assert.Equal(originalSlots, slots.Select(slot => (slot.Id, slot.Xcoordinate, slot.Ycoordinate, slot.SlotCode)).ToList());
        Assert.Equal(originalGate, (gate.Id, gate.Xcoordinate, gate.Ycoordinate));
        Assert.DoesNotContain(graph.Nodes, node => slots.Any(slot => slot.Id == node.Id && node.NodeType != LayoutNodeType.BoothSlot));
    }

    [Fact]
    public void Builder_PlacesAccessOutsideBoothAndStoresMeterDistances()
    {
        var (layoutId, block, slots, gate) = StandardFixture();
        var graph = NavigationGraphBuilder.Build(
            layoutId, 200, 200, [block], [.. slots, gate],
            10, 0.40, 0.80, DateTime.UtcNow);

        Assert.Equal(4, graph.Nodes.Count(node => node.NodeType == LayoutNodeType.BoothAccess));
        Assert.True(graph.Nodes.Count(node => node.NodeType == LayoutNodeType.Junction) >= 4);
        Assert.Empty(graph.UnreachableSlotCodes);
        Assert.Empty(graph.NarrowCorridorSlotCodes);

        var obstacles = LayoutBlockGeometry.ReconstructBooths([block], slots)
            .Select(item => item with { Bounds = item.Bounds.Inflate(LayoutDistance.ClearancePixels(10, 0.40)) })
            .ToList();
        foreach (var access in graph.Nodes.Where(node => node.NodeType == LayoutNodeType.BoothAccess))
        {
            var own = obstacles.First(item => item.SlotCode == access.SlotCode);
            Assert.DoesNotContain(obstacles.Where(item => item.SlotCode != access.SlotCode),
                item => item.Bounds.Contains((double)access.Xcoordinate, (double)access.Ycoordinate));
            Assert.True(own.Bounds.Contains((double)access.Xcoordinate, (double)access.Ycoordinate)
                || Math.Abs((double)access.Xcoordinate - own.Bounds.Left) < 0.01
                || Math.Abs((double)access.Xcoordinate - own.Bounds.Right) < 0.01
                || Math.Abs((double)access.Ycoordinate - own.Bounds.Top) < 0.01
                || Math.Abs((double)access.Ycoordinate - own.Bounds.Bottom) < 0.01);
        }

        var byId = graph.Nodes.ToDictionary(node => node.Id);
        foreach (var edge in graph.Edges)
        {
            var from = byId[edge.FromNodeId];
            var to = byId[edge.ToNodeId];
            var ignore = from.SlotCode ?? to.SlotCode;
            Assert.DoesNotContain(obstacles.Where(item => item.SlotCode != ignore),
                item => LayoutBlockGeometry.SegmentIntersects(
                    (double)from.Xcoordinate, (double)from.Ycoordinate,
                    (double)to.Xcoordinate, (double)to.Ycoordinate, item.Bounds));
            Assert.Equal(LayoutDistance.Between(from, to, 10), edge.Distance);
        }
    }

    [Fact]
    public void Builder_MarksNarrowAndUnreachableSlots()
    {
        var layoutId = Guid.NewGuid();
        var blockId = Guid.NewGuid();
        var block = new LayoutBlock
        {
            Id = blockId, LayoutId = layoutId, Name = "Tight", Type = "Zone",
            X = 10, Y = 10, Width = 80, Height = 80,
            ConfigJson = """{"boothWidth":40,"boothHeight":40,"horizontalGap":2,"verticalGap":2,"innerPadding":2,"columns":2,"rows":1,"physical":true}"""
        };
        var slots = new[]
        {
            Slot(layoutId, blockId, "T-01", 0, 0, 32, 32),
            Slot(layoutId, blockId, "T-02", 0, 1, 74, 32)
        };
        var graph = NavigationGraphBuilder.Build(
            layoutId, 120, 120, [block], slots,
            10, 0.40, 0.80, DateTime.UtcNow);

        Assert.True(graph.IsValid);
        Assert.Contains("T-01", graph.NarrowCorridorSlotCodes);
        Assert.Contains("T-02", graph.UnreachableSlotCodes);
    }

    [Fact]
    public void Builder_SupportsMultipleEntrancesAndArbitraryBoothCounts()
    {
        var layoutId = Guid.NewGuid();
        var blockId = Guid.NewGuid();
        var block = new LayoutBlock
        {
            Id = blockId, LayoutId = layoutId, Name = "A", Type = "Zone",
            X = 20, Y = 40, Width = 132, Height = 90,
            ConfigJson = """{"boothWidth":30,"boothHeight":30,"horizontalGap":12,"verticalGap":12,"innerPadding":12,"columns":3,"rows":2,"physical":true}"""
        };
        var slots = new[]
        {
            Slot(layoutId, blockId, "A-01", 0, 0, 47, 67),
            Slot(layoutId, blockId, "A-02", 0, 1, 89, 67),
            Slot(layoutId, blockId, "A-03", 0, 2, 131, 67),
            Slot(layoutId, blockId, "A-04", 1, 0, 47, 109),
            Slot(layoutId, blockId, "A-05", 1, 1, 89, 109),
            Slot(layoutId, blockId, "A-06", 1, 2, 131, 109)
        };
        var gates = new[]
        {
            new LayoutNode
            {
                Id = Guid.NewGuid(), LayoutId = layoutId, NodeType = LayoutNodeType.Entrance, NodeName = "North",
                Xcoordinate = 89, Ycoordinate = 16, IsAccessible = true, IsStartingPoint = true
            },
            new LayoutNode
            {
                Id = Guid.NewGuid(), LayoutId = layoutId, NodeType = LayoutNodeType.Entrance, NodeName = "East",
                Xcoordinate = 170, Ycoordinate = 88, IsAccessible = true, IsStartingPoint = true
            }
        };

        var graph = NavigationGraphBuilder.Build(
            layoutId, 220, 200, [block], [.. slots, .. gates],
            10, 0.40, 0.80, DateTime.UtcNow);

        Assert.True(graph.IsValid);
        Assert.Equal(6, graph.AccessBySlotCode.Count);
        Assert.Empty(graph.UnreachableSlotCodes);
        Assert.Contains(graph.Nodes, node => node.Id == gates[0].Id);
        Assert.Contains(graph.Nodes, node => node.Id == gates[1].Id);
        Assert.Contains(graph.Edges, edge => edge.FromNodeId == gates[0].Id || edge.ToNodeId == gates[0].Id);
        Assert.Contains(graph.Edges, edge => edge.FromNodeId == gates[1].Id || edge.ToNodeId == gates[1].Id);
    }

    [Fact]
    public void Dijkstra_FollowsDerivedCorridorAndDoesNotTraverseBoothCenters()
    {
        var (layoutId, block, slots, gate) = StandardFixture();
        var graph = NavigationGraphBuilder.Build(
            layoutId, 200, 200, [block], [.. slots, gate],
            10, 0.40, 0.80, DateTime.UtcNow);
        Assert.True(graph.TryGetAccess("A-04", out var access));
        var solution = new DijkstraIndoorRouteSolver().Solve(
            graph.Nodes, graph.Edges, gate.Id, access.Id, new IndoorRoutePolicy());
        Assert.True(solution.Found);
        Assert.DoesNotContain(solution.NodeIds, id => slots.Any(slot => slot.Id == id));
        Assert.Contains(access.Id, solution.NodeIds);
    }

    [Fact]
    public void Dijkstra_UnreachableDestinationReturnsFailure()
    {
        var layoutId = Guid.NewGuid();
        var blockId = Guid.NewGuid();
        var isolatedBlock = new LayoutBlock
        {
            Id = blockId, LayoutId = layoutId, Name = "Isolated", Type = "Zone",
            X = 10, Y = 10, Width = 50, Height = 50,
            ConfigJson = """{"boothWidth":40,"boothHeight":40,"horizontalGap":2,"verticalGap":2,"innerPadding":2,"columns":1,"rows":1,"physical":true}"""
        };
        var slot = Slot(layoutId, blockId, "Z-01", 0, 0, 32, 32);
        var gate = new LayoutNode
        {
            Id = Guid.NewGuid(), LayoutId = layoutId, NodeType = LayoutNodeType.Entrance, NodeName = "Gate",
            Xcoordinate = 32, Ycoordinate = 32, IsAccessible = true, IsStartingPoint = true
        };
        var graph = NavigationGraphBuilder.Build(
            layoutId, 220, 220, [isolatedBlock], [slot, gate],
            10, 0.40, 0.80, DateTime.UtcNow);
        Assert.Contains("Z-01", graph.UnreachableSlotCodes);
        Assert.True(graph.TryGetAccess("Z-01", out var access));
        var solution = new DijkstraIndoorRouteSolver().Solve(
            graph.Nodes, graph.Edges, gate.Id, access.Id, new IndoorRoutePolicy());
        Assert.False(solution.Found);
    }

    [Fact]
    public void LayoutDistance_ConvertsPixelsWithPixelsPerMeter()
    {
        Assert.Equal(3m, LayoutDistance.Between(0, 0, 30, 0, 10));
        Assert.Equal(30m, LayoutDistance.Between(0, 0, 30, 0, (double?)null));
    }

    [Fact]
    public void Calibration_UsesIndependentPhysicalWidthAndLength()
    {
        var layout = new MarketLayout { Width = 1200, Height = 1090, MarketWidthMeters = 120, MarketLengthMeters = 109 };
        var scale = LayoutPhysicalCalibration.TryResolve(layout);
        Assert.NotNull(scale);
        Assert.Equal("MarketLayoutPhysical", scale!.Value.Source);
        Assert.Equal(0.1d, scale.Value.ScaleX, 12);
        Assert.Equal(0.1d, scale.Value.ScaleY, 12);
        Assert.True(scale.Value.IsUniform);
        Assert.Equal(42.01m, decimal.Round(LayoutDistance.Between(620, 20, 200, 10, scale), 2));
        Assert.Equal(8.73m, decimal.Round(LayoutDistance.Between(200, 10, 141.67, 75, scale), 2));
    }

    [Fact]
    public void Calibration_FallsBackToNightMarketBoundaryAndStaysUncalibratedWithoutPhysicalData()
    {
        var layout = new MarketLayout { Width = 1200, Height = 1090 };
        Assert.Null(LayoutPhysicalCalibration.TryResolve(layout));
        var scale = LayoutPhysicalCalibration.TryResolve(layout, new NightMarket
        {
            BoundaryWidthMeters = 120, BoundaryHeightMeters = 109
        });
        Assert.NotNull(scale);
        Assert.Equal("NightMarketBoundary", scale!.Value.Source);
        Assert.Equal(0.1d, scale.Value.ScaleX, 12);
        Assert.Equal(0.1d, scale.Value.ScaleY, 12);
    }

    [Fact]
    public void Calibration_DoesNotAssumeEqualAxes()
    {
        var scale = LayoutPhysicalCalibration.TryResolve(new MarketLayout
        {
            Width = 100, Height = 200, MarketWidthMeters = 10, MarketLengthMeters = 40
        });
        Assert.NotNull(scale);
        Assert.False(scale!.Value.IsUniform);
        Assert.Equal(0.1d, scale.Value.ScaleX, 12);
        Assert.Equal(0.2d, scale.Value.ScaleY, 12);
        Assert.Equal(2.24m, decimal.Round(LayoutDistance.Between(0, 0, 10, 10, scale), 2));
    }

    [Fact]
    public async Task FindRoute_UsesTransientGraphWithoutRewritingPersistedStarEdges()
    {
        await using var db = new SNMDbContext(new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var marketId = Guid.NewGuid();
        var layoutId = Guid.NewGuid();
        var blockId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var junctionId = Guid.NewGuid();
        var gateId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        db.NightMarkets.Add(new NightMarket
        {
            Id = marketId, Name = "Market", Address = "Test", Status = NightMarketStatus.Active,
            ModerationStatus = ModerationStatus.Active
        });
        db.MarketLayouts.Add(new MarketLayout
        {
            Id = layoutId, NightMarketId = marketId, LayoutName = "Live", Version = 2, GraphRevision = 4,
            Width = 200, Height = 160, Status = MarketLayoutStatus.Active, PixelsPerMeter = 10,
            DistanceCalibrationStatus = DistanceCalibrationStatus.Calibrated
        });
        db.LayoutBlocks.Add(new LayoutBlock
        {
            Id = blockId, LayoutId = layoutId, Name = "Zone A", Type = "Zone",
            X = 20, Y = 40, Width = 90, Height = 90,
            ConfigJson = """{"boothWidth":30,"boothHeight":30,"horizontalGap":12,"verticalGap":12,"innerPadding":12,"columns":2,"rows":2,"physical":true}"""
        });
        db.LayoutNodes.AddRange(
            new LayoutNode
            {
                Id = gateId, LayoutId = layoutId, NodeType = LayoutNodeType.Entrance, NodeName = "Main Entrance",
                Xcoordinate = 68, Ycoordinate = 16, IsAccessible = true, IsStartingPoint = true
            },
            new LayoutNode
            {
                Id = junctionId, LayoutId = layoutId, LayoutBlockId = blockId, NodeType = LayoutNodeType.Junction,
                NodeName = "Aisle", Xcoordinate = 68, Ycoordinate = 10, IsAccessible = true
            },
            new LayoutNode
            {
                Id = slotId, LayoutId = layoutId, LayoutBlockId = blockId, NodeType = LayoutNodeType.BoothSlot,
                SlotCode = "A-01", NodeName = "A-01", RowIndex = 0, ColumnIndex = 0,
                Xcoordinate = 47, Ycoordinate = 67, IsAccessible = true
            },
            Slot(layoutId, blockId, "A-02", 0, 1, 89, 67),
            Slot(layoutId, blockId, "A-03", 1, 0, 47, 109),
            Slot(layoutId, blockId, "A-04", 1, 1, 89, 109));
        db.LayoutEdges.AddRange(
            new LayoutEdge { Id = Guid.NewGuid(), LayoutId = layoutId, FromNodeId = gateId, ToNodeId = junctionId, Distance = 6, IsAccessible = true, IsBidirectional = true },
            new LayoutEdge { Id = Guid.NewGuid(), LayoutId = layoutId, FromNodeId = junctionId, ToNodeId = slotId, Distance = 60, IsAccessible = true, IsBidirectional = true });
        db.Booths.Add(new Booth { Id = boothId, NightMarketId = marketId, BoothName = "Pho", Status = BoothStatus.Active });
        db.BoothLocations.Add(new BoothLocation
        {
            Id = Guid.NewGuid(), BoothId = boothId, LayoutId = layoutId, LayoutNodeId = slotId,
            SlotNumber = "A-01", Xcoordinate = 47, Ycoordinate = 67
        });
        await db.SaveChangesAsync();

        var mapper = new MapperConfiguration(configuration => configuration.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var route = await new MapNavigationService(
            new NightMarketRepository(db), new MarketLayoutRepository(db), new ZoneRepository(db),
            new LayoutNodeRepository(db), new LayoutEdgeRepository(db), new BoothLocationRepository(db),
            new BoothRepository(db), mapper).FindRouteToBoothAsync(layoutId, gateId, boothId);

        Assert.NotNull(route.Data);
        Assert.DoesNotContain(route.Data!.Path, point => point.NodeId == slotId);
        Assert.True(route.Data.IsDistanceCalibrated);
        Assert.NotNull(route.Data.TotalDistanceMeters);
        Assert.True(route.Data.TotalDistanceMeters > 0);
        Assert.True(route.Data.TotalDistanceMeters < 30m);
        Assert.Equal("PixelsPerMeter", route.Data.DistanceScaleSource);
        Assert.Equal(2, await db.LayoutEdges.CountAsync());
        Assert.Equal(6, await db.LayoutNodes.CountAsync());
        Assert.Equal(4, (await db.MarketLayouts.SingleAsync()).GraphRevision);
    }

    [Fact]
    public async Task FindRoute_InvalidGeometry_DoesNotMutateLayout()
    {
        await using var db = new SNMDbContext(new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var marketId = Guid.NewGuid();
        var layoutId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var gateId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        db.NightMarkets.Add(new NightMarket
        {
            Id = marketId, Name = "Market", Address = "Test", Status = NightMarketStatus.Active,
            ModerationStatus = ModerationStatus.Active
        });
        db.MarketLayouts.Add(new MarketLayout
        {
            Id = layoutId, NightMarketId = marketId, LayoutName = "Live", Version = 1, GraphRevision = 1,
            Width = 200, Height = 160, Status = MarketLayoutStatus.Active
        });
        db.LayoutNodes.AddRange(
            new LayoutNode
            {
                Id = gateId, LayoutId = layoutId, NodeType = LayoutNodeType.Entrance, NodeName = "Gate",
                Xcoordinate = 10, Ycoordinate = 10, IsAccessible = true, IsStartingPoint = true
            },
            new LayoutNode
            {
                Id = slotId, LayoutId = layoutId, NodeType = LayoutNodeType.BoothSlot, SlotCode = "A-01",
                NodeName = "A-01", Xcoordinate = 40, Ycoordinate = 40, IsAccessible = true
            });
        db.Booths.Add(new Booth { Id = boothId, NightMarketId = marketId, BoothName = "Pho", Status = BoothStatus.Active });
        db.BoothLocations.Add(new BoothLocation
        {
            Id = Guid.NewGuid(), BoothId = boothId, LayoutId = layoutId, LayoutNodeId = slotId, SlotNumber = "A-01"
        });
        await db.SaveChangesAsync();

        var mapper = new MapperConfiguration(configuration => configuration.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var error = await Assert.ThrowsAsync<AppException>(() => new MapNavigationService(
            new NightMarketRepository(db), new MarketLayoutRepository(db), new ZoneRepository(db),
            new LayoutNodeRepository(db), new LayoutEdgeRepository(db), new BoothLocationRepository(db),
            new BoothRepository(db), mapper).FindRouteToBoothAsync(layoutId, gateId, boothId));

        Assert.Equal("NAVIGATION_GEOMETRY_INVALID", error.ErrorCode);
        Assert.Equal(0, await db.LayoutBlocks.CountAsync());
        Assert.Equal(2, await db.LayoutNodes.CountAsync());
    }

    private static (Guid LayoutId, LayoutBlock Block, LayoutNode[] Slots, LayoutNode Gate) StandardFixture()
    {
        var layoutId = Guid.NewGuid();
        var blockId = Guid.NewGuid();
        var block = new LayoutBlock
        {
            Id = blockId, LayoutId = layoutId, Name = "Zone A", Type = "Zone",
            X = 20, Y = 40, Width = 90, Height = 90,
            ConfigJson = """{"boothWidth":30,"boothHeight":30,"horizontalGap":12,"verticalGap":12,"innerPadding":12,"columns":2,"rows":2,"physical":true}"""
        };
        var slots = new[]
        {
            Slot(layoutId, blockId, "A-01", 0, 0, 47, 67),
            Slot(layoutId, blockId, "A-02", 0, 1, 89, 67),
            Slot(layoutId, blockId, "A-03", 1, 0, 47, 109),
            Slot(layoutId, blockId, "A-04", 1, 1, 89, 109)
        };
        var gate = new LayoutNode
        {
            Id = Guid.NewGuid(), LayoutId = layoutId, NodeType = LayoutNodeType.Entrance, NodeName = "Main Entrance",
            Xcoordinate = 68, Ycoordinate = 16, IsAccessible = true, IsStartingPoint = true
        };
        return (layoutId, block, slots, gate);
    }

    private static LayoutNode Slot(Guid layoutId, Guid blockId, string code, int row, int column, double x, double y)
        => new()
        {
            Id = Guid.NewGuid(), LayoutId = layoutId, LayoutBlockId = blockId, NodeType = LayoutNodeType.BoothSlot,
            SlotCode = code, NodeName = code, RowIndex = row, ColumnIndex = column,
            Xcoordinate = (decimal)x, Ycoordinate = (decimal)y, IsAccessible = true
        };
}

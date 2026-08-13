using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.MarketLayouts;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class LayoutGeneratorHubAndSpokeRegressionTests
{
    [Fact]
    public void ComputeGeneration_StillProducesOneJunctionPerZoneAndStarEdges()
    {
        var layout = new MarketLayout
        {
            Id = Guid.NewGuid(),
            LayoutName = "Draft",
            Width = 800,
            Height = 600,
            Status = MarketLayoutStatus.Draft
        };
        var zoneA = new Zone { Id = Guid.NewGuid(), ZoneName = "Zone A", ZoneCode = "A", Capacity = 4 };
        var zoneB = new Zone { Id = Guid.NewGuid(), ZoneName = "Zone B", ZoneCode = "B", Capacity = 4 };
        var request = new GenerateLayoutRequest
        {
            ZoneConfigs =
            [
                new ZoneGenerationConfig { ZoneId = zoneA.Id, Columns = 2, BoothWidth = 80, BoothHeight = 60, Gap = 20 },
                new ZoneGenerationConfig { ZoneId = zoneB.Id, Columns = 2, BoothWidth = 80, BoothHeight = 60, Gap = 20 }
            ],
            ZonesPerRow = 2,
            AutoExpandCanvas = true
        };

        var result = new LayoutGeneratorService().ComputeGeneration(
            layout, [zoneA, zoneB], [], [], request);

        Assert.Equal(2, result.Blocks.Count);
        var junctions = result.Nodes.Where(node => node.NodeType == LayoutNodeType.Junction).ToList();
        var slots = result.Nodes.Where(node => node.NodeType == LayoutNodeType.BoothSlot).ToList();
        Assert.Equal(2, junctions.Count);
        Assert.Equal(8, slots.Count);
        Assert.DoesNotContain(result.Nodes, node => node.NodeType == LayoutNodeType.BoothAccess);
        Assert.Contains(result.Nodes, node => node.NodeType == LayoutNodeType.Entrance);

        foreach (var slot in slots)
        {
            var zoneJunction = junctions.Single(junction =>
                junction.LayoutBlockId == slot.LayoutBlockId || junction.ZoneId == slot.ZoneId);
            Assert.Contains(result.Edges, edge =>
                (edge.FromNodeId == slot.Id && edge.ToNodeId == zoneJunction.Id)
                || (edge.ToNodeId == slot.Id && edge.FromNodeId == zoneJunction.Id));
        }

        Assert.Contains(junctions, junction => junction.NodeName == "Zone A Aisle");
        Assert.Contains(junctions, junction => junction.NodeName == "Zone B Aisle");
        Assert.True(junctions.All(junction =>
            (double)junction.Ycoordinate < result.Blocks.First(block => block.Id == junction.LayoutBlockId).Y));
    }
}

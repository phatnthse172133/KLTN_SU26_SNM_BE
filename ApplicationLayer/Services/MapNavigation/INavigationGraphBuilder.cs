using ApplicationLayer.Services.MarketLayouts;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.MapNavigation;

public interface INavigationGraphBuilder
{
    /// <summary>
    /// Derives a transient walkable graph from persisted layout geometry.
    /// Never mutates the input layout, blocks, or nodes.
    /// </summary>
    NavigationGraph Build(
        MarketLayout layout,
        IReadOnlyCollection<LayoutBlock> blocks,
        IReadOnlyCollection<LayoutNode> persistedNodes,
        LayoutPhysicalScale? physicalScale = null);
}

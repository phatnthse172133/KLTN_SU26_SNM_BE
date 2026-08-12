using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.MarketLayouts;

/// <summary>
/// Core grid generator algorithm — runs entirely on the Backend to prevent FE/BE drift.
/// </summary>
public interface ILayoutGeneratorService
{
    /// <summary>
    /// Compute preview data without touching the database.
    /// </summary>
    GenerationPreviewResponse ComputePreview(
        MarketLayout layout,
        IReadOnlyCollection<Zone> zones,
        IReadOnlyCollection<LayoutNode> existingNodes,
        IReadOnlyCollection<BoothLocation> assignedLocations,
        GenerateLayoutRequest request);

    /// <summary>
    /// Compute the final Zone blocks, slot nodes, and walkway edges to be persisted.
    /// Returns null on the same data as preview (caller checks CanApply before invoking).
    /// </summary>
    GenerationResult ComputeGeneration(
        MarketLayout layout,
        IReadOnlyCollection<Zone> zones,
        IReadOnlyCollection<LayoutNode> existingNodes,
        IReadOnlyCollection<BoothLocation> assignedLocations,
        GenerateLayoutRequest request);
}

public record GenerationResult(
    IReadOnlyList<LayoutBlock> Blocks,
    IReadOnlyList<LayoutNode> Nodes,
    IReadOnlyList<LayoutEdge> Edges,
    int NewCanvasWidth,
    int NewCanvasHeight,
    GenerationPreviewResponse Preview);

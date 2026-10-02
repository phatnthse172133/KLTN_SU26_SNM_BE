using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.MarketLayouts;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketMaps;

public sealed partial class MarketMapService
{
    /// <summary>
    /// Read-only source catalogue for the composition workflow. Selectability
    /// mirrors <c>CreateDraftAsync</c>/<c>CloneToDraftAsync</c> so the UI can
    /// disable cards with a reason instead of failing on save.
    /// </summary>
    public async Task<ApiResponse<MarketMapCompositionSourcesResponse>> GetCompositionSourcesAsync(
        Guid nightMarketId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var market = await EnsureOwnedMarketAsync(nightMarketId, actorId, cancellationToken);
        var layouts = await _layouts.GetCompositionCatalogAsync(nightMarketId, cancellationToken);
        var maps = await _marketMaps.GetByMarketIdAsync(nightMarketId, cancellationToken);
        var blocks = layouts.Count == 0
            ? []
            : await _layouts.GetBlocksByLayoutIdsAsync(
                layouts.Select(layout => layout.Id).ToArray(), cancellationToken);
        var zoneCountByLayout = blocks
            .Where(IsZoneBlock)
            .GroupBy(block => block.LayoutId)
            .ToDictionary(group => group.Key, group => group.Count());

        var usageBySource = layouts
            .Where(layout => layout.BasedOnLayoutId.HasValue && !IsLegacyMap(layout.MarketMap))
            .GroupBy(layout => layout.BasedOnLayoutId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<CompositionSourceUsageResponse>)group
                    .OrderByDescending(clone => clone.MarketMap.Version)
                    .Select(clone => new CompositionSourceUsageResponse
                    {
                        MarketMapId = clone.MarketMapId,
                        MarketMapName = clone.MarketMap.Name,
                        MarketMapVersion = clone.MarketMap.Version,
                        MarketMapStatus = clone.MarketMap.Status.ToString(),
                        CloneLayoutId = clone.Id
                    })
                    .ToArray());

        var response = new MarketMapCompositionSourcesResponse
        {
            MaxLayoutsPerMarket = await _entitlements.GetMaxLayoutsPerMarketAsync(actorId),
            MaxSlotsPerMarket = await _entitlements.GetMaxSlotsPerMarketAsync(actorId),
            MarketBoundaryWidthMeters = market.BoundaryWidthMeters,
            MarketBoundaryHeightMeters = market.BoundaryHeightMeters,
            Maps = maps
                .Where(map => !IsLegacyMap(map))
                .Select(map => new MarketMapSummaryResponse
                {
                    Id = map.Id,
                    Name = map.Name,
                    Version = map.Version,
                    Status = map.Status.ToString(),
                    CreatedAt = map.CreatedAt,
                    PublishedAt = map.PublishedAt,
                    UpdatedAt = map.UpdatedAt,
                    LayoutCount = map.MarketLayouts.Count(layout => !layout.IsDeleted)
                })
                .ToArray(),
            Layouts = layouts.Select(layout =>
            {
                var dimensions = TryResolvePhysicalDimensions(layout, market);
                var slotCount = CountBoothSlots(layout);
                var (code, reason) = ResolveIneligibility(layout, dimensions, slotCount, market);
                return new CompositionSourceLayoutResponse
                {
                    LayoutId = layout.Id,
                    LayoutName = layout.LayoutName,
                    SectionCode = layout.SectionCode,
                    SectionName = layout.SectionName,
                    Version = layout.Version,
                    Status = layout.Status.ToString(),
                    Width = layout.Width,
                    Height = layout.Height,
                    PhysicalWidthMeters = dimensions?.Width,
                    PhysicalHeightMeters = dimensions?.Height,
                    OffsetXMeters = layout.OffsetXMeters,
                    OffsetYMeters = layout.OffsetYMeters,
                    ZoneCount = zoneCountByLayout.GetValueOrDefault(layout.Id),
                    SlotCount = slotCount,
                    GateCount = layout.LayoutNodes.Count(node =>
                        !node.IsDeleted && node.NodeType == LayoutNodeType.Entrance),
                    BasedOnLayoutId = layout.BasedOnLayoutId,
                    MarketMapId = layout.MarketMapId,
                    MarketMapName = layout.MarketMap.Name,
                    MarketMapVersion = layout.MarketMap.Version,
                    MarketMapStatus = layout.MarketMap.Status.ToString(),
                    IsStandalone = IsLegacyMap(layout.MarketMap),
                    IsSelectable = code is null,
                    IneligibleCode = code,
                    IneligibleReason = reason,
                    UsedBy = usageBySource.GetValueOrDefault(layout.Id) ?? []
                };
            }).ToArray()
        };

        return ApiResponse<MarketMapCompositionSourcesResponse>.SuccessResponse(response);
    }

    private static bool IsLegacyMap(MarketMap map)
        => map.Status == MarketMapStatus.Draft &&
           map.Name.Equals(MarketMap.LegacyDraftName, StringComparison.OrdinalIgnoreCase);

    private static (string? Code, string? Reason) ResolveIneligibility(
        MarketLayout layout, PhysicalDimensions? dimensions, int slotCount, NightMarket market)
    {
        var isCloneable = layout.Status is MarketLayoutStatus.Active or MarketLayoutStatus.Inactive
            || (layout.Status == MarketLayoutStatus.Draft && IsLegacyMap(layout.MarketMap));
        if (!isCloneable)
        {
            return layout.Status == MarketLayoutStatus.Draft
                ? ("COMPOSITION_DRAFT_CHILD", "This layout is a copy inside a draft market map and cannot be used as a source.")
                : ("LAYOUT_NOT_SELECTABLE", $"A layout in {layout.Status} status cannot be used as a source.");
        }

        if (!dimensions.HasValue)
            return ("LAYOUT_PHYSICAL_SIZE_UNAVAILABLE", "Layout physical width and height cannot be resolved in meters.");
        if (slotCount == 0)
            return ("LAYOUT_NOT_GENERATED", "Layout has no booth slots yet.");
        if ((market.BoundaryWidthMeters is > 0 &&
             GeometryTolerance.Exceeds(dimensions.Value.Width, market.BoundaryWidthMeters.Value)) ||
            (market.BoundaryHeightMeters is > 0 &&
             GeometryTolerance.Exceeds(dimensions.Value.Height, market.BoundaryHeightMeters.Value)))
            return ("LAYOUT_LARGER_THAN_MARKET", "Layout is larger than the night market boundary.");

        return (null, null);
    }
}

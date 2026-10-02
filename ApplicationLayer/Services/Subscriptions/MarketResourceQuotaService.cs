using ApplicationLayer.Exceptions;
using DomainLayer.InterfaceRepository;
using DomainLayer.InterfaceRepositories;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Subscriptions;

public sealed record MarketMapCompositionQuotaAssessment(
    int LayoutCount,
    int? LayoutLimit,
    int SlotCount,
    int SlotLimit)
{
    public bool LayoutLimitExceeded => LayoutLimit.HasValue && LayoutCount > LayoutLimit.Value;
    public bool SlotLimitExceeded => SlotCount > SlotLimit;
}

public interface IMarketResourceQuotaService
{
    Task<MarketMapCompositionQuotaAssessment> AssessMapCompositionAsync(
        Guid marketOwnerId, int layoutCount, int slotCount,
        CancellationToken cancellationToken = default);

    Task EnsureMapCompositionCapacityAsync(
        Guid marketOwnerId, int layoutCount, int slotCount,
        CancellationToken cancellationToken = default);

    Task EnsureCanAddLayoutsAsync(
        Guid marketOwnerId, Guid marketMapId, int additionalLayouts,
        CancellationToken cancellationToken = default);

    Task EnsureCanAddBoothSlotsAsync(
        Guid marketOwnerId, Guid marketMapId, int additionalSlots,
        CancellationToken cancellationToken = default, Guid? layoutId = null);

    Task EnsureBoothSlotCapacityAsync(
        Guid marketOwnerId, Guid marketMapId, Guid layoutId, int proposedLayoutSlotCount,
        CancellationToken cancellationToken = default);

    Task<string?> GetBoothSlotCapacityErrorAsync(
        Guid marketOwnerId, Guid marketMapId, Guid layoutId, int proposedLayoutSlotCount,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Owns package quota semantics for one Draft/Active overall map composition.
/// Slot capacity is the sum of physical BoothSlot nodes across its sections.
/// </summary>
public sealed class MarketResourceQuotaService : IMarketResourceQuotaService
{
    private readonly ISubscriptionEntitlementService _entitlements;
    private readonly IMarketLayoutRepository _layouts;
    private readonly ILayoutNodeRepository _nodes;
    private readonly IMarketMapRepository _maps;

    public MarketResourceQuotaService(
        ISubscriptionEntitlementService entitlements,
        IMarketLayoutRepository layouts,
        ILayoutNodeRepository nodes, IMarketMapRepository maps)
        => (_entitlements, _layouts, _nodes, _maps) = (entitlements, layouts, nodes, maps);

    private async Task<bool> IsStandaloneLibraryAsync(Guid mapId, CancellationToken cancellationToken)
    {
        var map = await _maps.GetManagementDetailAsync(mapId, cancellationToken);
        return map?.Status == MarketMapStatus.Draft &&
            map.Name.Equals(MarketMap.LegacyDraftName, StringComparison.OrdinalIgnoreCase);
    }

    public async Task EnsureMapCompositionCapacityAsync(
        Guid marketOwnerId, int layoutCount, int slotCount,
        CancellationToken cancellationToken = default)
    {
        var assessment = await AssessMapCompositionAsync(
            marketOwnerId, layoutCount, slotCount, cancellationToken);
        if (assessment.LayoutLimitExceeded)
            throw AppException.Forbidden(
                $"Your current subscription allows a maximum of {assessment.LayoutLimit!.Value} layout section(s) per market map.",
                "LAYOUT_LIMIT_REACHED");

        if (assessment.SlotLimitExceeded)
            throw AppException.Forbidden(
                $"This market map would contain {slotCount} booth slots, but your current subscription allows a maximum of {assessment.SlotLimit}.",
                "PLAN_LIMIT_REACHED");
    }

    public async Task<MarketMapCompositionQuotaAssessment> AssessMapCompositionAsync(
        Guid marketOwnerId, int layoutCount, int slotCount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var layoutLimit = await _entitlements.GetMaxLayoutsPerMarketAsync(marketOwnerId);
        var slotLimit = await _entitlements.GetMaxSlotsPerMarketAsync(marketOwnerId);
        return new(layoutCount, layoutLimit, slotCount, slotLimit);
    }

    public async Task EnsureCanAddLayoutsAsync(
        Guid marketOwnerId, Guid marketMapId, int additionalLayouts,
        CancellationToken cancellationToken = default)
    {
        if (additionalLayouts <= 0) return;
        var limit = await _entitlements.GetMaxLayoutsPerMarketAsync(marketOwnerId);
        if (!limit.HasValue) return;

        var used = await _layouts.CountQuotaRelevantLayoutsAsync(marketMapId, cancellationToken);
        if (used + additionalLayouts > limit.Value)
            throw AppException.Forbidden(
                $"Your current subscription allows a maximum of {limit.Value} layout section(s) per market.",
                "LAYOUT_LIMIT_REACHED");
    }

    public async Task EnsureCanAddBoothSlotsAsync(
        Guid marketOwnerId, Guid marketMapId, int additionalSlots,
        CancellationToken cancellationToken = default, Guid? layoutId = null)
    {
        if (additionalSlots <= 0) return;
        var used = await _nodes.CountBoothSlotsByMarketMapAsync(
            marketMapId, cancellationToken: cancellationToken);
        if (layoutId.HasValue && await IsStandaloneLibraryAsync(marketMapId, cancellationToken))
            used = (await _nodes.GetByLayoutAsync(layoutId.Value, cancellationToken: cancellationToken))
                .Count(node => !node.IsDeleted && node.NodeType == LayoutNodeType.BoothSlot);
        var error = await GetCapacityErrorAsync(marketOwnerId, checked(used + additionalSlots));
        if (error is not null)
            throw AppException.Forbidden(error, "PLAN_LIMIT_REACHED");
    }

    public async Task EnsureBoothSlotCapacityAsync(
        Guid marketOwnerId, Guid marketMapId, Guid layoutId, int proposedLayoutSlotCount,
        CancellationToken cancellationToken = default)
    {
        var error = await GetBoothSlotCapacityErrorAsync(
            marketOwnerId, marketMapId, layoutId, proposedLayoutSlotCount, cancellationToken);
        if (error is not null)
            throw AppException.Forbidden(error, "PLAN_LIMIT_REACHED");
    }

    public async Task<string?> GetBoothSlotCapacityErrorAsync(
        Guid marketOwnerId, Guid marketMapId, Guid layoutId, int proposedLayoutSlotCount,
        CancellationToken cancellationToken = default)
    {
        var otherSlots = await IsStandaloneLibraryAsync(marketMapId, cancellationToken)
            ? 0
            : await _nodes.CountBoothSlotsByMarketMapAsync(marketMapId, layoutId, cancellationToken);
        return await GetCapacityErrorAsync(
            marketOwnerId, checked(otherSlots + proposedLayoutSlotCount));
    }

    private async Task<string?> GetCapacityErrorAsync(Guid marketOwnerId, int proposedMapSlotCount)
    {
        var limit = await _entitlements.GetMaxSlotsPerMarketAsync(marketOwnerId);
        return proposedMapSlotCount > limit
            ? $"This market map would contain {proposedMapSlotCount} booth slots, but your current subscription allows a maximum of {limit}."
            : null;
    }
}

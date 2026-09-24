using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Subscriptions;
using ApplicationLayer.Services.MarketLayouts;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepositories;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketMaps;

/// <summary>
/// Draft MarketMap management workflow. This service composes, arranges,
/// previews, and validates drafts; it must not invoke the legacy layout
/// activation flow or change the active MarketMap.
/// </summary>
public sealed partial class MarketMapService : IMarketMapService
{
    private readonly IMarketMapRepository _marketMaps;
    private readonly IMarketLayoutRepository _layouts;
    private readonly INightMarketRepository _nightMarkets;
    private readonly ISubscriptionEntitlementService _entitlements;
    private readonly IMarketResourceQuotaService _resourceQuota;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILayoutGraphValidationService _graphValidation;
    private readonly IZoneRepository _zones;
    private readonly ILayoutNavigationAnchorRepository _anchors;
    private readonly IBoothLocationRepository _locations;

    public MarketMapService(
        IMarketMapRepository marketMaps,
        IMarketLayoutRepository layouts,
        INightMarketRepository nightMarkets,
        ISubscriptionEntitlementService entitlements,
        IMarketResourceQuotaService resourceQuota,
        IUnitOfWork unitOfWork,
        ILayoutGraphValidationService graphValidation,
        IZoneRepository zones,
        ILayoutNavigationAnchorRepository anchors,
        IBoothLocationRepository locations)
    {
        _marketMaps = marketMaps;
        _layouts = layouts;
        _nightMarkets = nightMarkets;
        _entitlements = entitlements;
        _resourceQuota = resourceQuota;
        _unitOfWork = unitOfWork;
        _graphValidation = graphValidation;
        _zones = zones;
        _anchors = anchors;
        _locations = locations;
    }

    public async Task<ApiResponse<IReadOnlyCollection<MarketMapSummaryResponse>>> GetAllAsync(
        Guid nightMarketId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var market = await EnsureOwnedMarketAsync(nightMarketId, actorId, cancellationToken);
        var maps = await _marketMaps.GetByMarketIdAsync(nightMarketId, cancellationToken);
        var response = maps.Select(map => new MarketMapSummaryResponse
        {
            Id = map.Id,
            Name = map.Name,
            Version = map.Version,
            Status = map.Status.ToString(),
            CreatedAt = map.CreatedAt,
            PublishedAt = map.PublishedAt,
            UpdatedAt = map.UpdatedAt,
            LayoutCount = map.MarketLayouts.Count(layout => !layout.IsDeleted)
        }).ToArray();

        return ApiResponse<IReadOnlyCollection<MarketMapSummaryResponse>>.SuccessResponse(response);
    }

    public async Task<ApiResponse<MarketMapDetailResponse>> GetByIdAsync(
        Guid nightMarketId, Guid marketMapId, Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var market = await EnsureOwnedMarketAsync(nightMarketId, actorId, cancellationToken);
        var map = await _marketMaps.GetDetailAsync(nightMarketId, marketMapId, cancellationToken)
            ?? throw AppException.NotFound("Market map was not found.", "MARKET_MAP_NOT_FOUND");

        return ApiResponse<MarketMapDetailResponse>.SuccessResponse(ToDetailResponse(map, market));
    }

    public async Task<ApiResponse<IReadOnlyCollection<EligibleMarketLayoutResponse>>> GetEligibleLayoutsAsync(
        Guid nightMarketId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var market = await EnsureOwnedMarketAsync(nightMarketId, actorId, cancellationToken);
        var layouts = await _layouts.GetEligibleCompositionSourcesAsync(nightMarketId, cancellationToken);
        var response = layouts.Select(layout =>
        {
            var dimensions = TryResolvePhysicalDimensions(layout, market);
            return new EligibleMarketLayoutResponse
            {
                LayoutId = layout.Id,
                LayoutName = layout.LayoutName,
                SectionCode = layout.SectionCode,
                SectionName = layout.SectionName,
                Version = layout.Version,
                Width = layout.Width,
                Height = layout.Height,
                PhysicalWidthMeters = dimensions?.Width,
                PhysicalHeightMeters = dimensions?.Height,
                GraphRevision = layout.GraphRevision,
                OffsetX = layout.OffsetXMeters,
                OffsetY = layout.OffsetYMeters,
                SlotCount = CountBoothSlots(layout)
            };
        }).ToArray();

        return ApiResponse<IReadOnlyCollection<EligibleMarketLayoutResponse>>.SuccessResponse(response);
    }

    public async Task<ApiResponse<MarketMapDetailResponse>> CreateDraftAsync(
        Guid nightMarketId, CreateMarketMapDraftRequest request, Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var market = await EnsureOwnedMarketAsync(nightMarketId, actorId, cancellationToken);
        if (!await _entitlements.HasActiveMarketSubscriptionAsync(actorId))
            throw AppException.Forbidden(
                "An active Market subscription is required to perform this action.",
                "MARKET_SUBSCRIPTION_REQUIRED");

        var name = ValidateAndNormalizeRequest(request);
        var requestedIds = request.LayoutIds.ToArray();
        var sources = await LoadAndValidateSourcesAsync(
            nightMarketId, requestedIds, cancellationToken);
        var selectedSlotCount = sources.Sum(CountBoothSlots);
        await _resourceQuota.EnsureMapCompositionCapacityAsync(
            market.MarketOwnerId!.Value, sources.Count, selectedSlotCount, cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _layouts.AcquireMarketLockAsync(nightMarketId, cancellationToken);

            // Re-read after taking the market lock so a stale source selection cannot
            // be composed if a layout lifecycle operation completed concurrently.
            sources = await LoadAndValidateSourcesAsync(
                nightMarketId, requestedIds, cancellationToken);
            selectedSlotCount = sources.Sum(CountBoothSlots);
            await _resourceQuota.EnsureMapCompositionCapacityAsync(
                market.MarketOwnerId.Value, sources.Count, selectedSlotCount, cancellationToken);

            var now = DateTime.UtcNow;
            var map = new MarketMap
            {
                Id = Guid.NewGuid(),
                NightMarketId = nightMarketId,
                Name = name,
                Version = checked(await _marketMaps.GetLatestVersionAsync(
                    nightMarketId, cancellationToken) + 1),
                Status = MarketMapStatus.Draft,
                CreatedAt = now,
                UpdatedAt = now,
                PublishedAt = null
            };

            await _marketMaps.AddAsync(map);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var sourceById = sources.ToDictionary(layout => layout.Id);
            var clonedLayouts = new List<MarketMapLayoutSummaryResponse>(requestedIds.Length);
            foreach (var sourceId in requestedIds)
            {
                var source = sourceById[sourceId];
                var clone = await _layouts.CloneToDraftAsync(
                    source.Id, map.Id, null, now, cancellationToken);
                clonedLayouts.Add(ToLayoutSummary(clone, CountBoothSlots(source), market));
            }

            var response = new MarketMapDetailResponse
            {
                Id = map.Id,
                NightMarketId = map.NightMarketId,
                Name = map.Name,
                Version = map.Version,
                Status = map.Status.ToString(),
                CreatedAt = map.CreatedAt,
                PublishedAt = map.PublishedAt,
                UpdatedAt = map.UpdatedAt,
                Layouts = clonedLayouts
            };

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return ApiResponse<MarketMapDetailResponse>.SuccessResponse(
                response,
                "Market map draft created successfully. The source layouts are unchanged.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<NightMarket> EnsureOwnedMarketAsync(
        Guid nightMarketId, Guid actorId, CancellationToken cancellationToken)
    {
        var market = await _nightMarkets.GetActiveByIdAsync(nightMarketId, cancellationToken)
            ?? throw AppException.NotFound("Night market was not found.", "NIGHT_MARKET_NOT_FOUND");
        if (market.MarketOwnerId != actorId)
            throw AppException.Forbidden(
                "You do not have permission to manage this night market's market maps.",
                "NIGHT_MARKET_OWNERSHIP_REQUIRED");
        return market;
    }

    private async Task<IReadOnlyCollection<MarketLayout>> LoadAndValidateSourcesAsync(
        Guid nightMarketId, IReadOnlyCollection<Guid> requestedIds,
        CancellationToken cancellationToken)
    {
        var sources = await _layouts.GetCompositionCandidatesByIdsAsync(
            requestedIds, cancellationToken);
        var sourceById = sources.ToDictionary(layout => layout.Id);

        var missingId = requestedIds.FirstOrDefault(id => !sourceById.ContainsKey(id));
        if (missingId != Guid.Empty)
            throw AppException.NotFound(
                $"Source layout '{missingId}' was not found.",
                "SOURCE_LAYOUT_NOT_FOUND");

        var foreign = sources.FirstOrDefault(layout => layout.NightMarketId != nightMarketId);
        if (foreign is not null)
            throw AppException.BadRequest(
                $"Layout '{foreign.Id}' belongs to another night market.",
                "SOURCE_LAYOUT_MARKET_MISMATCH");

        var deleted = sources.FirstOrDefault(layout => layout.IsDeleted);
        if (deleted is not null)
            throw AppException.Conflict(
                $"Layout '{deleted.Id}' has been deleted and cannot be used as a source.",
                "SOURCE_LAYOUT_DELETED");

        var ineligible = sources.FirstOrDefault(layout =>
            layout.Status != MarketLayoutStatus.Active &&
            !(layout.Status == MarketLayoutStatus.Draft &&
              layout.MarketMap.Status == MarketMapStatus.Draft &&
              layout.MarketMap.Name.Equals(MarketMap.LegacyDraftName, StringComparison.OrdinalIgnoreCase)));
        if (ineligible is not null)
            throw AppException.Conflict(
                $"Layout '{ineligible.Id}' is {ineligible.Status}; only active layouts or editable layouts from the legacy draft can be selected.",
                "SOURCE_LAYOUT_NOT_ACTIVE");

        var duplicateSection = sources
            .GroupBy(layout => layout.SectionCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSection is not null)
            throw AppException.BadRequest(
                $"Only one version of physical section '{duplicateSection.Key}' can be selected.",
                "DUPLICATE_SECTION_CODE");

        return sources;
    }

    private static string ValidateAndNormalizeRequest(CreateMarketMapDraftRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw AppException.BadRequest("Market map name is required.", "MARKET_MAP_NAME_REQUIRED");
        var name = request.Name.Trim();
        if (name.Length > 150)
            throw AppException.BadRequest(
                "Market map name cannot exceed 150 characters.",
                "MARKET_MAP_NAME_TOO_LONG");
        if (name.Equals(MarketMap.LegacyDraftName, StringComparison.OrdinalIgnoreCase))
            throw AppException.BadRequest(
                "This market map name is reserved for legacy layout compatibility.",
                "MARKET_MAP_NAME_RESERVED");

        if (request.LayoutIds is null || request.LayoutIds.Count == 0)
            throw AppException.BadRequest(
                "Select at least one source layout.",
                "SOURCE_LAYOUT_REQUIRED");
        if (request.LayoutIds.Any(id => id == Guid.Empty))
            throw AppException.BadRequest(
                "Every source layout identifier must be a non-empty GUID.",
                "SOURCE_LAYOUT_ID_INVALID");
        if (request.LayoutIds.Distinct().Count() != request.LayoutIds.Count)
            throw AppException.BadRequest(
                "The source layout selection contains duplicate identifiers.",
                "DUPLICATE_LAYOUT_ID");

        return name;
    }

    private static MarketMapDetailResponse ToDetailResponse(MarketMap map, NightMarket market)
        => new()
        {
            Id = map.Id,
            NightMarketId = map.NightMarketId,
            Name = map.Name,
            Version = map.Version,
            Status = map.Status.ToString(),
            CreatedAt = map.CreatedAt,
            PublishedAt = map.PublishedAt,
            UpdatedAt = map.UpdatedAt,
            Layouts = map.MarketLayouts
                .Where(layout => !layout.IsDeleted)
                .OrderBy(layout => layout.DisplayOrder)
                .ThenBy(layout => layout.SectionName)
                .Select(layout => ToLayoutSummary(layout, CountBoothSlots(layout), market))
                .ToArray()
        };

    private static MarketMapLayoutSummaryResponse ToLayoutSummary(
        MarketLayout layout, int slotCount, NightMarket market)
    {
        var dimensions = TryResolvePhysicalDimensions(layout, market);
        return new()
        {
            Id = layout.Id,
            LayoutName = layout.LayoutName,
            SectionCode = layout.SectionCode,
            SectionName = layout.SectionName,
            Version = layout.Version,
            Status = layout.Status.ToString(),
            Width = layout.Width,
            Height = layout.Height,
            PhysicalWidthMeters = dimensions?.Width,
            PhysicalHeightMeters = dimensions?.Height,
            GraphRevision = layout.GraphRevision,
            OffsetXMeters = layout.OffsetXMeters,
            OffsetYMeters = layout.OffsetYMeters,
            DisplayOrder = layout.DisplayOrder,
            IsDefaultView = layout.IsDefaultView,
            SlotCount = slotCount
        };
    }

    private static int CountBoothSlots(MarketLayout layout)
        => layout.LayoutNodes.Count(node =>
            !node.IsDeleted && node.NodeType == LayoutNodeType.BoothSlot);
}

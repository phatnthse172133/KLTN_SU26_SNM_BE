using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.MarketLayouts;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketMaps;

public sealed partial class MarketMapService
{
    public async Task<ApiResponse<MarketMapDetailResponse>> ArrangeAsync(
        Guid nightMarketId, Guid marketMapId, ArrangeMarketMapRequest request, Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var market = await EnsureOwnedMarketAsync(nightMarketId, actorId, cancellationToken);
        var map = await GetManagedMapAsync(nightMarketId, marketMapId, cancellationToken);
        var placements = ValidateArrangement(map, market, request);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _layouts.AcquireMarketLockAsync(nightMarketId, cancellationToken);
            map = await _marketMaps.GetManagementDetailForUpdateAsync(
                marketMapId, cancellationToken)
                ?? throw AppException.NotFound("Market map was not found.", "MARKET_MAP_NOT_FOUND");
            if (map.NightMarketId != nightMarketId)
                throw AppException.BadRequest(
                    "MarketMap does not belong to the requested night market.",
                    "MARKET_MAP_MARKET_MISMATCH");
            placements = ValidateArrangement(map, market, request);

            var now = DateTime.UtcNow;
            var layoutsById = map.MarketLayouts
                .Where(layout => !layout.IsDeleted)
                .ToDictionary(layout => layout.Id);
            foreach (var placement in placements)
            {
                var layout = layoutsById[placement.LayoutId];
                layout.OffsetXMeters = placement.OffsetXMeters;
                layout.OffsetYMeters = placement.OffsetYMeters;
                layout.DisplayOrder = placement.DisplayOrder;
                layout.UpdatedAt = now;
            }

            map.UpdatedAt = now;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return ApiResponse<MarketMapDetailResponse>.SuccessResponse(
                ToDetailResponse(map, market),
                "Market map arrangement updated successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ApiResponse<MarketMapPreviewResponse>> GetPreviewAsync(
        Guid nightMarketId, Guid marketMapId, Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var market = await EnsureOwnedMarketAsync(nightMarketId, actorId, cancellationToken);
        var map = await GetManagedMapAsync(nightMarketId, marketMapId, cancellationToken);
        var liveLayouts = map.MarketLayouts.Where(layout => !layout.IsDeleted).ToArray();
        var blocksByLayout = await LoadBlocksAsync(liveLayouts, cancellationToken);
        var validation = await BuildValidationAsync(
            map, market, actorId, blocksByLayout, cancellationToken);

        var layoutPreviews = liveLayouts
            .OrderBy(layout => layout.DisplayOrder)
            .ThenBy(layout => layout.SectionName)
            .Select(layout =>
            {
                var dimensions = TryResolvePhysicalDimensions(layout, market);
                var blocks = blocksByLayout[layout.Id];
                return new MarketMapLayoutPreviewResponse
                {
                    LayoutId = layout.Id,
                    LayoutName = layout.LayoutName,
                    SectionCode = layout.SectionCode,
                    SectionName = layout.SectionName,
                    OffsetXMeters = layout.OffsetXMeters,
                    OffsetYMeters = layout.OffsetYMeters,
                    PhysicalWidthMeters = dimensions?.Width,
                    PhysicalHeightMeters = dimensions?.Height,
                    DisplayOrder = layout.DisplayOrder,
                    IsDefaultView = layout.IsDefaultView,
                    ZoneCount = blocks.Count(IsZoneBlock),
                    BoothSlotCount = CountBoothSlots(layout)
                };
            })
            .ToArray();

        var bounds = MarketMapGeometry.CalculateOverallBounds(liveLayouts, market);

        var response = new MarketMapPreviewResponse
        {
            MarketMapId = map.Id,
            Name = map.Name,
            Version = map.Version,
            Status = map.Status.ToString(),
            OverallBounds = bounds,
            LayoutCount = liveLayouts.Length,
            // Zone is a market-level identity and may be referenced by more
            // than one layout snapshot. Count each represented identity once.
            ZoneCount = liveLayouts
                .SelectMany(layout => blocksByLayout[layout.Id]
                    .Select(block => block.ZoneId)
                    .Concat(layout.LayoutNodes.Select(node => node.ZoneId)))
                .OfType<Guid>()
                .Distinct()
                .Count(),
            BoothSlotCount = layoutPreviews.Sum(layout => layout.BoothSlotCount),
            Layouts = layoutPreviews,
            ValidationSummary = new MarketMapValidationSummaryResponse
            {
                IsValid = validation.IsValid,
                CanActivate = validation.CanActivate,
                ErrorCount = validation.Errors.Count,
                WarningCount = validation.Warnings.Count
            }
        };

        return ApiResponse<MarketMapPreviewResponse>.SuccessResponse(response);
    }

    public async Task<ApiResponse<MarketMapValidationResponse>> ValidateAsync(
        Guid nightMarketId, Guid marketMapId, Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var market = await EnsureOwnedMarketAsync(nightMarketId, actorId, cancellationToken);
        var map = await GetManagedMapAsync(nightMarketId, marketMapId, cancellationToken);
        var liveLayouts = map.MarketLayouts.Where(layout => !layout.IsDeleted).ToArray();
        var blocksByLayout = await LoadBlocksAsync(liveLayouts, cancellationToken);
        var result = await BuildValidationAsync(
            map, market, actorId, blocksByLayout, cancellationToken);
        return ApiResponse<MarketMapValidationResponse>.SuccessResponse(
            result,
            result.IsValid ? "Market map is valid." : "Market map validation failed.");
    }

    private async Task<MarketMapValidationResponse> BuildValidationAsync(
        MarketMap map,
        NightMarket market,
        Guid actorId,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<LayoutBlock>> blocksByLayout,
        CancellationToken cancellationToken)
    {
        var errors = new List<MarketMapValidationIssueResponse>();
        var warnings = new List<MarketMapValidationIssueResponse>();
        var liveLayouts = map.MarketLayouts.Where(layout => !layout.IsDeleted).ToArray();

        if (map.Status != MarketMapStatus.Draft)
            errors.Add(Issue("MARKET_MAP_NOT_DRAFT", "Only a draft MarketMap can proceed through the editing and validation workflow."));
        if (liveLayouts.Length == 0)
            errors.Add(Issue("MARKET_MAP_EMPTY", "MarketMap must contain at least one layout."));

        foreach (var deleted in map.MarketLayouts.Where(layout => layout.IsDeleted))
            errors.Add(Issue("CHILD_LAYOUT_DELETED", "A deleted layout is still associated with this MarketMap.", deleted));

        foreach (var group in liveLayouts.GroupBy(
                     layout => layout.SectionCode.Trim(), StringComparer.OrdinalIgnoreCase)
                 .Where(group => group.Count() > 1))
            errors.Add(Issue(
                "DUPLICATE_SECTION_CODE",
                $"Physical section '{group.Key}' occurs more than once in this MarketMap."));

        foreach (var group in liveLayouts.GroupBy(layout => layout.DisplayOrder)
                     .Where(group => group.Count() > 1))
            errors.Add(Issue(
                "DUPLICATE_DISPLAY_ORDER",
                $"Display order '{group.Key}' is assigned to more than one child layout."));

        foreach (var layout in liveLayouts)
        {
            if (layout.MarketMapId != map.Id)
                errors.Add(Issue("CHILD_MAP_MISMATCH", "Child layout belongs to another MarketMap.", layout));
            if (layout.NightMarketId != map.NightMarketId)
                errors.Add(Issue("CHILD_MARKET_MISMATCH", "Child layout belongs to another night market.", layout));
            if (layout.Status != MarketLayoutStatus.Draft)
                errors.Add(Issue("CHILD_LAYOUT_NOT_DRAFT", "Every child layout must be in Draft status.", layout));
            if (layout.DisplayOrder is < 0 or > 1000)
                errors.Add(Issue("DISPLAY_ORDER_INVALID", "Layout display order must be between 0 and 1000.", layout));

            foreach (var conflict in LayoutPhysicalCalibration.DetectConflicts(
                         layout, market.BoundaryWidthMeters, market.BoundaryHeightMeters))
            {
                warnings.Add(Issue(
                    "CALIBRATION_SOURCE_CONFLICT",
                    conflict.Message + " Explicit/precedence-resolved physical dimensions remain authoritative.",
                    layout,
                    severity: "Warning"));
            }
        }

        AddPlacementIssues(map, market, liveLayouts, null, errors, warnings);

        var defaultCount = liveLayouts.Count(layout => layout.IsDefaultView);
        if (defaultCount == 0)
            warnings.Add(Issue("DEFAULT_VIEW_MISSING", "MarketMap has no default-view layout."));
        else if (defaultCount > 1)
            errors.Add(Issue(
                "MULTIPLE_DEFAULT_VIEWS",
                "MarketMap has more than one default-view layout; the active-layout database constraint permits only one."));

        var duplicateBoothIds = await _locations.GetDuplicateBoothIdsByMarketMapAsync(
            map.Id, cancellationToken);
        foreach (var boothId in duplicateBoothIds)
            errors.Add(Issue(
                "DUPLICATE_BOOTH_ASSIGNMENT",
                $"Booth '{boothId}' has snapshot assignments in more than one child layout."));

        if (!await _entitlements.HasActiveMarketSubscriptionAsync(actorId))
            errors.Add(Issue(
                "MARKET_SUBSCRIPTION_REQUIRED",
                "An active Market subscription is required before this MarketMap can be activated."));

        var slotCount = liveLayouts.Sum(CountBoothSlots);
        var quota = await _resourceQuota.AssessMapCompositionAsync(
            actorId, liveLayouts.Length, slotCount, cancellationToken);
        if (quota.LayoutLimitExceeded)
            errors.Add(Issue(
                "LAYOUT_QUOTA_EXCEEDED",
                $"This MarketMap contains {quota.LayoutCount} layouts, but the current subscription allows a maximum of {quota.LayoutLimit}."));
        if (quota.SlotLimitExceeded)
            errors.Add(Issue(
                "SLOT_QUOTA_EXCEEDED",
                $"This MarketMap contains {quota.SlotCount} booth slots, but the current subscription allows a maximum of {quota.SlotLimit}."));

        var zones = await _zones.GetActiveByNightMarketIdAsync(
            map.NightMarketId, cancellationToken: cancellationToken);
        var validZoneIds = zones.Select(zone => zone.Id).ToHashSet();

        foreach (var layout in liveLayouts)
        {
            var graph = await _graphValidation.ValidateAsync(layout.Id, cancellationToken);
            errors.AddRange(graph.Errors.Select(message =>
                Issue("CHILD_LAYOUT_INVALID", message, layout)));
            warnings.AddRange(graph.Warnings.Select(message =>
                Issue("CHILD_LAYOUT_WARNING", message, layout, severity: "Warning")));

            var blocks = blocksByLayout[layout.Id];
            var blockIds = blocks.Where(block => !block.IsDeleted).Select(block => block.Id).ToHashSet();
            foreach (var block in blocks.Where(block => !block.IsDeleted))
            {
                if (block.LayoutId != layout.Id)
                    errors.Add(Issue("BLOCK_LAYOUT_MISMATCH", "Layout block belongs to another layout.", layout, zoneId: block.ZoneId));
                if (block.ZoneId.HasValue && !validZoneIds.Contains(block.ZoneId.Value))
                    errors.Add(Issue("ZONE_REFERENCE_INVALID", "Layout block references a zone outside this night market.", layout, zoneId: block.ZoneId));
                if (IsZoneBlock(block))
                {
                    foreach (var message in LayoutGeometryValidator.ValidateBlockMove(
                                 layout, block, block.X, block.Y, blocks))
                        errors.Add(Issue("ZONE_GEOMETRY_INVALID", message, layout, zoneId: block.ZoneId));
                }
            }

            foreach (var node in layout.LayoutNodes.Where(node => !node.IsDeleted))
            {
                if (node.LayoutId != layout.Id)
                    errors.Add(Issue("NODE_LAYOUT_MISMATCH", "Layout node belongs to another layout.", layout, node.Id, node.ZoneId));
                if (node.ZoneId.HasValue && !validZoneIds.Contains(node.ZoneId.Value))
                    errors.Add(Issue("ZONE_REFERENCE_INVALID", "Layout node references a zone outside this night market.", layout, node.Id, node.ZoneId));
                if (node.LayoutBlockId.HasValue && !blockIds.Contains(node.LayoutBlockId.Value))
                    errors.Add(Issue("NODE_BLOCK_REFERENCE_INVALID", "Layout node references an invalid layout block.", layout, node.Id, node.ZoneId));

                if (node.NodeType == LayoutNodeType.BoothSlot &&
                    (node.ZoneId.HasValue || node.LayoutBlockId.HasValue))
                {
                    foreach (var message in LayoutGeometryValidator.ValidateNodeMove(
                                 layout, node, node.Xcoordinate, node.Ycoordinate,
                                 layout.LayoutNodes.ToArray(), blocks))
                        errors.Add(Issue("BOOTH_SLOT_GEOMETRY_INVALID", message, layout, node.Id, node.ZoneId));
                }
            }

            var anchors = await _anchors.GetByLayoutAsync(layout.Id, cancellationToken);
            var nodesById = layout.LayoutNodes
                .Where(node => !node.IsDeleted)
                .ToDictionary(node => node.Id);
            foreach (var duplicate in anchors.GroupBy(
                         anchor => anchor.AnchorCode.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
                errors.Add(Issue(
                    "ANCHOR_CODE_DUPLICATE",
                    $"Navigation anchor code '{duplicate.Key}' is duplicated.", layout));

            foreach (var anchor in anchors)
            {
                if (!nodesById.TryGetValue(anchor.LayoutNodeId, out var node) || node.LayoutId != layout.Id)
                {
                    errors.Add(Issue("ANCHOR_NODE_INVALID", "Navigation anchor references a node outside this layout.", layout, anchor.LayoutNodeId));
                    continue;
                }
                if (anchor.AnchorType is NavigationAnchorType.Entrance or NavigationAnchorType.Both &&
                    node.NodeType != LayoutNodeType.Entrance)
                    errors.Add(Issue("ANCHOR_NODE_TYPE_INVALID", "An entrance anchor must reference an Entrance node.", layout, node.Id, node.ZoneId));
                if (anchor.OpeningTime.HasValue != anchor.ClosingTime.HasValue)
                    errors.Add(Issue("ANCHOR_HOURS_INCOMPLETE", "Navigation anchor opening and closing times must be provided together.", layout, node.Id, node.ZoneId));
                if (anchor.Latitude is < -90 or > 90 || anchor.Longitude is < -180 or > 180)
                    errors.Add(Issue("ANCHOR_COORDINATE_INVALID", "Navigation anchor latitude or longitude is invalid.", layout, node.Id, node.ZoneId));
            }
        }

        return new MarketMapValidationResponse
        {
            Errors = DistinctIssues(errors),
            Warnings = DistinctIssues(warnings)
        };
    }

    private IReadOnlyCollection<MarketMapLayoutArrangementRequest> ValidateArrangement(
        MarketMap map, NightMarket market, ArrangeMarketMapRequest request)
    {
        if (map.Status != MarketMapStatus.Draft)
            throw AppException.Conflict(
                "Only a draft MarketMap can be arranged.",
                "MARKET_MAP_NOT_DRAFT");
        if (request.Layouts is null || request.Layouts.Count == 0)
            throw AppException.BadRequest(
                "Arrangement must contain every child layout.",
                "ARRANGEMENT_LAYOUTS_REQUIRED");
        if (request.Layouts.Any(item => item.LayoutId == Guid.Empty))
            throw AppException.BadRequest(
                "Every arrangement layout identifier must be a non-empty GUID.",
                "ARRANGEMENT_LAYOUT_ID_INVALID");
        if (request.Layouts.Select(item => item.LayoutId).Distinct().Count() != request.Layouts.Count)
            throw AppException.BadRequest(
                "Arrangement contains duplicate layout identifiers.",
                "ARRANGEMENT_DUPLICATE_LAYOUT_ID");
        if (request.Layouts.Select(item => item.DisplayOrder).Distinct().Count() != request.Layouts.Count)
            throw AppException.BadRequest(
                "Every child layout must have a unique display order.",
                "ARRANGEMENT_DUPLICATE_DISPLAY_ORDER");
        if (request.Layouts.Any(item =>
                !double.IsFinite(item.OffsetXMeters) ||
                !double.IsFinite(item.OffsetYMeters) ||
                GeometryTolerance.IsNegative(item.OffsetXMeters) ||
                GeometryTolerance.IsNegative(item.OffsetYMeters) ||
                item.DisplayOrder is < 0 or > 1000))
            throw AppException.BadRequest(
                "Layout offsets must be finite and non-negative, and display order must be between 0 and 1000.",
                "ARRANGEMENT_VALUE_INVALID");

        var liveLayouts = map.MarketLayouts.Where(layout => !layout.IsDeleted).ToArray();
        var liveIds = liveLayouts.Select(layout => layout.Id).ToHashSet();
        var requestedIds = request.Layouts.Select(item => item.LayoutId).ToHashSet();
        var extraId = requestedIds.FirstOrDefault(id => !liveIds.Contains(id));
        if (extraId != Guid.Empty)
        {
            var deleted = map.MarketLayouts.FirstOrDefault(layout => layout.Id == extraId && layout.IsDeleted);
            throw deleted is not null
                ? AppException.Conflict(
                    $"Layout '{extraId}' has been deleted.",
                    "ARRANGEMENT_LAYOUT_DELETED")
                : AppException.BadRequest(
                    $"Layout '{extraId}' does not belong to this MarketMap.",
                    "ARRANGEMENT_FOREIGN_LAYOUT");
        }
        if (!liveIds.SetEquals(requestedIds))
            throw AppException.BadRequest(
                "Arrangement must contain every non-deleted child layout exactly once.",
                "ARRANGEMENT_INCOMPLETE");

        var invalidChild = liveLayouts.FirstOrDefault(layout =>
            layout.MarketMapId != map.Id ||
            layout.NightMarketId != map.NightMarketId ||
            layout.Status != MarketLayoutStatus.Draft);
        if (invalidChild is not null)
            throw AppException.Conflict(
                $"Layout '{invalidChild.Id}' is not an editable child of this draft MarketMap.",
                "ARRANGEMENT_CHILD_NOT_EDITABLE");

        var placementErrors = new List<MarketMapValidationIssueResponse>();
        var placementWarnings = new List<MarketMapValidationIssueResponse>();
        AddPlacementIssues(map, market, liveLayouts, request.Layouts, placementErrors, placementWarnings);
        if (placementErrors.Count > 0)
        {
            var messages = placementErrors.Select(issue => issue.Message).Distinct().ToArray();
            throw AppException.Validation(
                "Market map arrangement is invalid. " + string.Join(" ", messages),
                new Dictionary<string, string[]> { ["layouts"] = messages },
                "MARKET_MAP_ARRANGEMENT_INVALID");
        }

        return request.Layouts;
    }

    private static void AddPlacementIssues(
        MarketMap map,
        NightMarket market,
        IReadOnlyCollection<MarketLayout> layouts,
        IReadOnlyCollection<MarketMapLayoutArrangementRequest>? proposed,
        ICollection<MarketMapValidationIssueResponse> errors,
        ICollection<MarketMapValidationIssueResponse> warnings)
    {
        var proposedById = proposed?.ToDictionary(item => item.LayoutId);
        var rects = new List<PlacementRect>();
        foreach (var layout in layouts)
        {
            var x = proposedById is not null ? proposedById[layout.Id].OffsetXMeters : layout.OffsetXMeters;
            var y = proposedById is not null ? proposedById[layout.Id].OffsetYMeters : layout.OffsetYMeters;
            if (!double.IsFinite(x) || !double.IsFinite(y)
                || GeometryTolerance.IsNegative(x) || GeometryTolerance.IsNegative(y))
            {
                errors.Add(Issue("LAYOUT_OFFSET_INVALID", "Layout offsets must be finite and non-negative.", layout));
                continue;
            }

            var dimensions = TryResolvePhysicalDimensions(layout, market);
            if (!dimensions.HasValue)
            {
                errors.Add(Issue(
                    "LAYOUT_PHYSICAL_SIZE_UNAVAILABLE",
                    "Layout physical width and height cannot be resolved in meters.", layout));
                continue;
            }

            var rect = new PlacementRect(layout.Id, x, y, dimensions.Value.Width, dimensions.Value.Height);
            rects.Add(rect);
            if (market.BoundaryWidthMeters is > 0 &&
                GeometryTolerance.Exceeds(rect.Right, market.BoundaryWidthMeters.Value))
                errors.Add(Issue("LAYOUT_OUTSIDE_MARKET", "Layout exceeds the night market width boundary.", layout));
            if (market.BoundaryHeightMeters is > 0 &&
                GeometryTolerance.Exceeds(rect.Bottom, market.BoundaryHeightMeters.Value))
                errors.Add(Issue("LAYOUT_OUTSIDE_MARKET", "Layout exceeds the night market length boundary.", layout));
        }

        if (market.BoundaryWidthMeters is not > 0 || market.BoundaryHeightMeters is not > 0)
            warnings.Add(Issue(
                "MARKET_BOUNDARY_UNAVAILABLE",
                "Night market physical boundaries are unavailable; containment cannot be fully validated.",
                severity: "Warning"));

        for (var leftIndex = 0; leftIndex < rects.Count; leftIndex++)
        for (var rightIndex = leftIndex + 1; rightIndex < rects.Count; rightIndex++)
        {
            var left = rects[leftIndex];
            var right = rects[rightIndex];
            if (!RectanglesOverlap(left, right)) continue;
            var leftLayout = layouts.First(layout => layout.Id == left.LayoutId);
            var rightLayout = layouts.First(layout => layout.Id == right.LayoutId);
            errors.Add(Issue(
                "LAYOUT_OVERLAP",
                $"Layout '{leftLayout.SectionName}' overlaps layout '{rightLayout.SectionName}'.",
                leftLayout));
        }
    }

    private async Task<MarketMap> GetManagedMapAsync(
        Guid nightMarketId, Guid marketMapId, CancellationToken cancellationToken)
    {
        var map = await _marketMaps.GetManagementDetailAsync(marketMapId, cancellationToken)
            ?? throw AppException.NotFound("Market map was not found.", "MARKET_MAP_NOT_FOUND");
        if (map.NightMarketId != nightMarketId)
            throw AppException.BadRequest(
                "MarketMap does not belong to the requested night market.",
                "MARKET_MAP_MARKET_MISMATCH");
        return map;
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<LayoutBlock>>> LoadBlocksAsync(
        IReadOnlyCollection<MarketLayout> layouts, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, IReadOnlyCollection<LayoutBlock>>();
        foreach (var layout in layouts)
            result[layout.Id] = await _layouts.GetBlocksByLayoutIdAsync(layout.Id, cancellationToken);
        return result;
    }

    private static PhysicalDimensions? TryResolvePhysicalDimensions(
        MarketLayout layout, NightMarket market)
    {
        var scale = LayoutPhysicalCalibration.TryResolve(layout, market);
        if (!scale.HasValue || layout.Width <= 0 || layout.Height <= 0)
            return null;
        var width = layout.Width * scale.Value.ScaleX;
        var height = layout.Height * scale.Value.ScaleY;
        return double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > 0
            ? new PhysicalDimensions(width, height)
            : null;
    }

    private static bool RectanglesOverlap(PlacementRect left, PlacementRect right)
        => GeometryTolerance.RectanglesHaveInteriorOverlap(
            left.X, left.Y, left.Width, left.Height,
            right.X, right.Y, right.Width, right.Height);

    private static bool IsZoneBlock(LayoutBlock block)
        => !block.IsDeleted && block.Type.Equals("Zone", StringComparison.OrdinalIgnoreCase);

    private static MarketMapValidationIssueResponse Issue(
        string code,
        string message,
        MarketLayout? layout = null,
        Guid? nodeId = null,
        Guid? zoneId = null,
        string severity = "Error")
        => new()
        {
            Code = code,
            Message = message,
            Severity = severity,
            LayoutId = layout?.Id,
            SectionCode = layout?.SectionCode,
            NodeId = nodeId,
            ZoneId = zoneId
        };

    private static IReadOnlyCollection<MarketMapValidationIssueResponse> DistinctIssues(
        IEnumerable<MarketMapValidationIssueResponse> issues)
        => issues
            .GroupBy(issue => new
            {
                issue.Code,
                issue.Message,
                issue.Severity,
                issue.LayoutId,
                issue.SectionCode,
                issue.NodeId,
                issue.ZoneId
            })
            .Select(group => group.First())
            .ToArray();

    private readonly record struct PhysicalDimensions(double Width, double Height);
    private readonly record struct PlacementRect(
        Guid LayoutId, double X, double Y, double Width, double Height)
    {
        public double Right => X + Width;
        public double Bottom => Y + Height;
    }
}

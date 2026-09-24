using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketMaps;

public sealed partial class MarketMapService
{
    public async Task<ApiResponse<MarketMapDetailResponse>> SetDefaultLayoutAsync(
        Guid nightMarketId,
        Guid marketMapId,
        SetMarketMapDefaultLayoutRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var market = await EnsureOwnedMarketAsync(nightMarketId, actorId, cancellationToken);
        if (request.LayoutId == Guid.Empty)
            throw AppException.BadRequest(
                "A layout must be selected as the default view.",
                "DEFAULT_LAYOUT_ID_REQUIRED");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Default selection is part of the MarketMap draft composition. The
            // per-market lock serializes it with arrangement and activation.
            await _layouts.AcquireMarketLockAsync(nightMarketId, cancellationToken);

            var map = await _marketMaps.GetManagementDetailForUpdateAsync(
                marketMapId, cancellationToken)
                ?? throw AppException.NotFound(
                    "Market map was not found.",
                    "MARKET_MAP_NOT_FOUND");

            if (map.NightMarketId != nightMarketId)
                throw AppException.BadRequest(
                    "MarketMap does not belong to the requested night market.",
                    "MARKET_MAP_MARKET_MISMATCH");
            if (map.Status != MarketMapStatus.Draft)
                throw AppException.Conflict(
                    "The default view can only be changed on a draft MarketMap.",
                    "DEFAULT_LAYOUT_MAP_NOT_DRAFT");

            var liveLayouts = map.MarketLayouts
                .Where(layout => !layout.IsDeleted)
                .ToArray();
            var target = liveLayouts.FirstOrDefault(layout => layout.Id == request.LayoutId)
                ?? throw AppException.BadRequest(
                    "The selected layout is not part of this MarketMap.",
                    "DEFAULT_LAYOUT_NOT_IN_MARKET_MAP");
            if (target.Status != MarketLayoutStatus.Draft)
                throw AppException.Conflict(
                    "Only a draft layout can be selected as the draft MarketMap default.",
                    "DEFAULT_LAYOUT_NOT_DRAFT");

            var now = DateTime.UtcNow;
            foreach (var layout in liveLayouts)
            {
                var shouldBeDefault = layout.Id == target.Id;
                if (layout.IsDefaultView == shouldBeDefault) continue;
                layout.IsDefaultView = shouldBeDefault;
                layout.UpdatedAt = now;
            }

            map.UpdatedAt = now;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (liveLayouts.Count(layout => layout.IsDefaultView) != 1)
                throw AppException.Conflict(
                    "The draft MarketMap default-view invariant could not be established.",
                    "DEFAULT_LAYOUT_INVARIANT_FAILED");

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return ApiResponse<MarketMapDetailResponse>.SuccessResponse(
                ToDetailResponse(map, market),
                "Draft MarketMap default view updated successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }
}

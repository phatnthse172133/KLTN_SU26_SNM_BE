using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketMaps;

public sealed partial class MarketMapService
{
    public async Task<ApiResponse<MarketMapDetailResponse>> ActivateAsync(
        Guid nightMarketId, Guid marketMapId, Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var market = await EnsureOwnedMarketAsync(nightMarketId, actorId, cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _layouts.AcquireMarketLockAsync(nightMarketId, cancellationToken);

            var target = await _marketMaps.GetManagementDetailForUpdateAsync(
                marketMapId, cancellationToken)
                ?? throw AppException.NotFound("Market map was not found.", "MARKET_MAP_NOT_FOUND");
            if (target.NightMarketId != nightMarketId)
                throw AppException.BadRequest(
                    "MarketMap does not belong to the requested night market.",
                    "MARKET_MAP_MARKET_MISMATCH");
            if (target.Status != MarketMapStatus.Draft)
                throw AppException.Conflict(
                    "Only a draft MarketMap can be activated.",
                    "MARKET_MAP_NOT_DRAFT");
            if (target.Name.Equals(DomainLayer.Entities.MarketMap.LegacyDraftName, StringComparison.OrdinalIgnoreCase))
                throw AppException.Conflict(
                    "The legacy editable draft map cannot be published.",
                    "LEGACY_DRAFT_MAP_NOT_PUBLISHABLE");
            if (target.PublishedAt.HasValue)
                throw AppException.Conflict(
                    "Draft MarketMap has an unexpected publication timestamp and cannot be activated.",
                    "DRAFT_PUBLISHED_AT_INVALID");

            var previousActive = await _marketMaps.GetActiveForUpdateAsync(
                nightMarketId, cancellationToken);

            var targetLayouts = target.MarketLayouts
                .Where(layout => !layout.IsDeleted)
                .ToArray();
            var blocksByLayout = await LoadBlocksAsync(targetLayouts, cancellationToken);
            var validation = await BuildValidationAsync(
                target, market, actorId, blocksByLayout, cancellationToken);
            if (!validation.IsValid || !validation.CanActivate)
                throw AppException.UnprocessableEntity(
                    "MarketMap validation failed. Resolve all blocking issues before activation.",
                    "MARKET_MAP_VALIDATION_FAILED",
                    validation);

            var operationalLayouts = await _layouts.GetOperationalByMarketForUpdateAsync(
                nightMarketId, cancellationToken);
            var now = DateTime.UtcNow;

            if (previousActive is not null)
            {
                previousActive.Status = MarketMapStatus.Archived;
                previousActive.UpdatedAt = now;
            }

            // Deactivate every currently operational physical section, not only
            // sections that have a replacement. This makes section removal and
            // stale legacy-published sections converge to the new composition.
            foreach (var layout in operationalLayouts)
            {
                layout.Status = MarketLayoutStatus.Inactive;
                layout.UpdatedAt = now;
            }

            // Persist removals from the partial unique indexes before adding the
            // new active map and section rows. Both saves remain in this transaction.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var layout in targetLayouts)
            {
                layout.Status = MarketLayoutStatus.Active;
                layout.UpdatedAt = now;
            }
            target.Status = MarketMapStatus.Active;
            target.PublishedAt = now;
            target.UpdatedAt = now;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var activeMap = await _marketMaps.GetActiveByMarketIdAsync(
                nightMarketId, cancellationToken);
            var activeLayouts = await _layouts.GetPublishedMapsAsync(
                nightMarketId, cancellationToken);
            var targetLayoutIds = targetLayouts.Select(layout => layout.Id).ToHashSet();
            var activeLayoutIds = activeLayouts.Select(layout => layout.Id).ToHashSet();
            if (activeMap?.Id != target.Id || !targetLayoutIds.SetEquals(activeLayoutIds))
                throw AppException.Conflict(
                    "The resulting operational layout composition is inconsistent; activation was rolled back.",
                    "ACTIVE_COMPOSITION_INVARIANT_FAILED");

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return ApiResponse<MarketMapDetailResponse>.SuccessResponse(
                ToDetailResponse(target, market),
                "MarketMap activated successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }
}

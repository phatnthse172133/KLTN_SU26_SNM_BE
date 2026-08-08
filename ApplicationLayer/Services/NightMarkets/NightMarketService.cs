using AutoMapper;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Subscriptions;
using ApplicationLayer.Services.CustomerDiscovery;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.NightMarkets;

public class NightMarketService : INightMarketService
{
    private readonly INightMarketRepository _markets;
    private readonly IMapper _mapper;
    private readonly ISubscriptionEntitlementService _entitlements;
    private readonly IBoothRepository _booths;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IMarketLayoutRepository _layouts;
    private readonly IZoneRepository _zones;
    private readonly IOrderRepository _orders;
    private readonly ApplicationLayer.Services.Storage.IFileStorageService _fileStorage;
    private readonly INightMarketImageRepository _marketImages;
    private readonly IUnitOfWork _unitOfWork;

    public NightMarketService(
        INightMarketRepository markets,
        IMapper mapper,
        ISubscriptionEntitlementService entitlements,
        IBoothRepository booths,
        ISubscriptionRepository subscriptions,
        IMarketLayoutRepository layouts,
        IZoneRepository zones,
        IOrderRepository orders,
        ApplicationLayer.Services.Storage.IFileStorageService fileStorage,
        INightMarketImageRepository marketImages,
        IUnitOfWork unitOfWork)
    {
        _markets = markets;
        _mapper = mapper;
        _entitlements = entitlements;
        _booths = booths;
        _subscriptions = subscriptions;
        _layouts = layouts;
        _zones = zones;
        _orders = orders;
        _fileStorage = fileStorage;
        _marketImages = marketImages;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PaginationResp<NightMarketResponse>>> GetAllAsync(
        NightMarketListRequest request,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var page = await _markets.GetActivePagedAsync(
            request.Keyword,
            isAdmin ? request.Status : NightMarketStatus.Active,
            request.Page,
            request.PageSize,
            request.SortBy,
            request.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase),
            cancellationToken);

        return ApiResponse<PaginationResp<NightMarketResponse>>.SuccessResponse(
            _mapper.MapPage<NightMarket, NightMarketResponse>(page, request));
    }

    public async Task<ApiResponse<PaginationResp<NightMarketListItemResponse>>> GetCustomerAllAsync(
        NightMarketListRequest request,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var localTime = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(utcNow));
        var page = await _markets.GetCustomerPagedAsync(
            request.Keyword,
            null,
            localTime,
            request.Page,
            request.PageSize,
            request.SortBy,
            request.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase),
            cancellationToken);
        var items = page.Items.Select(market => MapCustomerListItem(market, utcNow)).ToList();
        return ApiResponse<PaginationResp<NightMarketListItemResponse>>.SuccessResponse(
            PaginationResp<NightMarketListItemResponse>.Create(items, page.TotalCount, request));
    }

    public async Task<ApiResponse<NightMarketDetailResponse>> GetCustomerAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var market = await _markets.GetCustomerByIdAsync(id, cancellationToken)
            ?? throw AppException.NotFound("Night market was not found.", "NIGHT_MARKET_NOT_FOUND");
        var images = await _marketImages.FindAsync(image =>
            image.NightMarketId == id && !image.IsDeleted);
        var listItem = MapCustomerListItem(market, DateTime.UtcNow);
        var imageUrls = (string.IsNullOrWhiteSpace(market.ThumbnailUrl)
                ? images.Select(image => image.ImageUrl)
                : new[] { market.ThumbnailUrl }.Concat(images.Select(image => image.ImageUrl)))
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return ApiResponse<NightMarketDetailResponse>.SuccessResponse(new NightMarketDetailResponse
        {
            Id = listItem.Id,
            Name = listItem.Name,
            Address = listItem.Address,
            Latitude = listItem.Latitude,
            Longitude = listItem.Longitude,
            ThumbnailUrl = listItem.ThumbnailUrl,
            OpeningHours = listItem.OpeningHours,
            ClosingHours = listItem.ClosingHours,
            IsOpenNow = listItem.IsOpenNow,
            OpeningStatusText = listItem.OpeningStatusText,
            ActiveBoothCount = listItem.ActiveBoothCount,
            Status = listItem.Status,
            Description = market.Description,
            ImageUrls = imageUrls,
            HasLayout = market.HasLayout
        });
    }

    private static NightMarketListItemResponse MapCustomerListItem(
        NightMarketCustomerReadModel market,
        DateTime utcNow)
    {
        var availability = NightMarketAvailability.Evaluate(market, utcNow);
        return new NightMarketListItemResponse
        {
            Id = market.Id,
            Name = market.Name,
            Address = market.Address,
            Latitude = market.Latitude,
            Longitude = market.Longitude,
            ThumbnailUrl = market.ThumbnailUrl,
            OpeningHours = market.OpeningHours,
            ClosingHours = market.ClosingHours,
            IsOpenNow = availability.IsOpenNow,
            OpeningStatusText = availability.StatusText,
            ActiveBoothCount = market.ActiveBoothCount,
            Status = market.Status.ToString()
        };
    }

    public async Task<ApiResponse<List<NightMarketOptionDto>>> GetOptionsAsync(bool isAdmin = false, CancellationToken cancellationToken = default)
    {
        var page = await _markets.GetActivePagedAsync(
            null, isAdmin ? null : NightMarketStatus.Active, 1, 200, "name", true, cancellationToken);

        var options = page.Items.Select(m => new NightMarketOptionDto
        {
            Id = m.Id,
            Name = m.Name,
            Status = m.Status.ToString()
        }).ToList();

        return ApiResponse<List<NightMarketOptionDto>>.SuccessResponse(options);
    }

    public async Task<ApiResponse<NightMarketResponse>> GetAsync(Guid id, Guid? viewerId = null, string? viewerRole = null, CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(id, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        if (market.Status != NightMarketStatus.Active && !CanViewInactiveMarket(market, viewerId, viewerRole))
            throw AppException.NotFound("Night market was not found.");

        return ApiResponse<NightMarketResponse>.SuccessResponse(_mapper.Map<NightMarketResponse>(market));
    }

    private static bool CanViewInactiveMarket(NightMarket market, Guid? viewerId, string? viewerRole)
        => string.Equals(viewerRole, "Admin", StringComparison.OrdinalIgnoreCase)
           || (viewerId.HasValue && market.MarketOwnerId == viewerId);

    public async Task<ApiResponse<List<NightMarketResponse>>> GetMineAsync(Guid marketOwnerId, CancellationToken cancellationToken = default)
    {
        var markets = await _markets.GetByOwnerIdAsync(marketOwnerId, cancellationToken);
        return ApiResponse<List<NightMarketResponse>>.SuccessResponse(_mapper.Map<List<NightMarketResponse>>(markets));
    }

    public async Task<ApiResponse<NightMarketResponse>> CreateAsync(CreateNightMarketRequest request, Guid marketOwnerId, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request, cancellationToken: cancellationToken);

        var hasSubscription = await _entitlements.HasActiveMarketSubscriptionAsync(marketOwnerId);
        if (!hasSubscription)
            throw AppException.Forbidden("An active Market subscription is required to perform this action.", "MARKET_SUBSCRIPTION_REQUIRED");

        var maxMarkets = await _entitlements.GetMaxMarketsAsync(marketOwnerId);
        var currentCount = await _markets.CountAsync(m =>
            m.MarketOwnerId == marketOwnerId && !m.IsDeleted &&
            m.ModerationStatus != ModerationStatus.Suspended);
        if (currentCount >= maxMarkets)
            throw AppException.Forbidden(
                $"Your current package allows a maximum of {maxMarkets} night market(s). Please upgrade to create more.",
                "MARKET_LIMIT_REACHED");

        var market = _mapper.Map<NightMarket>(request);
        var now = DateTime.UtcNow;
        market.Id = Guid.NewGuid();
        market.MarketOwnerId = marketOwnerId;
        market.Status = NightMarketStatus.Inactive;
        market.IsDeleted = false;
        market.CreatedAt = now;
        market.UpdatedAt = now;

        await _markets.AddAsync(market);
        await _markets.SaveChangesAsync();
        return ApiResponse<NightMarketResponse>.SuccessResponse(_mapper.Map<NightMarketResponse>(market), "Night market created successfully.");
    }

    public async Task<ApiResponse<NightMarketResponse>> UpdateAsync(Guid id, UpdateNightMarketRequest request, Guid? currentUserId, string currentUserRole, CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(id, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        EnsureOwnership(market, currentUserId, currentUserRole);

        await ValidateAsync(request, id, cancellationToken);

        var originalStatus = market.Status;
        var oldThumbnailUrl = market.ThumbnailUrl;

        _mapper.Map(request, market);
        market.Status = originalStatus;
        market.UpdatedAt = DateTime.UtcNow;

        _markets.Update(market);
        await _markets.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(oldThumbnailUrl) && oldThumbnailUrl != market.ThumbnailUrl)
        {
            await _fileStorage.DeleteImageIfManagedAsync(oldThumbnailUrl, cancellationToken);
        }

        return ApiResponse<NightMarketResponse>.SuccessResponse(_mapper.Map<NightMarketResponse>(market), "Night market updated successfully.");
    }

    public async Task<ApiResponse<NightMarketResponse>> UpdateGeographicLocationAsync(
        Guid id,
        UpdateNightMarketGeographicLocationRequest request,
        Guid? currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(id, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        EnsureOwnership(market, currentUserId, currentUserRole);

        ValidateGeographicLocation(
            request.Address,
            request.Latitude,
            request.Longitude,
            request.BoundaryWidthMeters,
            request.BoundaryHeightMeters);

        _mapper.Map(request, market);
        market.UpdatedAt = DateTime.UtcNow;

        _markets.Update(market);
        await _markets.SaveChangesAsync();

        return ApiResponse<NightMarketResponse>.SuccessResponse(
            _mapper.Map<NightMarketResponse>(market),
            "Night market geographic location updated successfully.");
    }

    public async Task<ApiResponse<NightMarketNavigationInfoResponse>> GetNavigationInfoAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(id, cancellationToken);
        if (market is null || market.Status != NightMarketStatus.Active)
            throw AppException.NotFound("Night market was not found.");

        if (!market.Latitude.HasValue ||
            !market.Longitude.HasValue ||
            !market.BoundaryWidthMeters.HasValue ||
            !market.BoundaryHeightMeters.HasValue)
            throw AppException.BadRequest("Night market geographic information is incomplete.");

        return ApiResponse<NightMarketNavigationInfoResponse>.SuccessResponse(
            _mapper.Map<NightMarketNavigationInfoResponse>(market));
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, Guid? currentUserId, string currentUserRole, CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(id, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        EnsureOwnership(market, currentUserId, currentUserRole);

        var impact = await BuildDeletionImpactAsync(id);
        if (impact.OpenOrders > 0)
        {
            throw AppException.Conflict(
                "This night market has open orders. Complete, cancel, or resolve them before deleting the market.",
                "MARKET_HAS_OPEN_ORDERS");
        }

        market.IsDeleted = true;
        market.DeletedAt = DateTime.UtcNow;
        market.DeletedBy = currentUserId;
        market.DeletionReason = "User requested soft delete.";
        market.UpdatedAt = DateTime.UtcNow;
        _markets.Update(market);
        await _markets.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { market.Id }, "Night market deleted successfully. All active booths and recommendations have been hidden.");
    }

    public async Task<ApiResponse<object>> GetDeletionImpactAsync(Guid id, Guid? currentUserId, string currentUserRole, CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(id, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        EnsureOwnership(market, currentUserId, currentUserRole);

        var impact = await BuildDeletionImpactAsync(id);

        return ApiResponse<object>.SuccessResponse(new
        {
            impact.ActiveBooths,
            impact.OpenOrders,
            impact.Layouts,
            impact.Zones
        });
    }

    private async Task<NightMarketDeletionImpact> BuildDeletionImpactAsync(Guid nightMarketId)
    {
        var booths = (await _booths.FindAsync(b => b.NightMarketId == nightMarketId)).ToList();
        var boothOwnerIds = booths
            .Select(b => b.BoothOwnerId)
            .Distinct()
            .ToList();

        var openOrders = boothOwnerIds.Count == 0
            ? 0
            : await _orders.CountAsync(o =>
                boothOwnerIds.Contains(o.BoothOwnerId)
                && (o.Status == OrderStatus.Placed || o.Status == OrderStatus.Preparing || o.Status == OrderStatus.ReadyForPickup));

        var layouts = await _layouts.CountAsync(l => l.NightMarketId == nightMarketId);
        var zones = await _zones.CountAsync(z => z.NightMarketId == nightMarketId);

        return new NightMarketDeletionImpact(
            booths.Count(b => b.Status == BoothStatus.Active),
            openOrders,
            layouts,
            zones);
    }

    private async Task<NightMarket?> GetActiveMarketAsync(Guid id, CancellationToken cancellationToken)
        => await _markets.GetActiveByIdAsync(id, cancellationToken);

    private sealed record NightMarketDeletionImpact(
        int ActiveBooths,
        int OpenOrders,
        int Layouts,
        int Zones);

    private static void EnsureOwnership(NightMarket market, Guid? currentUserId, string currentUserRole)
    {
        if (string.Equals(currentUserRole, "Admin", StringComparison.OrdinalIgnoreCase))
            return;
        if (market.MarketOwnerId == null || market.MarketOwnerId != currentUserId)
            throw AppException.Forbidden("You do not have permission to modify this night market.");
    }

    private async Task ValidateAsync(
        CreateNightMarketRequest request,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        ValidateGeographicLocation(
            request.Address,
            request.Latitude,
            request.Longitude,
            request.BoundaryWidthMeters,
            request.BoundaryHeightMeters);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw AppException.BadRequest("Night market name is required.");

        if (request.OpeningHours.HasValue != request.ClosingHours.HasValue)
            throw AppException.BadRequest("Opening hours and closing hours must be provided together.");

        if (request.OpeningHours.HasValue &&
            request.ClosingHours.HasValue &&
            request.OpeningHours.Value >= request.ClosingHours.Value)
            throw AppException.BadRequest("Opening hours must be earlier than closing hours for same-day operation.");

        if (await _markets.ActiveNameExistsAsync(request.Name, excludeId, cancellationToken))
            throw AppException.Conflict("Night market name already exists.", "MARKET_NAME_EXISTS");
    }

    private static void ValidateGeographicLocation(
        string address,
        decimal? latitude,
        decimal? longitude,
        int boundaryWidthMeters,
        int boundaryHeightMeters)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw AppException.BadRequest("Night market address is required.");

        if (!latitude.HasValue || latitude.Value is < -90 or > 90)
            throw AppException.BadRequest("Latitude must be between -90 and 90.");

        if (!longitude.HasValue || longitude.Value is < -180 or > 180)
            throw AppException.BadRequest("Longitude must be between -180 and 180.");

        if (boundaryWidthMeters <= 0 || boundaryHeightMeters <= 0)
            throw AppException.BadRequest("Boundary width and height must be greater than zero.");
    }

    public async Task<ApiResponse<NightMarketResponse>> PatchStatusAsync(
        Guid id,
        PatchNightMarketStatusRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (request.Status != NightMarketStatus.Active && request.Status != NightMarketStatus.Inactive)
            throw AppException.BadRequest("Only Active or Inactive status is allowed.");

        var market = await _markets.GetByIdAsync(id);
        if (market is null || market.IsDeleted)
            throw AppException.NotFound("Night market was not found.");

        EnsureOwnership(market, currentUserId, currentUserRole);

        if (market.ModerationStatus == ModerationStatus.Suspended)
            throw AppException.Forbidden(
                "This night market is suspended by an administrator. You cannot change its status until the suspension is lifted.",
                "MARKET_SUSPENDED");

        if (market.Status == request.Status)
            throw AppException.BadRequest($"Night market is already {request.Status}.");

        if (request.Status == NightMarketStatus.Active)
        {
            if (market.MarketOwnerId.HasValue)
            {
                var hasSub = await _entitlements.HasActiveMarketSubscriptionAsync(market.MarketOwnerId.Value);
                if (!hasSub)
                    throw AppException.Forbidden("An active Market subscription is required to activate a night market.", "MARKET_SUBSCRIPTION_REQUIRED");
            }

            var hasActiveLayout = await _layouts.CountAsync(l => l.NightMarketId == id && l.Status == MarketLayoutStatus.Active && !l.IsDeleted) > 0;
            if (!hasActiveLayout)
                throw AppException.BadRequest("An active layout is required before activating the night market.", "LAYOUT_REQUIRED");
        }

        market.Status = request.Status;
        market.UpdatedAt = DateTime.UtcNow;

        _markets.Update(market);
        await _markets.SaveChangesAsync();

        return ApiResponse<NightMarketResponse>.SuccessResponse(
            _mapper.Map<NightMarketResponse>(market),
            $"Night market status changed to {request.Status} successfully.");
    }

    public async Task<ApiResponse<ActivationReadinessResponse>> GetActivationReadinessAsync(
        Guid id,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var market = await _markets.GetByIdAsync(id);
        if (market is null || market.IsDeleted)
            throw AppException.NotFound("This night market no longer exists.", "MARKET_NOT_FOUND");

        EnsureOwnership(market, currentUserId, currentUserRole);

        var hasSubscription = market.MarketOwnerId.HasValue
            && await _entitlements.HasActiveMarketSubscriptionAsync(market.MarketOwnerId.Value);
        var hasActiveLayout = await _layouts.CountAsync(layout =>
            layout.NightMarketId == id
            && layout.Status == MarketLayoutStatus.Active
            && !layout.IsDeleted) > 0;
        var isNotSuspended = market.ModerationStatus != ModerationStatus.Suspended;

        var checks = new List<ActivationReadinessCheckResponse>
        {
            new()
            {
                Code = "MARKET_SUBSCRIPTION_REQUIRED",
                Passed = hasSubscription,
                Message = hasSubscription
                    ? "Your Market subscription is active."
                    : "Choose and complete payment for a Market plan before activating this night market.",
                CtaLabel = hasSubscription ? null : "View plans",
                CtaAction = hasSubscription ? null : "navigate:/marketowner/subscriptions"
            },
            new()
            {
                Code = "LAYOUT_REQUIRED",
                Passed = hasActiveLayout,
                Message = hasActiveLayout
                    ? "An active layout is ready."
                    : "Create, validate, and activate a layout before activating this night market.",
                CtaLabel = hasActiveLayout ? null : "Open layouts",
                CtaAction = hasActiveLayout ? null : "navigate:/marketowner/layouts"
            },
            new()
            {
                Code = "MARKET_SUSPENDED",
                Passed = isNotSuspended,
                Message = isNotSuspended
                    ? "This night market is not suspended."
                    : "This night market was suspended by an administrator. Contact Support if you need assistance."
            }
        };

        return ApiResponse<ActivationReadinessResponse>.SuccessResponse(new ActivationReadinessResponse
        {
            CanActivate = checks.All(check => check.Passed),
            Checks = checks
        });
    }

    private async Task<List<NightMarketImage>> GetActiveImagesAsync(Guid marketId)
    {
        var images = await _marketImages.FindAsync(i => i.NightMarketId == marketId && !i.IsDeleted);
        return images.OrderBy(i => i.DisplayOrder).ToList();
    }

    private static List<NightMarketImageResponse> MapImages(IEnumerable<NightMarketImage> images)
        => images.Select(i => new NightMarketImageResponse
        {
            Id = i.Id,
            NightMarketId = i.NightMarketId,
            ImageUrl = i.ImageUrl,
            DisplayOrder = i.DisplayOrder,
            IsCover = i.IsCover,
            IsDeleted = i.IsDeleted,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt
        }).ToList();

    private async Task DeleteImageFileBestEffortAsync(string? imageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return;

        try
        {
            await _fileStorage.DeleteImageIfManagedAsync(imageUrl, cancellationToken);
        }
        catch
        {
        }
    }

    public async Task<ApiResponse<List<NightMarketImageResponse>>> GetImagesAsync(
        Guid marketId, Guid? currentUserId, string currentUserRole, CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(marketId, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        EnsureOwnership(market, currentUserId, currentUserRole);

        var images = await GetActiveImagesAsync(marketId);
        return ApiResponse<List<NightMarketImageResponse>>.SuccessResponse(MapImages(images), "Market images retrieved successfully.");
    }

    public async Task<ApiResponse<List<NightMarketImageResponse>>> UploadImageAsync(
        Guid marketId, Stream stream, string fileName, string contentType, long length, Guid? currentUserId, string currentUserRole, CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(marketId, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        EnsureOwnership(market, currentUserId, currentUserRole);

        if (stream is null || length == 0)
            throw AppException.BadRequest("Image file is required.");

        var allowedMime = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
        if (!allowedMime.Contains(contentType))
            throw AppException.BadRequest("Unsupported image format. Please use JPG, PNG or WEBP.");

        if (length > 5 * 1024 * 1024)
            throw AppException.BadRequest("Image size must not exceed 5 MB.");

        string? savedUrl = null;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _marketImages.AcquireGalleryLockAsync(marketId, cancellationToken);

            var activeImages = await GetActiveImagesAsync(marketId);
            if (activeImages.Count >= 5)
                throw AppException.BadRequest("You can upload up to 5 images.");

            var url = await _fileStorage.SaveImageAsync("market-thumbnail", stream, fileName, contentType, length, cancellationToken);
            savedUrl = url;

            var now = DateTime.UtcNow;
            var isFirst = activeImages.Count == 0;
            var newImage = new NightMarketImage
            {
                Id = Guid.NewGuid(),
                NightMarketId = marketId,
                ImageUrl = url,
                DisplayOrder = activeImages.Count,
                IsCover = isFirst,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _marketImages.AddAsync(newImage);
            await _marketImages.SaveChangesAsync();

            if (isFirst)
            {
                market.ThumbnailUrl = url;
                market.UpdatedAt = now;
                _markets.Update(market);
                await _markets.SaveChangesAsync();
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            activeImages.Add(newImage);
            return ApiResponse<List<NightMarketImageResponse>>.SuccessResponse(MapImages(activeImages), "Image uploaded successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            await DeleteImageFileBestEffortAsync(savedUrl, cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<List<NightMarketImageResponse>>> DeleteImageAsync(
        Guid marketId, Guid imageId, Guid? currentUserId, string currentUserRole, CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(marketId, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        EnsureOwnership(market, currentUserId, currentUserRole);

        string deletedUrl;
        List<NightMarketImage> remaining;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _marketImages.AcquireGalleryLockAsync(marketId, cancellationToken);

            var activeImages = await GetActiveImagesAsync(marketId);
            var target = activeImages.FirstOrDefault(i => i.Id == imageId);
            if (target == null)
                throw AppException.NotFound("Image not found or already deleted.");

            var now = DateTime.UtcNow;
            var wasCover = target.IsCover;
            deletedUrl = target.ImageUrl;

            target.IsDeleted = true;
            target.IsCover = false;
            target.UpdatedAt = now;
            _marketImages.Update(target);
            await _marketImages.SaveChangesAsync();

            remaining = activeImages.Where(i => i.Id != imageId).OrderBy(i => i.DisplayOrder).ToList();
            for (int i = 0; i < remaining.Count; i++)
            {
                remaining[i].DisplayOrder = i;
            }

            if (wasCover)
            {
                if (remaining.Count > 0)
                {
                    remaining[0].IsCover = true;
                    market.ThumbnailUrl = remaining[0].ImageUrl;
                }
                else
                {
                    market.ThumbnailUrl = null;
                }
                market.UpdatedAt = now;
                _markets.Update(market);
            }

            if (remaining.Count > 0)
            {
                _marketImages.UpdateRange(remaining);
            }
            await _marketImages.SaveChangesAsync();
            if (wasCover)
            {
                await _markets.SaveChangesAsync();
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        var stillReferenced = string.Equals(market.ThumbnailUrl, deletedUrl, StringComparison.OrdinalIgnoreCase)
            || await _marketImages.AnyAsync(i => !i.IsDeleted && i.ImageUrl == deletedUrl);
        if (!stillReferenced)
        {
            await DeleteImageFileBestEffortAsync(deletedUrl, cancellationToken);
        }

        return ApiResponse<List<NightMarketImageResponse>>.SuccessResponse(MapImages(remaining), "Image deleted successfully.");
    }

    public async Task<ApiResponse<List<NightMarketImageResponse>>> SetCoverImageAsync(
        Guid marketId, Guid imageId, Guid? currentUserId, string currentUserRole, CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(marketId, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        EnsureOwnership(market, currentUserId, currentUserRole);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _marketImages.AcquireGalleryLockAsync(marketId, cancellationToken);

            var activeImages = await GetActiveImagesAsync(marketId);
            var target = activeImages.FirstOrDefault(i => i.Id == imageId);
            if (target == null)
                throw AppException.NotFound("Image not found.");

            var now = DateTime.UtcNow;
            foreach (var img in activeImages)
            {
                img.IsCover = false;
                img.UpdatedAt = now;
            }
            _marketImages.UpdateRange(activeImages);
            await _marketImages.SaveChangesAsync();

            target.IsCover = true;
            target.UpdatedAt = now;
            _marketImages.Update(target);
            await _marketImages.SaveChangesAsync();

            market.ThumbnailUrl = target.ImageUrl;
            market.UpdatedAt = now;
            _markets.Update(market);
            await _markets.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return ApiResponse<List<NightMarketImageResponse>>.SuccessResponse(MapImages(activeImages), "Cover image set successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

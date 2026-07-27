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
    private readonly IBoothRegistrationRepository _registrations;
    private readonly IMarketLayoutRepository _layouts;
    private readonly IZoneRepository _zones;
    private readonly IOrderRepository _orders;
    private readonly IFoodItemRepository _foodItems;

    public NightMarketService(
        INightMarketRepository markets,
        IMapper mapper,
        ISubscriptionEntitlementService entitlements,
        IBoothRepository booths,
        ISubscriptionRepository subscriptions,
        IBoothRegistrationRepository registrations,
        IMarketLayoutRepository layouts,
        IZoneRepository zones,
        IOrderRepository orders,
        IFoodItemRepository foodItems)
    {
        _markets = markets;
        _mapper = mapper;
        _entitlements = entitlements;
        _booths = booths;
        _subscriptions = subscriptions;
        _registrations = registrations;
        _layouts = layouts;
        _zones = zones;
        _orders = orders;
        _foodItems = foodItems;
    }

    public async Task<ApiResponse<PaginationResp<NightMarketListItemResponse>>> GetAllAsync(
        NightMarketListRequest request,
        CancellationToken cancellationToken = default)
    {
        var localTime = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(DateTime.UtcNow));
        var page = await _markets.GetCustomerPagedAsync(
            request.Keyword,
            request.OpenNow,
            localTime,
            request.Page,
            request.PageSize,
            request.SortBy,
            request.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase),
            cancellationToken);

        var items = page.Items.Select(MapListItem).ToList();
        return ApiResponse<PaginationResp<NightMarketListItemResponse>>.SuccessResponse(
            PaginationResp<NightMarketListItemResponse>.Create(items, page.TotalCount, request));
    }

    public async Task<ApiResponse<List<NightMarketOptionDto>>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var page = await _markets.GetCustomerPagedAsync(
            null, null, default, 1, 200, "name", true, cancellationToken);

        var options = page.Items.Select(m => new NightMarketOptionDto
        {
            Id = m.Id,
            Name = m.Name,
            Status = m.Status.ToString()
        }).ToList();

        return ApiResponse<List<NightMarketOptionDto>>.SuccessResponse(options);
    }

    public async Task<ApiResponse<NightMarketDetailResponse>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var market = await GetCustomerMarketAsync(id, cancellationToken);
        return ApiResponse<NightMarketDetailResponse>.SuccessResponse(MapDetail(market));
    }

    public async Task<ApiResponse<PaginationResp<NightMarketBoothListItemResponse>>> GetBoothsAsync(
        Guid id,
        PaginationReq pagination,
        CancellationToken cancellationToken = default)
    {
        await EnsureCustomerMarketExistsAsync(id, cancellationToken);
        var page = await _booths.GetCustomerByNightMarketPagedAsync(
            id, pagination.Page, pagination.PageSize, cancellationToken);
        var localTime = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(DateTime.UtcNow));
        var items = page.Items.Select(item => new NightMarketBoothListItemResponse
        {
            Id = item.Id,
            NightMarketId = item.NightMarketId,
            Name = item.Name,
            Description = item.Description,
            ThumbnailUrl = item.ThumbnailUrl,
            SlotNumber = item.SlotNumber,
            Latitude = item.Latitude,
            Longitude = item.Longitude,
            OpenTime = item.OpenTime,
            CloseTime = item.CloseTime,
            IsOpenNow = CustomerAvailability.IsOpenNow(item, localTime),
            AverageRating = item.AverageRating,
            IsFeatured = item.IsFeatured
        }).ToList();

        return ApiResponse<PaginationResp<NightMarketBoothListItemResponse>>.SuccessResponse(
            PaginationResp<NightMarketBoothListItemResponse>.Create(items, page.TotalCount, pagination));
    }

    public async Task<ApiResponse<PaginationResp<NightMarketFoodListItemResponse>>> GetFoodsAsync(
        Guid id,
        PaginationReq pagination,
        CancellationToken cancellationToken = default)
    {
        await EnsureCustomerMarketExistsAsync(id, cancellationToken);
        var page = await _foodItems.GetCustomerByNightMarketPagedAsync(
            id, DateTime.UtcNow, pagination.Page, pagination.PageSize, cancellationToken);
        var localTime = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(DateTime.UtcNow));
        var items = page.Items.Select(item => new NightMarketFoodListItemResponse
        {
            Id = item.Id,
            BoothId = item.BoothId,
            BoothName = item.BoothName,
            CategoryId = item.CategoryId,
            CategoryName = item.CategoryName,
            Name = item.Name,
            Description = item.Description,
            Price = item.Price,
            ThumbnailUrl = item.ThumbnailUrl,
            IsAvailable = item.IsAvailable,
            CanOrder = item.IsAvailable && CustomerAvailability.IsOpenNow(item, localTime),
            IsFeatured = item.IsFeatured
        }).ToList();

        return ApiResponse<PaginationResp<NightMarketFoodListItemResponse>>.SuccessResponse(
            PaginationResp<NightMarketFoodListItemResponse>.Create(items, page.TotalCount, pagination));
    }

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
            m.Status != NightMarketStatus.Cancelled);
        if (currentCount >= maxMarkets)
            throw AppException.Forbidden(
                $"Your current package allows a maximum of {maxMarkets} night market(s). Please upgrade to create more.",
                "MARKET_LIMIT_REACHED");

        var market = _mapper.Map<NightMarket>(request);
        var now = DateTime.UtcNow;
        market.Id = Guid.NewGuid();
        market.MarketOwnerId = marketOwnerId;
        market.Status = NightMarketStatus.Draft;
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

        _mapper.Map(request, market);
        market.Status = originalStatus;
        market.UpdatedAt = DateTime.UtcNow;

        _markets.Update(market);
        await _markets.SaveChangesAsync();
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
        var market = await GetCustomerMarketAsync(id, cancellationToken);

        if (!market.Latitude.HasValue ||
            !market.Longitude.HasValue ||
            !market.BoundaryWidthMeters.HasValue ||
            !market.BoundaryHeightMeters.HasValue)
            throw AppException.BadRequest("Night market geographic information is incomplete.");

        return ApiResponse<NightMarketNavigationInfoResponse>.SuccessResponse(new NightMarketNavigationInfoResponse
        {
            NightMarketId = market.Id,
            Name = market.Name,
            Address = market.Address,
            Destination = new GeographicCoordinateResponse
            {
                Latitude = market.Latitude.Value,
                Longitude = market.Longitude.Value
            },
            Boundary = new GeographicBoundaryResponse
            {
                WidthMeters = market.BoundaryWidthMeters.Value,
                HeightMeters = market.BoundaryHeightMeters.Value
            }
        });
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
            impact.PendingRegistrations,
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

        var pendingRegistrations = await _registrations.CountAsync(
            r => r.RequestedNightMarketId == nightMarketId
                && r.Status == BoothRegistrationStatus.PendingReview);
        var layouts = await _layouts.CountAsync(l => l.NightMarketId == nightMarketId);
        var zones = await _zones.CountAsync(z => z.NightMarketId == nightMarketId);

        return new NightMarketDeletionImpact(
            booths.Count(b => b.Status == BoothStatus.Active),
            openOrders,
            pendingRegistrations,
            layouts,
            zones);
    }

    private async Task<NightMarket?> GetActiveMarketAsync(Guid id, CancellationToken cancellationToken)
        => await _markets.GetActiveByIdAsync(id, cancellationToken);

    private async Task<NightMarketCustomerReadModel> GetCustomerMarketAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await _markets.GetCustomerByIdAsync(id, cancellationToken)
           ?? throw AppException.NotFound("Night market was not found.", "NIGHT_MARKET_NOT_FOUND");

    private async Task EnsureCustomerMarketExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await _markets.CustomerVisibleExistsAsync(id, cancellationToken))
            throw AppException.NotFound("Night market was not found.", "NIGHT_MARKET_NOT_FOUND");
    }

    private static NightMarketListItemResponse MapListItem(NightMarketCustomerReadModel market)
    {
        var availability = NightMarketAvailability.Evaluate(market, DateTime.UtcNow);
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

    private static NightMarketDetailResponse MapDetail(NightMarketCustomerReadModel market)
    {
        var listItem = MapListItem(market);
        return new NightMarketDetailResponse
        {
            Id = listItem.Id,
            Name = listItem.Name,
            Description = market.Description,
            Address = listItem.Address,
            Latitude = listItem.Latitude,
            Longitude = listItem.Longitude,
            ThumbnailUrl = listItem.ThumbnailUrl,
            ImageUrls = string.IsNullOrWhiteSpace(market.ThumbnailUrl) ? [] : [market.ThumbnailUrl],
            OpeningHours = listItem.OpeningHours,
            ClosingHours = listItem.ClosingHours,
            IsOpenNow = listItem.IsOpenNow,
            OpeningStatusText = listItem.OpeningStatusText,
            ActiveBoothCount = listItem.ActiveBoothCount,
            HasLayout = market.HasLayout,
            Status = listItem.Status
        };
    }

    private sealed record NightMarketDeletionImpact(
        int ActiveBooths,
        int OpenOrders,
        int PendingRegistrations,
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
            throw AppException.Conflict("Night market name already exists.");
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
}

using AutoMapper;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Subscriptions;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Booths;

public class BoothService : IBoothService
{
    private readonly IBoothRepository _booths;
    private readonly IZoneRepository _zones;
    private readonly IMapper _mapper;
    private readonly IBoothLocationRepository _locations;
    private readonly ISubscriptionEntitlementService _entitlements;
    private readonly INightMarketRepository _nightMarkets;
    private readonly IUserRepository _users;
    private readonly IGenericRepository<Role> _roles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILayoutNodeRepository _layoutNodes;
    private readonly IMarketLayoutRepository _marketLayouts;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ApplicationLayer.Services.Storage.IFileStorageService _fileStorage;
    private readonly IGenericRepository<ModerationActionHistory> _moderationHistory;
    public BoothService(IBoothRepository booths, IZoneRepository zones, IMapper mapper, IBoothLocationRepository locations, ISubscriptionEntitlementService entitlements, INightMarketRepository nightMarkets, IUserRepository users, IGenericRepository<Role> roles, IUnitOfWork unitOfWork, ILayoutNodeRepository layoutNodes, IMarketLayoutRepository marketLayouts, ISubscriptionRepository subscriptions, ApplicationLayer.Services.Storage.IFileStorageService fileStorage, IGenericRepository<ModerationActionHistory> moderationHistory)
    {
        _booths = booths;
        _zones = zones;
        _mapper = mapper;
        _locations = locations;
        _entitlements = entitlements;
        _nightMarkets = nightMarkets;
        _users = users;
        _roles = roles;
        _unitOfWork = unitOfWork;
        _layoutNodes = layoutNodes;
        _marketLayouts = marketLayouts;
        _subscriptions = subscriptions;
        _fileStorage = fileStorage;
        _moderationHistory = moderationHistory;
    }

    public async Task<ApiResponse<BoothResponse>> GetMyBoothAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        var booth = await _booths.GetByOwnerIdAsync(ownerId, cancellationToken);
        if (booth is null)
            throw AppException.NotFound("You do not have a booth.");

        var response = _mapper.Map<BoothResponse>(booth);
        if (booth.Status == BoothStatus.Banned)
        {
            var bannedStatus = BoothStatus.Banned.ToString();
            var history = await _moderationHistory.FindAsync(h => h.BoothId == booth.Id && h.NewStatus == bannedStatus);
            response.BanReason = history.OrderByDescending(h => h.CreatedAt).FirstOrDefault()?.Reason;
        }

        return ApiResponse<BoothResponse>.SuccessResponse(response);
    }

    public async Task<ApiResponse<BoothResponse>> UpdateMyBoothAsync(
        Guid ownerId,
        UpdateMyBoothRequest request,
        CancellationToken cancellationToken = default)
    {
        var booth = await _booths.GetByOwnerIdAsync(ownerId, cancellationToken);
        if (booth is null)
            throw AppException.NotFound("You do not have a booth.");

        ValidateBoothFields(request.BoothName, request.PhoneNumber, request.OpenTime, request.CloseTime);
        var oldThumbnailUrl = booth.ThumbnailUrl;

        _mapper.Map(request, booth);
        booth.BoothName = booth.BoothName.Trim();
        booth.PhoneNumber = TextHelper.NormalizePhoneNumber(request.PhoneNumber);
        booth.UpdatedAt = DateTime.UtcNow;

        _booths.Update(booth); await _booths.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(oldThumbnailUrl) && oldThumbnailUrl != booth.ThumbnailUrl)
        {
            await _fileStorage.DeleteImageIfManagedAsync(oldThumbnailUrl, cancellationToken);
        }

        return ApiResponse<BoothResponse>.SuccessResponse(_mapper.Map<BoothResponse>(booth), "Booth updated successfully.");
    }

    public async Task<ApiResponse<BoothResponse>> TogglePauseMyBoothAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var booth = await _booths.GetByOwnerIdAsync(ownerId, cancellationToken);
        if (booth is null)
            throw AppException.NotFound("You do not have a booth.");

        if (booth.Status is BoothStatus.Banned)
            throw AppException.BadRequest("This booth has been banned by the platform.", "BOOTH_BANNED");

        await _entitlements.RequireBoothFeatureAsync(booth.Id, e => e.PauseBooth,
            "Your current plan does not include booth pausing. Upgrade to Booth Boost or Booth Featured to use this feature.",
            "PAUSE_BOOTH_NOT_INCLUDED");

        if (booth.Status == BoothStatus.Active)
        {
            booth.Status = BoothStatus.Inactive;
            await _locations.ReleaseAsync(booth.Id, DateTime.UtcNow, cancellationToken);
            booth.ZoneId = null;
        }
        else if (booth.Status == BoothStatus.Inactive)
        {
            booth.Status = BoothStatus.Active;
        }

        booth.UpdatedAt = DateTime.UtcNow;
        _booths.Update(booth);
        await _booths.SaveChangesAsync();

        var message = booth.Status == BoothStatus.Inactive ? "Booth has been paused successfully." : "Booth has been resumed successfully.";
        return ApiResponse<BoothResponse>.SuccessResponse(_mapper.Map<BoothResponse>(booth), message);
    }

    public async Task<ApiResponse<PaginationResp<BoothResponse>>> GetAllAsync(PaginationReq pagination, Guid? nightMarketId = null, CancellationToken cancellationToken = default)
    {
        var booths = await _booths.GetAllAsync();
        var query = booths.AsQueryable();

        if (nightMarketId.HasValue)
        {
            query = query.Where(b => b.NightMarketId == nightMarketId.Value);
        }

        var totalElements = query.Count();
        var pagedBooths = query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToList();

        var responses = pagedBooths.Select(b => _mapper.Map<BoothResponse>(b)).ToList();
        var paginatedResp = PaginationResp<BoothResponse>.Create(responses, totalElements, pagination);

        return ApiResponse<PaginationResp<BoothResponse>>.SuccessResponse(paginatedResp);
    }

    public async Task<ApiResponse<BoothResponse>> UpdateByAdminAsync(Guid boothId, AdminUpdateBoothRequest request, CancellationToken cancellationToken = default)
    {
        var booth = await _booths.GetByIdAsync(boothId);
        if (booth is null)
            throw AppException.NotFound("Booth was not found.");

        ValidateBoothFields(request.BoothName, request.PhoneNumber, request.OpenTime, request.CloseTime);
        if (request.ZoneId.HasValue && (await _zones.GetActiveByIdAsync(request.ZoneId.Value))?.NightMarketId != booth.NightMarketId)
            throw AppException.BadRequest("The assigned zone does not belong to this booth's night market.");

        if (request.IsFeatured && !booth.IsFeatured)
        {
            await _entitlements.RequireBoothFeatureAsync(booth.Id, e => e.FeaturedBooth,
                "This booth's subscription does not include the featured booth feature. Please upgrade to Booth Featured.",
                "FEATURED_BOOTH_NOT_INCLUDED");
        }

        _mapper.Map(request, booth);
        booth.BoothName = booth.BoothName.Trim();
        booth.PhoneNumber = TextHelper.NormalizePhoneNumber(request.PhoneNumber);
        booth.UpdatedAt = DateTime.UtcNow;

        _booths.Update(booth);
        await _booths.SaveChangesAsync();
        return ApiResponse<BoothResponse>.SuccessResponse(_mapper.Map<BoothResponse>(booth), "Booth updated successfully by the administrator.");
    }

    public async Task<ApiResponse<BoothNavigationInfoResponse>> GetBoothNavigationInfoAsync(Guid boothId, CancellationToken cancellationToken = default)
    {
        var booth = await _booths.GetByIdAsync(boothId);
        if (booth is null)
            throw AppException.NotFound("Booth not found.", "BOOTH_NOT_FOUND");

        var market = await _nightMarkets.GetByIdAsync(booth.NightMarketId);
        if (market is null)
            throw AppException.NotFound("Night market not found.", "MARKET_NOT_FOUND");

        var location = await _locations.GetCurrentByBoothAsync(boothId, cancellationToken);

        GeographicCoordinateResponse? boothCoord = null;
        if (location != null)
        {
            boothCoord = new GeographicCoordinateResponse
            {
                Latitude = location.Ycoordinate,
                Longitude = location.Xcoordinate
            };
        }

        GeographicCoordinateResponse? marketCenter = null;
        if (market.Latitude != null && market.Longitude != null)
        {
            marketCenter = new GeographicCoordinateResponse
            {
                Latitude = market.Latitude.Value,
                Longitude = market.Longitude.Value
            };
        }

        GeographicBoundaryResponse? marketBoundary = null;
        if (market.BoundaryWidthMeters.HasValue && market.BoundaryHeightMeters.HasValue)
        {
            marketBoundary = new GeographicBoundaryResponse
            {
                WidthMeters = market.BoundaryWidthMeters.Value,
                HeightMeters = market.BoundaryHeightMeters.Value
            };
        }

        var response = new BoothNavigationInfoResponse
        {
            BoothId = booth.Id,
            BoothName = booth.BoothName,
            NightMarketId = market.Id,
            NightMarketName = market.Name,
            NightMarketAddress = market.Address,
            BoothCoordinate = boothCoord,
            NightMarketCenter = marketCenter,
            NightMarketBoundary = marketBoundary
        };

        return ApiResponse<BoothNavigationInfoResponse>.SuccessResponse(response);
    }

    private async Task<NightMarket> VerifyMarketOwnershipAsync(Guid marketOwnerId, Guid marketId, CancellationToken cancellationToken)
    {
        var ownedMarkets = await _nightMarkets.GetByOwnerIdAsync(marketOwnerId, cancellationToken);
        var market = ownedMarkets.FirstOrDefault(item => item.Id == marketId);
        if (market is null)
            throw AppException.Forbidden("You do not own this night market.", "NOT_MARKET_OWNER");
        return market;
    }

    private async Task<Booth> GetBoothOwnedByMarketOwnerAsync(Guid marketOwnerId, Guid boothId, CancellationToken cancellationToken)
    {
        var booth = await _booths.GetByIdAsync(boothId);
        if (booth is null)
            throw AppException.NotFound("Booth not found.", "BOOTH_NOT_FOUND");
        await VerifyMarketOwnershipAsync(marketOwnerId, booth.NightMarketId, cancellationToken);
        return booth;
    }

    private MarketOwnerBoothResponse MapMarketOwnerBooth(Booth booth, User? owner, NightMarket? market, Zone? zone)
    {
        return new MarketOwnerBoothResponse
        {
            Id = booth.Id,
            NightMarketId = booth.NightMarketId,
            BoothOwnerId = booth.BoothOwnerId,
            ZoneId = booth.ZoneId,
            BoothName = booth.BoothName,
            BoothCode = booth.BoothCode,
            Description = booth.Description,
            PhoneNumber = booth.PhoneNumber,
            SlotNumber = booth.SlotNumber,
            ThumbnailUrl = booth.ThumbnailUrl,
            OpenTime = booth.OpenTime,
            CloseTime = booth.CloseTime,
            Status = booth.Status.ToString(),
            OwnerName = owner?.FullName,
            OwnerEmail = owner?.Email,
            OwnerPhone = owner?.Phone,
            NightMarketName = market?.Name,
            ZoneName = zone?.ZoneName,
            AverageRating = null,
            IsFeatured = false
        };
    }

    private static void ValidateBoothFields(
        string boothName,
        string? phoneNumber,
        TimeOnly? openTime,
        TimeOnly? closeTime)
    {
        var fieldErrors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(boothName))
            fieldErrors["boothName"] = new[] { "Booth name is required." };
        if (!string.IsNullOrWhiteSpace(phoneNumber) && !TextHelper.IsValidPhoneNumber(phoneNumber))
            fieldErrors["phoneNumber"] = new[] { "Invalid phone number format." };
        if (openTime.HasValue != closeTime.HasValue)
            fieldErrors["openTime"] = new[] { "Opening hours and closing hours must be provided together." };

        if (fieldErrors.Count > 0)
            throw AppException.Validation("Please correct the highlighted fields.", fieldErrors, "VALIDATION_ERROR");
    }

    public async Task<ApiResponse<PaginationResp<MarketOwnerBoothResponse>>> GetByMarketOwnerAsync(
        Guid marketOwnerId, Guid? marketId, string? keyword, string? status,
        PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var ownerMarkets = await _nightMarkets.GetByOwnerIdAsync(marketOwnerId, cancellationToken);
        var ownerMarketIds = ownerMarkets.Select(item => item.Id).ToHashSet();
        if (marketId.HasValue && !ownerMarketIds.Contains(marketId.Value))
            throw AppException.Forbidden("You do not own this night market.", "NOT_MARKET_OWNER");

        var booths = await _booths.FindAsync(booth =>
            ownerMarketIds.Contains(booth.NightMarketId)
            && (!marketId.HasValue || booth.NightMarketId == marketId.Value));
        var boothOwnerIds = booths.Select(booth => booth.BoothOwnerId).ToHashSet();
        var users = await _users.FindAsync(user => boothOwnerIds.Contains(user.Id));
        var userById = users.ToDictionary(user => user.Id);
        var query = booths.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BoothStatus>(status, true, out var statusEnum))
        {
            query = query.Where(b => b.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(booth =>
                booth.BoothName.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase)
                || (userById.TryGetValue(booth.BoothOwnerId, out var owner)
                    && (owner.FullName.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase)
                        || owner.Email.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase))));
        }

        var totalElements = query.Count();
        var pagedBooths = query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToList();

        var responses = new List<MarketOwnerBoothResponse>();
        var marketDict = ownerMarkets.ToDictionary(m => m.Id);

        foreach (var booth in pagedBooths)
        {
            userById.TryGetValue(booth.BoothOwnerId, out var owner);
            marketDict.TryGetValue(booth.NightMarketId, out var market);
            Zone? zone = null;
            if (booth.ZoneId.HasValue)
                zone = await _zones.GetByIdAsync(booth.ZoneId.Value);

            responses.Add(MapMarketOwnerBooth(booth, owner, market, zone));
        }

        var paginatedResp = PaginationResp<MarketOwnerBoothResponse>.Create(responses, totalElements, pagination);
        return ApiResponse<PaginationResp<MarketOwnerBoothResponse>>.SuccessResponse(paginatedResp);
    }

    public async Task<ApiResponse<MarketOwnerBoothResponse>> CreateByMarketOwnerAsync(
        Guid marketOwnerId, Guid marketId, MarketOwnerCreateBoothRequest request, CancellationToken cancellationToken = default)
    {
        var market = await VerifyMarketOwnershipAsync(marketOwnerId, marketId, cancellationToken);

        if (market.ModerationStatus == ModerationStatus.Suspended)
            throw AppException.BadRequest("Cannot create a booth in a suspended market.", "MARKET_NOT_AVAILABLE");

        var fieldErrors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.BoothName))
            fieldErrors["boothName"] = new[] { "Booth name is required." };

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && !TextHelper.IsValidPhoneNumber(request.PhoneNumber))
            fieldErrors["phoneNumber"] = new[] { "Invalid phone number format." };

        if (request.OpenTime.HasValue != request.CloseTime.HasValue)
            fieldErrors["openTime"] = new[] { "Opening hours and closing hours must be provided together." };

        if (fieldErrors.Count > 0)
            throw AppException.Validation("Please correct the highlighted fields.", fieldErrors, "VALIDATION_ERROR");

        // Verify booth owner exists and has BoothOwner role
        var boothOwner = await _users.GetByIdAsync(request.BoothOwnerId);
        if (boothOwner is null)
            throw AppException.NotFound("Booth owner user not found.", "OWNER_NOT_FOUND");

        var role = await _roles.GetByIdAsync(boothOwner.RoleId);
        if (role?.RoleName != "BoothOwner")
            throw AppException.BadRequest("The specified user does not have the BoothOwner role.", "USER_NOT_BOOTH_OWNER");

        // Check if booth owner already has a booth (unique constraint)
        var existingBooth = await _booths.GetByOwnerIdAsync(boothOwner.Id, cancellationToken);
        if (existingBooth is not null)
            throw AppException.Conflict("This BoothOwner already has a booth.", "BOOTH_OWNER_ALREADY_HAS_BOOTH");

        // Create the booth with Inactive status (must be assigned to a slot before activating)
        var now = DateTime.UtcNow;
        var booth = new Booth
        {
            Id = Guid.NewGuid(),
            NightMarketId = marketId,
            BoothOwnerId = boothOwner.Id,
            BoothName = request.BoothName.Trim(),
            Description = request.Description,
            PhoneNumber = TextHelper.NormalizePhoneNumber(request.PhoneNumber),
            ThumbnailUrl = request.ThumbnailUrl,
            OpenTime = request.OpenTime,
            CloseTime = request.CloseTime,
            Status = DomainLayer.Enums.GeneralEnum.BoothStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _booths.AddAsync(booth);

            var freePackage = await _subscriptions.GetPackageByCodeAsync("BOOTH_FREE", cancellationToken);
            if (IsValidFreeBoothPackage(freePackage))
            {
                await _subscriptions.AddBoothSubscriptionAsync(new BoothSubscription
                {
                    Id = Guid.NewGuid(),
                    BoothId = booth.Id,
                    PackageId = freePackage!.Id,
                    StartDate = now,
                    EndDate = DateTime.MaxValue,
                    Status = SubscriptionStatus.Active,
                    PaidAmount = 0,
                    ChangeType = "FreeDefault",
                    CreatedAt = now,
                    UpdatedAt = now
                }, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return ApiResponse<MarketOwnerBoothResponse>.SuccessResponse(
            MapMarketOwnerBooth(booth, boothOwner, market, null),
            "Booth created successfully. Status is Inactive until assigned to a slot.");
    }

    public async Task<ApiResponse<MarketOwnerBoothResponse>> UpdateByMarketOwnerAsync(
        Guid marketOwnerId, Guid boothId, MarketOwnerUpdateBoothRequest request, CancellationToken cancellationToken = default)
    {
        var booth = await GetBoothOwnedByMarketOwnerAsync(marketOwnerId, boothId, cancellationToken);

        var fieldErrors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.BoothName))
            fieldErrors["boothName"] = new[] { "Booth name is required." };

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && !TextHelper.IsValidPhoneNumber(request.PhoneNumber))
            fieldErrors["phoneNumber"] = new[] { "Invalid phone number format." };

        if (request.OpenTime.HasValue != request.CloseTime.HasValue)
            fieldErrors["openTime"] = new[] { "Opening hours and closing hours must be provided together." };

        if (fieldErrors.Count > 0)
            throw AppException.Validation("Please correct the highlighted fields.", fieldErrors, "VALIDATION_ERROR");

        booth.BoothName = request.BoothName.Trim();
        booth.Description = request.Description;
        booth.PhoneNumber = TextHelper.NormalizePhoneNumber(request.PhoneNumber);
        // Only overwrite image if a new one is provided
        if (!string.IsNullOrWhiteSpace(request.ThumbnailUrl))
            booth.ThumbnailUrl = request.ThumbnailUrl;
        booth.OpenTime = request.OpenTime;
        booth.CloseTime = request.CloseTime;
        booth.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _booths.Update(booth);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        var owner = await _users.GetByIdAsync(booth.BoothOwnerId);
        var market = await _nightMarkets.GetByIdAsync(booth.NightMarketId);
        Zone? zone = null;
        if (booth.ZoneId.HasValue) zone = await _zones.GetByIdAsync(booth.ZoneId.Value);

        return ApiResponse<MarketOwnerBoothResponse>.SuccessResponse(
            MapMarketOwnerBooth(booth, owner, market, zone),
            "Booth updated successfully.");
    }

    public async Task<ApiResponse<MarketOwnerBoothResponse>> ChangeStatusByMarketOwnerAsync(
        Guid marketOwnerId, Guid boothId, MarketOwnerChangeBoothStatusRequest request, CancellationToken cancellationToken = default)
    {
        var booth = await GetBoothOwnedByMarketOwnerAsync(marketOwnerId, boothId, cancellationToken);

        if (booth.Status == DomainLayer.Enums.GeneralEnum.BoothStatus.Banned)
            throw AppException.BadRequest("This booth has been banned by the platform.", "BOOTH_BANNED");

        // Market owners can only set Active or Inactive
        if (request.Status is not DomainLayer.Enums.GeneralEnum.BoothStatus.Active
            and not DomainLayer.Enums.GeneralEnum.BoothStatus.Inactive)
        {
            throw AppException.BadRequest("Market owners can only set status to Active or Inactive.", "INVALID_STATUS_FOR_MARKET_OWNER");
        }

        // To activate, booth must have an active slot assignment
        if (request.Status == DomainLayer.Enums.GeneralEnum.BoothStatus.Active)
        {
            var currentLocation = await _locations.GetCurrentByBoothAsync(booth.Id, cancellationToken);
            if (currentLocation is null)
                throw AppException.BadRequest("Cannot activate a booth that has not been assigned to a slot.", "BOOTH_NOT_ASSIGNED_TO_SLOT");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            booth.Status = request.Status;
            booth.UpdatedAt = DateTime.UtcNow;
            _booths.Update(booth);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        var owner = await _users.GetByIdAsync(booth.BoothOwnerId);
        var market = await _nightMarkets.GetByIdAsync(booth.NightMarketId);
        Zone? zone = null;
        if (booth.ZoneId.HasValue) zone = await _zones.GetByIdAsync(booth.ZoneId.Value);

        return ApiResponse<MarketOwnerBoothResponse>.SuccessResponse(
            MapMarketOwnerBooth(booth, owner, market, zone),
            $"Booth status changed to {request.Status}.");
    }

    public async Task<ApiResponse<MarketOwnerBoothResponse>> CreateAndAssignBoothAsync(
        Guid marketOwnerId, Guid marketId, Guid layoutId, Guid nodeId, MarketOwnerCreateBoothRequest request, CancellationToken cancellationToken = default)
    {
        await VerifyMarketOwnershipAsync(marketOwnerId, marketId, cancellationToken);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _locations.AcquireMarketAssignmentLockAsync(marketId, cancellationToken);
            await VerifyMarketOwnershipAsync(marketOwnerId, marketId, cancellationToken);

            var fieldErrors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(request.BoothName))
                fieldErrors["boothName"] = new[] { "Booth name is required." };

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && !TextHelper.IsValidPhoneNumber(request.PhoneNumber))
                fieldErrors["phoneNumber"] = new[] { "Invalid phone number format." };

            if ((request.OpenTime.HasValue && !request.CloseTime.HasValue) ||
                (!request.OpenTime.HasValue && request.CloseTime.HasValue))
                fieldErrors["openTime"] = new[] { "Opening hours and closing hours must be provided together." };

            if (fieldErrors.Count > 0)
                throw AppException.Validation("Please correct the highlighted fields.", fieldErrors, "VALIDATION_ERROR");


            var layout = await _marketLayouts.GetByIdAsync(layoutId);
            if (layout is null || layout.NightMarketId != marketId)
                throw AppException.NotFound("Layout not found in this market.", "LAYOUT_NOT_FOUND");
            if (layout.Status != DomainLayer.Enums.GeneralEnum.MarketLayoutStatus.Active)
                throw AppException.BadRequest("The layout must be active to assign a booth.", "LAYOUT_NOT_ACTIVE");

            var node = await _layoutNodes.GetByIdAsync(nodeId);
            if (node is null || node.LayoutId != layoutId)
                throw AppException.NotFound("Slot not found in this layout.", "SLOT_NOT_FOUND");
            if (node.NodeType != DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot)
                throw AppException.BadRequest("The selected map position is not a booth slot.", "INVALID_NODE_TYPE");
            if (await _locations.GetCurrentByNodeAsync(nodeId, cancellationToken) is not null)
                throw AppException.Conflict("This slot is already occupied.", "SLOT_OCCUPIED");

            var activeBoothCount = await _locations.CountActiveByNightMarketAsync(marketId, cancellationToken);
            await _entitlements.RequireMarketFeatureAsync(
                marketOwnerId,
                entitlement => activeBoothCount < entitlement.MaxSlotsPerMarket,
                "Your current package has reached its booth slot limit.",
                "PLAN_LIMIT_REACHED");
            if (node.ZoneId.HasValue)
            {
                var nodeZone = await _zones.GetByIdAsync(node.ZoneId.Value);
                var isGeneralArea = string.Equals(nodeZone?.ZoneCode, "G", StringComparison.OrdinalIgnoreCase);
                if (!isGeneralArea)
                {
                    await _entitlements.RequireMarketFeatureAsync(
                        marketOwnerId,
                        entitlement => entitlement.ZoneManagement,
                        "Zone management is available with the Pro Market package.",
                        "ZONE_MANAGEMENT_NOT_INCLUDED");
                }
            }

            var boothOwner = await _users.GetByIdAsync(request.BoothOwnerId);
            if (boothOwner is null)
                throw AppException.NotFound("Booth owner user not found.", "OWNER_NOT_FOUND");
            var role = await _roles.GetByIdAsync(boothOwner.RoleId);
            if (role?.RoleName != "BoothOwner")
                throw AppException.BadRequest("The specified user does not have the BoothOwner role.", "USER_NOT_BOOTH_OWNER");
            if (await _booths.GetByOwnerIdAsync(boothOwner.Id, cancellationToken) is not null)
                throw AppException.Conflict("This Booth Owner already has a booth.", "OWNER_ALREADY_HAS_BOOTH");

            var freePackage = await _subscriptions.GetPackageByCodeAsync("BOOTH_FREE", cancellationToken);

            var now = DateTime.UtcNow;
            var booth = new Booth
            {
                Id = Guid.NewGuid(),
                NightMarketId = marketId,
                BoothOwnerId = boothOwner.Id,
                BoothName = request.BoothName.Trim(),
                Description = request.Description,
                PhoneNumber = TextHelper.NormalizePhoneNumber(request.PhoneNumber),
                ThumbnailUrl = request.ThumbnailUrl,
                OpenTime = request.OpenTime,
                CloseTime = request.CloseTime,
                Status = DomainLayer.Enums.GeneralEnum.BoothStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                ZoneId = node.ZoneId
            };
            var boothLocation = new BoothLocation
            {
                Id = Guid.NewGuid(),
                BoothId = booth.Id,
                LayoutId = layoutId,
                LayoutNodeId = nodeId,
                ZoneId = node.ZoneId,
                SlotNumber = node.SlotCode ?? node.NodeName,
                Xcoordinate = node.Xcoordinate,
                Ycoordinate = node.Ycoordinate,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _booths.AddAsync(booth);
            await _locations.AddAsync(boothLocation);
            if (IsValidFreeBoothPackage(freePackage))
            {
                await _subscriptions.AddBoothSubscriptionAsync(new BoothSubscription
                {
                    Id = Guid.NewGuid(),
                    BoothId = booth.Id,
                    PackageId = freePackage!.Id,
                    StartDate = now,
                    EndDate = DateTime.MaxValue,
                    Status = DomainLayer.Enums.GeneralEnum.SubscriptionStatus.Active,
                    PaidAmount = 0,
                    ChangeType = "FreeDefault",
                    CreatedAt = now,
                    UpdatedAt = now
                }, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var market = await _nightMarkets.GetByIdAsync(marketId);
            Zone? zone = null;
            if (node.ZoneId.HasValue) zone = await _zones.GetByIdAsync(node.ZoneId.Value);
            return ApiResponse<MarketOwnerBoothResponse>.SuccessResponse(
                MapMarketOwnerBooth(booth, boothOwner, market, zone),
                "Booth created and assigned successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<MarketOwnerBoothResponse>> AssignBoothAsync(
        Guid marketOwnerId, Guid marketId, Guid layoutId, Guid nodeId, Guid boothId, CancellationToken cancellationToken = default)
    {
        await VerifyMarketOwnershipAsync(marketOwnerId, marketId, cancellationToken);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _locations.AcquireMarketAssignmentLockAsync(marketId, cancellationToken);
            await VerifyMarketOwnershipAsync(marketOwnerId, marketId, cancellationToken);

            var layout = await _marketLayouts.GetByIdAsync(layoutId);
            if (layout is null || layout.NightMarketId != marketId)
                throw AppException.NotFound("Layout not found in this market.", "LAYOUT_NOT_FOUND");
            if (layout.Status != DomainLayer.Enums.GeneralEnum.MarketLayoutStatus.Active)
                throw AppException.BadRequest("The layout must be active to assign a booth.", "LAYOUT_NOT_ACTIVE");

            var node = await _layoutNodes.GetByIdAsync(nodeId);
            if (node is null || node.LayoutId != layoutId)
                throw AppException.NotFound("Slot not found in this layout.", "SLOT_NOT_FOUND");
            if (node.NodeType != DomainLayer.Enums.GeneralEnum.LayoutNodeType.BoothSlot)
                throw AppException.BadRequest("The selected map position is not a booth slot.", "INVALID_NODE_TYPE");

            var targetLocation = await _locations.GetCurrentByNodeAsync(nodeId, cancellationToken);
            if (targetLocation is not null && targetLocation.BoothId != boothId)
                throw AppException.Conflict("This slot is already occupied.", "SLOT_OCCUPIED");

            var booth = await GetBoothOwnedByMarketOwnerAsync(marketOwnerId, boothId, cancellationToken);
            if (booth.Status == DomainLayer.Enums.GeneralEnum.BoothStatus.Banned)
                throw AppException.BadRequest("This booth has been banned by the platform.", "BOOTH_BANNED");

            var currentLocation = await _locations.GetCurrentByBoothAsync(booth.Id, cancellationToken);
            if (currentLocation is null)
            {
                var activeBoothCount = await _locations.CountActiveByNightMarketAsync(marketId, cancellationToken);
                await _entitlements.RequireMarketFeatureAsync(
                    marketOwnerId,
                    entitlement => activeBoothCount < entitlement.MaxSlotsPerMarket,
                    "Your current package has reached its booth slot limit.",
                    "PLAN_LIMIT_REACHED");
            }
            if (node.ZoneId.HasValue)
            {
                var nodeZone = await _zones.GetByIdAsync(node.ZoneId.Value);
                var isGeneralArea = string.Equals(nodeZone?.ZoneCode, "G", StringComparison.OrdinalIgnoreCase);
                if (!isGeneralArea)
                {
                    await _entitlements.RequireMarketFeatureAsync(
                        marketOwnerId,
                        entitlement => entitlement.ZoneManagement,
                        "Zone management is available with the Pro Market package.",
                        "ZONE_MANAGEMENT_NOT_INCLUDED");
                }
            }

            var now = DateTime.UtcNow;
            await EnsureDefaultBoothSubscriptionAsync(booth.Id, now, cancellationToken);

            var newLocation = new BoothLocation
            {
                Id = Guid.NewGuid(),
                BoothId = booth.Id,
                LayoutId = layoutId,
                LayoutNodeId = nodeId,
                ZoneId = node.ZoneId,
                SlotNumber = node.SlotCode ?? node.NodeName,
                Xcoordinate = node.Xcoordinate,
                Ycoordinate = node.Ycoordinate,
                CreatedAt = now,
                UpdatedAt = now
            };

            booth.Status = DomainLayer.Enums.GeneralEnum.BoothStatus.Active;
            booth.ZoneId = node.ZoneId;
            booth.UpdatedAt = now;
            _booths.Update(booth);

            if (targetLocation is null)
                await _locations.AssignOrMoveAsync(newLocation, now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var owner = await _users.GetByIdAsync(booth.BoothOwnerId);
            var market = await _nightMarkets.GetByIdAsync(marketId);
            Zone? zone = null;
            if (node.ZoneId.HasValue) zone = await _zones.GetByIdAsync(node.ZoneId.Value);
            return ApiResponse<MarketOwnerBoothResponse>.SuccessResponse(
                MapMarketOwnerBooth(booth, owner, market, zone),
                targetLocation is null ? "Booth assigned successfully." : "Booth is already assigned to this slot.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task EnsureDefaultBoothSubscriptionAsync(
        Guid boothId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var activeSubscription = await _subscriptions.GetActiveBoothSubscriptionAsync(boothId, cancellationToken);
        if (activeSubscription is not null)
            return;

        var freePackage = await _subscriptions.GetPackageByCodeAsync("BOOTH_FREE", cancellationToken);

        // Booth Basic is the built-in entitlement for every booth. A damaged or
        // temporarily incomplete package catalogue must never prevent a Market
        // Owner from assigning an otherwise valid booth to a slot. The
        // entitlement service already falls back to Booth Basic when there is
        // no active subscription record. When the canonical package is valid,
        // we still persist the explicit free subscription for a complete audit
        // history.
        if (!IsValidFreeBoothPackage(freePackage))
        {
            return;
        }

        await _subscriptions.AddBoothSubscriptionAsync(new BoothSubscription
        {
            Id = Guid.NewGuid(),
            BoothId = boothId,
            PackageId = freePackage!.Id,
            StartDate = now,
            EndDate = DateTime.MaxValue,
            Status = SubscriptionStatus.Active,
            PaidAmount = 0,
            ChangeType = "FreeDefault",
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);
    }

    private static bool IsValidFreeBoothPackage(Package? package)
        => package is not null
           && package.Type == PackageType.Booth
           && package.Price == 0
           && package.Status == PackageStatus.Active;

    public async Task<ApiResponse<MarketOwnerBoothResponse>> ReleaseSlotBoothAsync(
        Guid marketOwnerId, Guid marketId, Guid layoutId, Guid nodeId, CancellationToken cancellationToken = default)
    {
        var market = await VerifyMarketOwnershipAsync(marketOwnerId, marketId, cancellationToken);
        if (market.ModerationStatus == ModerationStatus.Suspended)
            throw AppException.BadRequest("Cannot release a booth in a suspended market.", "MARKET_NOT_AVAILABLE");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _locations.AcquireMarketAssignmentLockAsync(marketId, cancellationToken);
            await VerifyMarketOwnershipAsync(marketOwnerId, marketId, cancellationToken);

            var layout = await _marketLayouts.GetByIdAsync(layoutId);
            if (layout is null || layout.NightMarketId != marketId)
                throw AppException.NotFound("Layout not found in this market.", "LAYOUT_NOT_FOUND");

            var node = await _layoutNodes.GetByIdAsync(nodeId);
            if (node is null || node.LayoutId != layoutId)
                throw AppException.NotFound("Slot not found in this layout.", "SLOT_NOT_FOUND");
            if (node.NodeType != LayoutNodeType.BoothSlot)
                throw AppException.BadRequest("The selected map position is not a booth slot.", "INVALID_NODE_TYPE");

            var targetLocation = await _locations.GetCurrentByNodeAsync(nodeId, cancellationToken);
            if (targetLocation is null)
                throw AppException.NotFound("No booth is currently assigned to this slot.", "SLOT_NOT_OCCUPIED");

            var booth = await GetBoothOwnedByMarketOwnerAsync(marketOwnerId, targetLocation.BoothId, cancellationToken);
            if (booth.Status == DomainLayer.Enums.GeneralEnum.BoothStatus.Banned)
                throw AppException.BadRequest("This booth has been banned by the platform.", "BOOTH_BANNED");

            var now = DateTime.UtcNow;
            await _locations.ReleaseAsync(booth.Id, now, cancellationToken);

            booth.Status = DomainLayer.Enums.GeneralEnum.BoothStatus.Inactive;
            booth.ZoneId = null;
            booth.UpdatedAt = now;
            _booths.Update(booth);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var owner = await _users.GetByIdAsync(booth.BoothOwnerId);
            return ApiResponse<MarketOwnerBoothResponse>.SuccessResponse(
                MapMarketOwnerBooth(booth, owner, market, null),
                "Booth released from slot successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<bool>> DeleteByMarketOwnerAsync(
        Guid marketOwnerId, Guid boothId, CancellationToken cancellationToken = default)
    {
        var booth = await GetBoothOwnedByMarketOwnerAsync(marketOwnerId, boothId, cancellationToken);
        if (booth.Status == DomainLayer.Enums.GeneralEnum.BoothStatus.Banned)
            throw AppException.BadRequest("This booth has been banned by the platform and cannot be deleted.", "BOOTH_BANNED");

        var currentLocation = await _locations.GetCurrentByBoothAsync(booth.Id, cancellationToken);
        if (currentLocation is not null)
            throw AppException.BadRequest("Cannot delete a booth that is currently assigned to a slot. Please release it from the slot first.", "BOOTH_HAS_ACTIVE_SLOT");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            booth.Status = BoothStatus.Inactive;
            booth.UpdatedAt = DateTime.UtcNow;
            _booths.Update(booth);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return ApiResponse<bool>.SuccessResponse(true, "Booth deactivated successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<List<BoothOwnerSearchResponse>>> SearchBoothOwnersAsync(
        string? keyword, CancellationToken cancellationToken = default)
    {
        var boothOwnerRole = await _roles.FirstOrDefaultAsync(role => role.RoleName == "BoothOwner");
        if (boothOwnerRole == null)
            return ApiResponse<List<BoothOwnerSearchResponse>>.SuccessResponse(new List<BoothOwnerSearchResponse>());

        var users = await _users.FindAsync(u => u.RoleId == boothOwnerRole.Id && u.Status == UserStatus.Active);
        var query = users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lowerKeyword = keyword.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(lowerKeyword) ||
                                     u.Email.ToLower().Contains(lowerKeyword) ||
                                     (u.Phone != null && u.Phone.Contains(lowerKeyword)));
        }

        var topUsers = query.OrderBy(u => u.FullName).Take(20).ToList();
        var result = new List<BoothOwnerSearchResponse>(topUsers.Count);
        foreach (var user in topUsers)
        {
            var booth = await _booths.GetByOwnerIdAsync(user.Id, cancellationToken);
            result.Add(new BoothOwnerSearchResponse
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                HasBooth = booth is not null
            });
        }

        return ApiResponse<List<BoothOwnerSearchResponse>>.SuccessResponse(result);
    }
}

using AutoMapper;
using ApplicationLayer.DTOs;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;
using ApplicationLayer.Services.Notifications;
using System.Text.Json;

namespace ApplicationLayer.Services.BoothRegistrations;

public class BoothRegistrationService : IBoothRegistrationService
{
    private readonly IBoothRegistrationRepository _registrations;
    private readonly IGenericRepository<BoothDocument> _documents;
    private readonly IBoothRepository _booths;
    private readonly INightMarketRepository _markets;
    private readonly IZoneRepository _zones;
    private readonly ILayoutNodeRepository _nodes;
    private readonly IMarketLayoutRepository _layouts;
    private readonly IBoothLocationRepository _locations;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IGenericRepository<User> _users;
    private readonly IGenericRepository<Role> _roles;
    private readonly IMapper _mapper;
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public BoothRegistrationService(IBoothRegistrationRepository registrations, IGenericRepository<BoothDocument> documents, IBoothRepository booths, INightMarketRepository markets, IZoneRepository zones, ILayoutNodeRepository nodes, IMarketLayoutRepository layouts, IBoothLocationRepository locations, ISubscriptionRepository subscriptions, IGenericRepository<User> users, IGenericRepository<Role> roles, IMapper mapper, INotificationService notifications, IUnitOfWork unitOfWork)
    {
        _registrations = registrations;
        _documents = documents;
        _booths = booths;
        _markets = markets;
        _zones = zones;
        _nodes = nodes;
        _layouts = layouts;
        _locations = locations;
        _subscriptions = subscriptions;
        _users = users;
        _roles = roles;
        _mapper = mapper;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<BoothRegistrationResponse>> CreateAsync(Guid ownerId, CreateBoothRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        if (!await IsBoothOwnerAsync(ownerId))
            throw AppException.Forbidden("Only accounts registered as BoothOwner can submit a booth registration.");

        if (await _booths.ExistsByOwnerIdAsync(ownerId, cancellationToken))
            throw AppException.Conflict("This BoothOwner account already has a booth.");

        if (request.Documents.Count == 0)
            throw AppException.BadRequest("At least one booth document is required.");

        var validationError = await ValidateRequestAsync(request);
        if (validationError is not null) 
            throw AppException.BadRequest(validationError);

        if (await _registrations.HasPendingAsync(ownerId, cancellationToken))
            throw AppException.Conflict("You already have a booth registration pending review.");

        var now = DateTime.UtcNow;
        var registration = _mapper.Map<BoothRegistration>(request);
        registration.Id = Guid.NewGuid();
        registration.OwnerId = ownerId;
        registration.Status = BoothRegistrationStatus.PendingReview;
        registration.CreatedAt = now;
        registration.UpdatedAt = now;
        await _registrations.AddAsync(registration);

        var documents = CreateDocuments(registration.Id, request.Documents, now);
        await _documents.AddRangeAsync(documents);

        await _registrations.SaveChangesAsync();
        var market = await _markets.GetActiveByIdAsync(registration.RequestedNightMarketId, cancellationToken);
        if (market?.MarketOwnerId is Guid marketOwnerId)
        {
            await _notifications.NotifyAsync(new NotificationMessage(
                marketOwnerId,
                NotificationType.RegistrationSubmitted,
                "New booth registration",
                $"{registration.BoothName} submitted a booth registration for {market.Name}.",
                registration.RequestedNightMarketId,
                "BoothRegistration",
                registration.Id,
                JsonSerializer.Serialize(new
                {
                    registrationId = registration.Id,
                    ownerId
                })), cancellationToken);
        }
        return ApiResponse<BoothRegistrationResponse>.SuccessResponse(ToResponse(registration, documents), "Booth registration submitted successfully.");
    }

    public async Task<ApiResponse<PaginationResp<BoothRegistrationResponse>>> GetMineAsync(
        Guid ownerId,
        PaginationReq pagination,
        CancellationToken cancellationToken = default)
    {
        var page = await _registrations.GetByOwnerPagedAsync(
            ownerId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<BoothRegistrationResponse>>.SuccessResponse(
            PaginationResp<BoothRegistrationResponse>.Create(
                await ToResponsesAsync(page.Items),
                page.TotalCount,
                pagination));
    }

    public async Task<ApiResponse<PaginationResp<BoothRegistrationResponse>>> GetPendingAsync(PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var page = await _registrations.GetPendingPagedAsync(
            pagination.Page, pagination.PageSize, cancellationToken);

        return ApiResponse<PaginationResp<BoothRegistrationResponse>>.SuccessResponse(
            PaginationResp<BoothRegistrationResponse>.Create(
                await ToResponsesAsync(page.Items),
                page.TotalCount,
                pagination));
    }

    public async Task<ApiResponse<PaginationResp<BoothRegistrationResponse>>> GetByMarketOwnerAsync(
        Guid marketOwnerId, Guid? marketId, string? status, string? keyword, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        BoothRegistrationStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BoothRegistrationStatus>(status, true, out var parsed))
            statusFilter = parsed;

        var page = await _registrations.GetByMarketOwnerPagedAsync(
            marketOwnerId, marketId, statusFilter, keyword, pagination.Page, pagination.PageSize, cancellationToken);

        return ApiResponse<PaginationResp<BoothRegistrationResponse>>.SuccessResponse(
            PaginationResp<BoothRegistrationResponse>.Create(
                await ToResponsesAsync(page.Items),
                page.TotalCount,
                pagination));
    }

    public async Task<ApiResponse<RegistrationCountsResponse>> GetCountsByMarketOwnerAsync(
        Guid marketOwnerId, Guid? marketId, CancellationToken cancellationToken = default)
    {
        var counts = await _registrations.GetCountsByMarketOwnerAsync(marketOwnerId, marketId, cancellationToken);
        var result = new RegistrationCountsResponse
        {
            PendingReview = counts.GetValueOrDefault(BoothRegistrationStatus.PendingReview),
            Approved = counts.GetValueOrDefault(BoothRegistrationStatus.Approved),
            Rejected = counts.GetValueOrDefault(BoothRegistrationStatus.Rejected),
            Total = counts.Values.Sum()
        };
        return ApiResponse<RegistrationCountsResponse>.SuccessResponse(result);
    }

    public async Task<ApiResponse<BoothRegistrationResponse>> ReviewAsync(Guid registrationId, ReviewBoothRegistrationRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var registration = await _registrations.GetByIdAsync(registrationId);
        if (registration is null) 
            throw AppException.NotFound("Booth registration was not found.");

        var market = await _markets.GetActiveByIdAsync(registration.RequestedNightMarketId, cancellationToken);
        if (market?.MarketOwnerId is not Guid marketOwnerId)
            throw AppException.Conflict("Night market does not have a valid owner.", "MARKET_OWNER_REQUIRED");

        if (actorId is Guid actor && market.MarketOwnerId != actor)
            throw AppException.Forbidden("You can only review registrations for your own markets.", "MARKET_OWNERSHIP_REQUIRED");

        if (request.Approved)
        {
            var subscription = await _subscriptions.GetActiveMarketSubscriptionAsync(marketOwnerId);
            if (subscription?.Package is null)
                throw AppException.Forbidden(
                    "An active Market package is required.",
                    "ACTIVE_MARKET_SUBSCRIPTION_REQUIRED");
        }

        if (registration.Status != BoothRegistrationStatus.PendingReview) 
            throw AppException.Conflict("This registration has already been processed.");

        if (!request.Approved && string.IsNullOrWhiteSpace(request.RejectReason)) 
            throw AppException.BadRequest("A rejection reason is required.");

        if (request.Approved && !await IsBoothOwnerAsync(registration.OwnerId))
            throw AppException.Conflict("The registration owner is not a BoothOwner account and cannot be approved.");

        if (request.Approved && await _booths.ExistsByOwnerIdAsync(registration.OwnerId, cancellationToken))
            throw AppException.Conflict("This BoothOwner account already has a booth.");

        var now = DateTime.UtcNow;
        var newStatus = request.Approved ? BoothRegistrationStatus.Approved : BoothRegistrationStatus.Rejected;
        var rejectReason = request.Approved ? null : request.RejectReason!.Trim();

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        List<BoothDocument> docs;
        try
        {
            var rowsAffected = await _registrations.UpdateStatusWithConcurrencyAsync(
                registrationId, BoothRegistrationStatus.PendingReview, newStatus, rejectReason, now, cancellationToken);

            if (rowsAffected == 0)
                throw AppException.Conflict("This registration has already been processed by another reviewer.", "REGISTRATION_ALREADY_PROCESSED");

            registration.Status = newStatus;
            registration.RejectReason = rejectReason;
            registration.UpdatedAt = now;

            docs = (await _documents.FindAsync(d => d.RegistrationId == registration.Id)).ToList();
            foreach (var doc in docs) { doc.VerificationStatus = request.Approved ? BoothDocumentStatus.Verified : BoothDocumentStatus.Rejected; doc.UpdatedAt = now; }
            _documents.UpdateRange(docs);
            if (request.Approved)
            {
                var booth = new Booth { Id = Guid.NewGuid(), RegistrationId = registration.Id, NightMarketId = registration.RequestedNightMarketId,
                    BoothOwnerId = registration.OwnerId, ZoneId = null, BoothName = registration.BoothName,
                    Description = registration.Description, PhoneNumber = registration.Phone, SlotNumber = null,
                    MapPositionX = null, MapPositionY = null, Status = BoothStatus.Active, CreatedAt = now, UpdatedAt = now };

                await _booths.AddAsync(booth);
                foreach (var doc in docs) doc.BoothId = booth.Id;
                var freePackage = await _subscriptions.GetPackageByCodeAsync("BOOTH_FREE", cancellationToken)
                    ?? throw AppException.ServiceUnavailable(
                        "The default booth plan is not available. Please contact the system administrator.",
                        "DEFAULT_BOOTH_PACKAGE_UNAVAILABLE");

                if (freePackage.Type != PackageType.Booth || freePackage.Price != 0 || freePackage.Status != PackageStatus.Active)
                    throw AppException.ServiceUnavailable(
                        "The default booth plan is not configured correctly. Please contact the system administrator.",
                        "DEFAULT_BOOTH_PACKAGE_INVALID");

                await _subscriptions.AddBoothSubscriptionAsync(new BoothSubscription
                {
                    Id = Guid.NewGuid(),
                    BoothId = booth.Id,
                    PackageId = freePackage.Id,
                    StartDate = now,
                    EndDate = DateTime.MaxValue,
                    Status = SubscriptionStatus.Active,
                    PaidAmount = 0,
                    ChangeType = "FreeDefault",
                    CreatedAt = now,
                    UpdatedAt = now
                }, cancellationToken);
                registration.Booth = booth;
            }
            _registrations.Update(registration);

            await _registrations.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (AppException)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
        await _notifications.NotifyAsync(new NotificationMessage(
            registration.OwnerId,
            request.Approved
                ? NotificationType.RegistrationApproved
                : NotificationType.RegistrationRejected,
            request.Approved
                ? "Booth registration approved"
                : "Booth registration rejected",
            request.Approved
                ? $"{registration.BoothName} has been approved."
                : registration.RejectReason!,
            registration.Booth?.Id,
            "BoothRegistration",
            registration.Id,
            JsonSerializer.Serialize(new
            {
                registrationId = registration.Id,
                boothId = registration.Booth?.Id,
                approved = request.Approved
            })), cancellationToken);
        return ApiResponse<BoothRegistrationResponse>.SuccessResponse(ToResponse(registration, docs), request.Approved ? "Registration approved and booth created." : "Booth registration rejected.");
    }

    private async Task<bool> IsZoneInMarketAsync(Guid zoneId, Guid marketId) => (await _zones.GetActiveByIdAsync(zoneId))?.NightMarketId == marketId;

    private async Task<bool> IsBoothOwnerAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            return false;

        var role = await _roles.GetByIdAsync(user.RoleId);
        return role?.RoleName == "BoothOwner";
    }

    private async Task<string?> ValidateRequestAsync(CreateBoothRegistrationRequest request)
    {
        if (await _markets.GetActiveByIdAsync(request.RequestedNightMarketId) is null)
            return "Night market was not found.";

        if (request.PreferredZoneId.HasValue && !(await IsZoneInMarketAsync(request.PreferredZoneId.Value, request.RequestedNightMarketId)))
            return "The selected zone does not belong to the night market.";

        return null;
    }

    private List<BoothDocument> CreateDocuments(Guid registrationId, IEnumerable<BoothDocumentRequest> requests, DateTime now)
        => requests.Select(request =>
        {
            var document = _mapper.Map<BoothDocument>(request);
            document.Id = Guid.NewGuid();
            document.RegistrationId = registrationId;
            document.VerificationStatus = BoothDocumentStatus.PendingReview;
            document.CreatedAt = now;
            document.UpdatedAt = now;
            return document;
        }).ToList();

    private async Task<List<BoothRegistrationResponse>> ToResponsesAsync(IEnumerable<BoothRegistration> registrations)
    {
        var result = new List<BoothRegistrationResponse>();
        foreach (var registration in registrations) result.Add(ToResponse(registration, await _documents.FindAsync(d => d.RegistrationId == registration.Id)));
        return result;
    }

    private BoothRegistrationResponse ToResponse(BoothRegistration registration, IEnumerable<BoothDocument> documents)
    {
        var response = _mapper.Map<BoothRegistrationResponse>(registration);
        response.Documents = _mapper.Map<List<BoothDocumentResponse>>(documents);
        return response;
    }
}

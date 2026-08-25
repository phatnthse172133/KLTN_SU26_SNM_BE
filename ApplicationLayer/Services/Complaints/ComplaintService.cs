using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Storage;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using System.Text.Json;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Complaints;

public class ComplaintService : IComplaintService
{
    private static readonly HashSet<ComplaintStatus> ActiveStatuses =
    [
        ComplaintStatus.Pending,
        ComplaintStatus.UnderReview,
        ComplaintStatus.WaitingForCustomer,
        ComplaintStatus.InProgress
    ];

    private static readonly HashSet<ComplaintStatus> TerminalStatuses =
    [
        ComplaintStatus.Resolved,
        ComplaintStatus.Rejected,
        ComplaintStatus.Closed,
        ComplaintStatus.Withdrawn
    ];

    private readonly IComplaintRepository _complaints;
    private readonly IBoothRepository _booths;
    private readonly IOrderRepository _orders;
    private readonly INightMarketRepository _nightMarkets;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IModerationRepository _moderation;
    private readonly IMapper _mapper;
    private readonly INotificationService _notifications;
    private readonly IFileStorageService _fileStorage;

    public ComplaintService(
        IComplaintRepository complaints,
        IBoothRepository booths,
        IOrderRepository orders,
        INightMarketRepository nightMarkets,
        ISubscriptionRepository subscriptions,
        IModerationRepository moderation,
        IMapper mapper,
        INotificationService notifications,
        IFileStorageService fileStorage)
    {
        _complaints = complaints;
        _booths = booths;
        _orders = orders;
        _nightMarkets = nightMarkets;
        _subscriptions = subscriptions;
        _moderation = moderation;
        _mapper = mapper;
        _notifications = notifications;
        _fileStorage = fileStorage;
    }

    public async Task<ApiResponse<ComplaintResponse>> CreateAsync(Guid customerId, CreateComplaintRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Category))
            throw AppException.BadRequest("Complaint category is invalid.", "COMPLAINT_CATEGORY_INVALID");
        if (request.Category == ComplaintCategory.PromotionIssue)
            throw AppException.BadRequest("Complaint category is invalid.", "COMPLAINT_CATEGORY_INVALID");

        var title = TextHelper.NormalizeOptionalText(request.Title) ?? GetCategoryTitle(request.Category);
        var description = TextHelper.NormalizeOptionalText(request.Description);
        if (title.Length < 3)
            throw AppException.BadRequest("Complaint title must contain at least 3 characters.", "COMPLAINT_TITLE_INVALID");
        if (title.Length > 200)
            throw AppException.BadRequest("Complaint title cannot exceed 200 characters.", "COMPLAINT_TITLE_INVALID");
        if (description is null || description.Length < 10)
            throw AppException.BadRequest("Complaint description must contain at least 10 characters.", "COMPLAINT_DESCRIPTION_INVALID");
        if (description.Length > 2000)
            throw AppException.BadRequest("Complaint description cannot exceed 2000 characters.", "COMPLAINT_DESCRIPTION_INVALID");
        var requestedImages = request.Images ?? [];
        if (requestedImages.Count > 5)
            throw AppException.BadRequest("A complaint can contain at most 5 images.", "COMPLAINT_IMAGE_LIMIT");

        var boothId = await GetAuthoritativeBoothIdAsync(customerId, request.OrderId, cancellationToken);
        if (request.BoothId != Guid.Empty && request.BoothId != boothId)
            throw AppException.BadRequest("The booth does not match the order.", "ORDER_BOOTH_MISMATCH");

        if (await _complaints.HasActiveComplaintAsync(customerId, boothId, request.OrderId))
            throw AppException.Conflict("There is already an active complaint for this order and booth.");

        var now = DateTime.UtcNow;
        var complaint = _mapper.Map<Complaint>(request);
        complaint.Id = Guid.NewGuid();
        complaint.CustomerId = customerId;
        complaint.BoothId = boothId;
        complaint.Category = request.Category;
        complaint.Title = title;
        complaint.Description = description;
        complaint.Status = ComplaintStatus.Pending;
        complaint.CreatedAt = now;
        complaint.UpdatedAt = now;

        await _complaints.AddAsync(complaint);
        await AddHistoryAsync(complaint.Id, null, ComplaintStatus.Pending, "Complaint submitted", customerId, "Customer", now, cancellationToken);

        var images = requestedImages
            .Where(i => !string.IsNullOrWhiteSpace(i.ImageUrl))
            .Select(i => new ComplaintImage
            {
                Id = Guid.NewGuid(),
                ComplaintId = complaint.Id,
                ImageUrl = i.ImageUrl.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();

        if (images.Count > 0)
            await _complaints.AddImagesAsync(images);

        if (!await _complaints.TrySaveNewComplaintAsync(cancellationToken))
            throw AppException.Conflict("There is already an active complaint for this order and booth.", "ACTIVE_COMPLAINT_EXISTS");

        var booth = await _booths.GetByIdAsync(complaint.BoothId);
        if (booth is not null)
        {
            await TryNotifyAsync(new NotificationMessage(
                booth.BoothOwnerId,
                NotificationType.ComplaintSubmitted,
                "New booth complaint",
                complaint.Title,
                booth.Id,
                "Complaint",
                complaint.Id,
                JsonSerializer.Serialize(new
                {
                    complaintId = complaint.Id,
                    boothId = booth.Id,
                    orderId = complaint.OrderId
                })), cancellationToken);
        }
        await TryNotifyRoleAsync(new RoleNotificationMessage(
            "Admin",
            NotificationType.ComplaintSubmitted,
            "New complaint submitted",
            complaint.Title,
            complaint.BoothId,
            "Complaint",
            complaint.Id,
            JsonSerializer.Serialize(new
            {
                complaintId = complaint.Id,
                boothId = complaint.BoothId,
                orderId = complaint.OrderId
            })), cancellationToken);

        if (booth is not null)
        {
            var market = await _nightMarkets.GetActiveByIdAsync(booth.NightMarketId, cancellationToken);
            if (market?.MarketOwnerId is Guid marketOwnerId)
            {
                await TryNotifyAsync(new NotificationMessage(
                    marketOwnerId,
                    NotificationType.ComplaintSubmitted,
                    "New complaint in your market",
                    complaint.Title,
                    booth.NightMarketId,
                    "Complaint",
                    complaint.Id,
                    JsonSerializer.Serialize(new
                    {
                        complaintId = complaint.Id,
                        boothId = booth.Id,
                        orderId = complaint.OrderId
                    })), cancellationToken);
            }
        }

        var created = await _complaints.GetWithImagesByIdAsync(complaint.Id) ?? complaint;
        return ApiResponse<ComplaintResponse>.SuccessResponse(ToResponse(created), "Complaint submitted successfully.");
    }

    public async Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetMineAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var page = await _complaints.GetPagedByCustomerWithImagesAsync(
            customerId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<ComplaintResponse>>.SuccessResponse(
            PaginationResp<ComplaintResponse>.Create(page.Items.Select(ToResponse).ToList(), page.TotalCount, pagination));
    }

    public async Task<ApiResponse<ComplaintResponse>> GetMineDetailAsync(Guid customerId, Guid complaintId, CancellationToken cancellationToken = default)
    {
        var complaint = await _complaints.GetCustomerWithImagesByIdAsync(customerId, complaintId, cancellationToken);
        if (complaint is null)
            throw AppException.NotFound("Complaint was not found.", "COMPLAINT_NOT_FOUND");

        return ApiResponse<ComplaintResponse>.SuccessResponse(ToResponse(complaint));
    }

    public async Task<ApiResponse<ComplaintResponse>> WithdrawAsync(Guid customerId, Guid complaintId, CancellationToken cancellationToken = default)
    {
        var complaint = await _complaints.GetCustomerWithImagesByIdAsync(customerId, complaintId, cancellationToken);
        if (complaint is null)
            throw AppException.NotFound("Complaint was not found.", "COMPLAINT_NOT_FOUND");

        if (!CanWithdraw(complaint.Status))
            throw AppException.BadRequest("This complaint can no longer be withdrawn.", "COMPLAINT_CANNOT_WITHDRAW");

        var previous = complaint.Status;
        var now = DateTime.UtcNow;
        await _complaints.BeginTransactionAsync();
        try
        {
            var rows = await _complaints.UpdateStatusWithConcurrencyAsync(
                complaintId, previous, ComplaintStatus.Withdrawn,
                complaint.AdminResponse, complaint.ResolutionAction, complaint.PolicyViolation, now, complaint.CustomerEvidenceRequestNote);
            if (rows == 0)
                throw AppException.Conflict("Complaint has already been processed.", "COMPLAINT_ALREADY_PROCESSED");

            await AddHistoryAsync(complaintId, previous, ComplaintStatus.Withdrawn, "Withdrawn by customer", customerId, "Customer", now, cancellationToken);
            await _complaints.SaveChangesAsync();
            await _complaints.CommitTransactionAsync();
        }
        catch (AppException)
        {
            await _complaints.RollbackTransactionAsync();
            throw;
        }
        catch
        {
            await _complaints.RollbackTransactionAsync();
            throw;
        }

        var updated = await _complaints.GetWithImagesByIdAsync(complaintId);
        await TryNotifyRoleAsync(new RoleNotificationMessage(
            "Admin",
            NotificationType.Complaint,
            "Complaint withdrawn",
            $"Complaint \"{updated?.Title}\" was withdrawn by the customer.",
            updated?.BoothId,
            "Complaint",
            complaintId,
            JsonSerializer.Serialize(new { complaintId, status = ComplaintStatus.Withdrawn.ToString() })), cancellationToken);

        return ApiResponse<ComplaintResponse>.SuccessResponse(ToResponse(updated!), "Complaint withdrawn successfully.");
    }

    public async Task<ApiResponse<ComplaintResponse>> AddEvidenceAsync(Guid customerId, Guid complaintId, AddComplaintEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        var complaint = await _complaints.GetCustomerWithImagesByIdAsync(customerId, complaintId, cancellationToken);
        if (complaint is null)
            throw AppException.NotFound("Complaint was not found.", "COMPLAINT_NOT_FOUND");

        if (!CanAddEvidence(complaint.Status))
            throw AppException.BadRequest("Evidence can only be added while waiting for customer response.", "COMPLAINT_EVIDENCE_NOT_ALLOWED");

        var requestedImages = request.Images ?? [];
        var existingCount = complaint.ComplaintImages?.Count ?? 0;
        if (existingCount + requestedImages.Count > 5)
            throw AppException.BadRequest("A complaint can contain at most 5 images.", "COMPLAINT_IMAGE_LIMIT");

        var now = DateTime.UtcNow;
        var previous = complaint.Status;
        await _complaints.BeginTransactionAsync();
        try
        {
            var rows = await _complaints.UpdateStatusWithConcurrencyAsync(
                complaintId, previous, ComplaintStatus.UnderReview,
                complaint.AdminResponse, complaint.ResolutionAction, complaint.PolicyViolation, now, complaint.CustomerEvidenceRequestNote);
            if (rows == 0)
                throw AppException.Conflict("Complaint has already been processed.", "COMPLAINT_ALREADY_PROCESSED");

            var images = requestedImages
                .Where(i => !string.IsNullOrWhiteSpace(i.ImageUrl))
                .Select(i => new ComplaintImage
                {
                    Id = Guid.NewGuid(),
                    ComplaintId = complaintId,
                    ImageUrl = i.ImageUrl.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .ToList();

            if (images.Count > 0)
                await _complaints.AddImagesAsync(images);

            await AddHistoryAsync(complaintId, previous, ComplaintStatus.UnderReview, "Customer submitted additional evidence", customerId, "Customer", now, cancellationToken);
            await _complaints.SaveChangesAsync();
            await _complaints.CommitTransactionAsync();
        }
        catch (AppException)
        {
            await _complaints.RollbackTransactionAsync();
            throw;
        }
        catch
        {
            await _complaints.RollbackTransactionAsync();
            throw;
        }

        var updated = await _complaints.GetWithImagesByIdAsync(complaintId);
        await TryNotifyRoleAsync(new RoleNotificationMessage(
            "Admin",
            NotificationType.ComplaintInReview,
            "Complaint evidence received",
            $"Customer submitted evidence for complaint \"{updated?.Title}\".",
            updated?.BoothId,
            "Complaint",
            complaintId,
            JsonSerializer.Serialize(new { complaintId, status = ComplaintStatus.UnderReview.ToString() })), cancellationToken);

        return ApiResponse<ComplaintResponse>.SuccessResponse(ToResponse(updated!), "Evidence submitted successfully.");
    }

    public async Task<ApiResponse<ComplaintResponse>> AddBoothResponseAsync(
        Guid boothOwnerId,
        Guid complaintId,
        BoothComplaintResponseRequest request,
        CancellationToken cancellationToken = default)
    {
        var complaint = await _complaints.GetWithImagesByIdAsync(complaintId)
            ?? throw AppException.NotFound("Complaint was not found.", "COMPLAINT_NOT_FOUND");

        var booth = await _booths.GetByIdAsync(complaint.BoothId)
            ?? throw AppException.NotFound("Booth was not found.");
        if (booth.BoothOwnerId != boothOwnerId)
            throw AppException.Forbidden("You can only respond to complaints for your own booth.", "BOOTH_OWNERSHIP_REQUIRED");

        if (!ActiveStatuses.Contains(complaint.Status))
            throw AppException.BadRequest("This complaint can no longer accept booth responses.", "COMPLAINT_BOOTH_RESPONSE_NOT_ALLOWED");

        var explanation = TextHelper.NormalizeOptionalText(request.Explanation);
        if (explanation is null || explanation.Length < 10)
            throw AppException.BadRequest("Booth explanation must contain at least 10 characters.", "COMPLAINT_BOOTH_RESPONSE_INVALID");
        if (explanation.Length > 2000)
            throw AppException.BadRequest("Booth explanation cannot exceed 2000 characters.", "COMPLAINT_BOOTH_RESPONSE_INVALID");

        var requestedImages = request.Images ?? [];
        var existingCount = complaint.ComplaintImages?.Count ?? 0;
        if (existingCount + requestedImages.Count > 5)
            throw AppException.BadRequest("A complaint can contain at most 5 images.", "COMPLAINT_IMAGE_LIMIT");

        var now = DateTime.UtcNow;
        var previous = complaint.Status;
        var nextStatus = previous is ComplaintStatus.Pending or ComplaintStatus.WaitingForCustomer
            ? ComplaintStatus.UnderReview
            : previous;

        await _complaints.BeginTransactionAsync();
        try
        {
            if (nextStatus != previous)
            {
                var rows = await _complaints.UpdateStatusWithConcurrencyAsync(
                    complaintId, previous, nextStatus,
                    complaint.AdminResponse, complaint.ResolutionAction, complaint.PolicyViolation, now, complaint.CustomerEvidenceRequestNote);
                if (rows == 0)
                    throw AppException.Conflict("Complaint has already been processed.", "COMPLAINT_ALREADY_PROCESSED");
            }

            complaint.BoothOwnerResponse = explanation;
            complaint.UpdatedAt = now;
            _complaints.Update(complaint);

            var images = requestedImages
                .Where(i => !string.IsNullOrWhiteSpace(i.ImageUrl))
                .Select(i => new ComplaintImage
                {
                    Id = Guid.NewGuid(),
                    ComplaintId = complaintId,
                    ImageUrl = i.ImageUrl.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .ToList();

            if (images.Count > 0)
                await _complaints.AddImagesAsync(images);

            await AddHistoryAsync(
                complaintId,
                previous,
                nextStatus,
                $"Booth owner response: {explanation}",
                boothOwnerId,
                "BoothOwner",
                now,
                cancellationToken);
            await _complaints.SaveChangesAsync();
            await _complaints.CommitTransactionAsync();
        }
        catch (AppException)
        {
            await _complaints.RollbackTransactionAsync();
            throw;
        }
        catch
        {
            await _complaints.RollbackTransactionAsync();
            throw;
        }

        var updated = await _complaints.GetWithImagesByIdAsync(complaintId);
        await TryNotifyRoleAsync(new RoleNotificationMessage(
            "Admin",
            NotificationType.ComplaintInReview,
            "Booth owner responded to complaint",
            $"Booth responded to complaint \"{updated?.Title}\".",
            updated?.BoothId,
            "Complaint",
            complaintId,
            JsonSerializer.Serialize(new { complaintId, status = nextStatus.ToString() })), cancellationToken);

        return ApiResponse<ComplaintResponse>.SuccessResponse(ToResponse(updated!), "Booth response submitted successfully.");
    }

    public async Task<ApiResponse<ComplaintImageUploadResponse>> UploadImageAsync(Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        var imageUrl = await _fileStorage.SaveImageAsync("complaints", stream, fileName, contentType, length, cancellationToken);
        return ApiResponse<ComplaintImageUploadResponse>.SuccessResponse(new ComplaintImageUploadResponse { ImageUrl = imageUrl }, "Complaint image uploaded successfully.");
    }

    public async Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetAllAsync(PaginationReq pagination, ComplaintStatus? status = null, CancellationToken cancellationToken = default)
    {
        var page = await _complaints.GetPagedWithImagesAsync(
            pagination.Page, pagination.PageSize, status, cancellationToken);
        return ApiResponse<PaginationResp<ComplaintResponse>>.SuccessResponse(
            PaginationResp<ComplaintResponse>.Create(page.Items.Select(ToResponse).ToList(), page.TotalCount, pagination));
    }

    public async Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetAllFilteredAsync(AdminComplaintQueryRequest query, CancellationToken cancellationToken = default)
    {
        var page = await _complaints.GetPagedWithImagesFilteredAsync(
            query.Page, query.PageSize, query.Status, query.Keyword, query.BoothId, cancellationToken);
        return ApiResponse<PaginationResp<ComplaintResponse>>.SuccessResponse(
            PaginationResp<ComplaintResponse>.Create(
                page.Items.Select(ToResponse).ToList(),
                page.TotalCount,
                new PaginationReq { Page = query.Page, PageSize = query.PageSize }));
    }

    public async Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetByBoothAsync(Guid ownerId, Guid boothId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var booth = await _booths.GetByIdAsync(boothId);
        if (booth is null)
            throw AppException.NotFound("Booth was not found.");

        if (booth.BoothOwnerId != ownerId)
            throw AppException.Forbidden("You do not have permission to view this booth's complaints.");

        var page = await _complaints.GetPagedByBoothWithImagesAsync(
            boothId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<ComplaintResponse>>.SuccessResponse(
            PaginationResp<ComplaintResponse>.Create(page.Items.Select(ToResponse).ToList(), page.TotalCount, pagination));
    }

    public async Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetByMarketOwnerAsync(
        Guid marketOwnerId, MarketOwnerComplaintQueryRequest query, CancellationToken cancellationToken = default)
    {
        if (query.FromDate.HasValue && query.ToDate.HasValue
            && query.FromDate.Value.Date > query.ToDate.Value.Date)
            throw AppException.BadRequest("FromDate cannot be later than ToDate.", "INVALID_DATE_RANGE");

        var page = await _complaints.GetPagedByMarketOwnerWithImagesAsync(
            marketOwnerId, query.Status, query.MarketId, query.FromDate, query.ToDate,
            query.Page, query.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<ComplaintResponse>>.SuccessResponse(
            PaginationResp<ComplaintResponse>.Create(page.Items.Select(ToResponse).ToList(), page.TotalCount, query));
    }

    public async Task<ApiResponse<ComplaintResponse>> GetDetailForMarketOwnerAsync(
        Guid marketOwnerId, Guid complaintId, CancellationToken cancellationToken = default)
    {
        var complaint = await _complaints.GetWithImagesByIdAsync(complaintId);
        if (complaint is null)
            throw AppException.NotFound("Complaint was not found.");

        var booth = await _booths.GetByIdAsync(complaint.BoothId);
        if (booth is null)
            throw AppException.NotFound("Booth was not found.");

        var market = await _nightMarkets.GetActiveByIdAsync(booth.NightMarketId, cancellationToken);
        if (market is null || market.MarketOwnerId != marketOwnerId)
            throw AppException.Forbidden("You can only view complaints for booths in your own markets.", "MARKET_OWNERSHIP_REQUIRED");

        return ApiResponse<ComplaintResponse>.SuccessResponse(ToResponse(complaint));
    }

    public async Task<ApiResponse<ComplaintCountsResponse>> GetCountsByMarketOwnerAsync(
        Guid marketOwnerId, CancellationToken cancellationToken = default)
    {
        var counts = await _complaints.CountByStatusByMarketOwnerAsync(marketOwnerId, cancellationToken);
        var pending = counts.GetValueOrDefault(ComplaintStatus.Pending);
        var resolved = counts.GetValueOrDefault(ComplaintStatus.Resolved);
        var rejected = counts.GetValueOrDefault(ComplaintStatus.Rejected);
        return ApiResponse<ComplaintCountsResponse>.SuccessResponse(new ComplaintCountsResponse
        {
            Pending = pending,
            Resolved = resolved,
            Rejected = rejected,
            Total = counts.Values.Sum()
        });
    }

    public async Task<ApiResponse<ComplaintResponse>> UpdateStatusAsync(
        Guid complaintId,
        UpdateComplaintStatusRequest request,
        CancellationToken cancellationToken = default,
        Guid? actorId = null,
        string? actorRole = null)
    {
        var complaint = await _complaints.GetByIdAsync(complaintId);
        if (complaint is null)
            throw AppException.NotFound("Complaint was not found.");

        if (actorId is Guid actor)
        {
            var booth = await _booths.GetByIdAsync(complaint.BoothId);
            if (booth is null)
                throw AppException.NotFound("Booth was not found.");
            var market = await _nightMarkets.GetActiveByIdAsync(booth.NightMarketId, cancellationToken);
            if (market is null || market.MarketOwnerId != actor)
                throw AppException.Forbidden("You can only handle complaints for booths in your own markets.", "MARKET_OWNERSHIP_REQUIRED");

            var subscription = await _subscriptions.GetActiveMarketSubscriptionAsync(actor);
            if (subscription?.Package is null)
                throw AppException.Forbidden(
                    "An active Market package is required.",
                    "ACTIVE_MARKET_SUBSCRIPTION_REQUIRED");

            if (request.Status == ComplaintStatus.Resolved && request.ResolutionAction == ComplaintResolutionAction.SuspendBooth)
                throw AppException.Forbidden("Only an administrator can ban a booth.", "ADMIN_SANCTION_REQUIRED");
        }

        if (TerminalStatuses.Contains(complaint.Status) &&
            !(complaint.Status == ComplaintStatus.Resolved && request.Status == ComplaintStatus.Closed))
            throw AppException.Conflict("Complaint has already been processed.", "COMPLAINT_ALREADY_PROCESSED");

        ValidateStatusTransition(complaint.Status, request.Status);
        ValidateResolutionRequest(request);

        var adminResponse = TextHelper.NormalizeOptionalText(request.AdminResponse);
        var evidenceNote = TextHelper.NormalizeOptionalText(request.EvidenceRequestNote) ?? adminResponse;
        var policyViolation = TextHelper.NormalizeOptionalText(request.PolicyViolation);
        var now = DateTime.UtcNow;
        var previousStatus = complaint.Status;

        await _complaints.BeginTransactionAsync();
        try
        {
            var resolutionAction = request.Status == ComplaintStatus.Resolved
                ? request.ResolutionAction
                : null;
            var policyViolationValue = request.Status == ComplaintStatus.Resolved
                ? policyViolation
                : null;
            var evidenceRequestNoteValue = request.Status == ComplaintStatus.WaitingForCustomer
                ? evidenceNote
                : complaint.CustomerEvidenceRequestNote;
            var persistedAdminResponse = request.Status == ComplaintStatus.WaitingForCustomer
                ? (adminResponse ?? complaint.AdminResponse)
                : adminResponse;

            var rowsAffected = await _complaints.UpdateStatusWithConcurrencyAsync(
                complaintId,
                previousStatus,
                request.Status,
                persistedAdminResponse,
                resolutionAction,
                policyViolationValue,
                now,
                evidenceRequestNoteValue);

            if (rowsAffected == 0)
                throw AppException.Conflict("Complaint has already been processed by another administrator.", "COMPLAINT_ALREADY_PROCESSED");

            complaint.Status = request.Status;
            complaint.AdminResponse = persistedAdminResponse;
            complaint.ResolutionAction = resolutionAction;
            complaint.PolicyViolation = policyViolationValue;
            complaint.CustomerEvidenceRequestNote = evidenceRequestNoteValue;
            complaint.UpdatedAt = now;

            var historyNote = request.Status == ComplaintStatus.WaitingForCustomer
                ? evidenceRequestNoteValue
                : adminResponse;
            await AddHistoryAsync(
                complaintId,
                previousStatus,
                request.Status,
                historyNote,
                actorId,
                actorRole ?? (actorId.HasValue ? "MarketOwner" : "Admin"),
                now,
                cancellationToken);
            await _complaints.SaveChangesAsync();

            if (request.Status == ComplaintStatus.Resolved && request.ResolutionAction == ComplaintResolutionAction.SuspendBooth)
            {
                var booth = await _booths.GetByIdAsync(complaint.BoothId);
                if (booth is not null)
                {
                    var previousBoothStatus = booth.Status;
                    booth.Status = BoothStatus.Banned;
                    booth.UpdatedAt = now;
                    _booths.Update(booth);
                    await _booths.SaveChangesAsync();

                    var moderationHistory = new ModerationActionHistory
                    {
                        Id = Guid.NewGuid(),
                        BoothId = booth.Id,
                        AdminId = actorId ?? Guid.Empty,
                        AdminName = "Complaint Workflow",
                        PreviousStatus = previousBoothStatus.ToString(),
                        NewStatus = BoothStatus.Banned.ToString(),
                        Reason = $"Auto-banned via complaint: {complaint.Title}",
                        Source = ModerationActionSource.Complaint,
                        ComplaintId = complaint.Id,
                        CreatedAt = now
                    };
                    await _moderation.AddAsync(moderationHistory);
                    await _moderation.SaveChangesAsync();
                }
            }

            await _complaints.CommitTransactionAsync();
        }
        catch (AppException)
        {
            await _complaints.RollbackTransactionAsync();
            throw;
        }
        catch (Exception)
        {
            await _complaints.RollbackTransactionAsync();
            throw;
        }

        var updated = await _complaints.GetWithImagesByIdAsync(complaintId);

        if (request.Status == ComplaintStatus.UnderReview)
        {
            await TryNotifyAsync(new NotificationMessage(
                complaint.CustomerId,
                NotificationType.ComplaintInReview,
                "Complaint under review",
                $"Your complaint \"{updated?.Title}\" is now under review.",
                complaint.BoothId,
                "Complaint",
                complaint.Id,
                JsonSerializer.Serialize(new
                {
                    complaintId = complaint.Id,
                    boothId = complaint.BoothId,
                    status = request.Status.ToString()
                })), cancellationToken);
        }
        else if (request.Status == ComplaintStatus.WaitingForCustomer)
        {
            await TryNotifyAsync(new NotificationMessage(
                complaint.CustomerId,
                NotificationType.Complaint,
                "Additional evidence requested",
                evidenceNote ?? "Please provide additional evidence for your complaint.",
                complaint.BoothId,
                "Complaint",
                complaint.Id,
                JsonSerializer.Serialize(new
                {
                    complaintId = complaint.Id,
                    boothId = complaint.BoothId,
                    status = request.Status.ToString()
                })), cancellationToken);
        }
        else if (request.Status is ComplaintStatus.Resolved or ComplaintStatus.Rejected or ComplaintStatus.Closed)
        {
            var notificationType = request.Status switch
            {
                ComplaintStatus.Resolved => NotificationType.ComplaintResolved,
                ComplaintStatus.Rejected => NotificationType.ComplaintRejected,
                _ => NotificationType.Complaint
            };

            var actionLabel = request.Status == ComplaintStatus.Resolved
                ? updated?.ResolutionAction?.ToString() ?? "NoViolation"
                : request.Status.ToString();
            var customerContent = $"Your complaint \"{updated?.Title}\" has been {request.Status}.\n\nAdmin response:\n{adminResponse}";
            if (request.Status == ComplaintStatus.Resolved && updated?.ResolutionAction != ComplaintResolutionAction.NoViolation)
                customerContent += $"\n\nAction taken: {updated?.ResolutionAction}";
            if (!string.IsNullOrEmpty(policyViolation))
                customerContent += $"\n\nPolicy violation: {policyViolation}";

            await TryNotifyAsync(new NotificationMessage(
                complaint.CustomerId,
                notificationType,
                $"Complaint {request.Status}",
                customerContent,
                complaint.BoothId,
                "Complaint",
                complaint.Id,
                JsonSerializer.Serialize(new
                {
                    complaintId = complaint.Id,
                    boothId = complaint.BoothId,
                    status = request.Status.ToString(),
                    resolutionAction = actionLabel
                })), cancellationToken);

            var boothEntity = await _booths.GetByIdAsync(complaint.BoothId);
            if (boothEntity is not null && request.Status is ComplaintStatus.Resolved or ComplaintStatus.Rejected)
            {
                try
                {
                    if (request.Status == ComplaintStatus.Rejected)
                    {
                        await _notifications.NotifyAsync(new NotificationMessage(
                            boothEntity.BoothOwnerId,
                            NotificationType.ComplaintRejected,
                            "Complaint rejected",
                            $"A complaint against your booth \"{boothEntity.BoothName}\" was rejected. No action was required.",
                            boothEntity.Id,
                            "Complaint",
                            complaint.Id,
                            JsonSerializer.Serialize(new { complaintId = complaint.Id, boothId = boothEntity.Id })), cancellationToken);
                    }
                    else
                    {
                        var (boothTitle, boothContent) = request.ResolutionAction switch
                        {
                            ComplaintResolutionAction.NoViolation => (
                                "Complaint resolved",
                                $"A complaint against your booth \"{boothEntity.BoothName}\" was resolved without penalty."),
                            ComplaintResolutionAction.Warning => (
                                "Booth warning issued",
                                $"Your booth \"{boothEntity.BoothName}\" received a warning.\n\nReason: {policyViolation}"),
                            ComplaintResolutionAction.SuspendBooth => (
                                "Booth banned",
                                $"Your booth \"{boothEntity.BoothName}\" has been banned following a complaint.\n\nViolation: {policyViolation}"),
                            _ => ("Complaint resolved", "Your complaint has been resolved.")
                        };

                        var boothNotifType = request.ResolutionAction == ComplaintResolutionAction.SuspendBooth
                            ? NotificationType.BoothSuspended
                            : NotificationType.ComplaintResolved;

                        await _notifications.NotifyAsync(new NotificationMessage(
                            boothEntity.BoothOwnerId,
                            boothNotifType,
                            boothTitle,
                            boothContent,
                            boothEntity.Id,
                            "Booth",
                            boothEntity.Id,
                            JsonSerializer.Serialize(new { complaintId = complaint.Id, boothId = boothEntity.Id })), cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to notify booth owner: {ex.Message}");
                }
            }
        }

        return ApiResponse<ComplaintResponse>.SuccessResponse(ToResponse(updated!), "Complaint status updated successfully.");
    }

    public async Task<ApiResponse<ComplaintCountsResponse>> GetCountsAsync(CancellationToken cancellationToken = default)
    {
        var counts = await _complaints.CountByStatusAsync(cancellationToken);

        var response = new ComplaintCountsResponse
        {
            Pending = counts.GetValueOrDefault(ComplaintStatus.Pending),
            Resolved = counts.GetValueOrDefault(ComplaintStatus.Resolved),
            Rejected = counts.GetValueOrDefault(ComplaintStatus.Rejected),
            Total = counts.Values.Sum()
        };

        return ApiResponse<ComplaintCountsResponse>.SuccessResponse(response);
    }

    private async Task AddHistoryAsync(
        Guid complaintId,
        ComplaintStatus? fromStatus,
        ComplaintStatus toStatus,
        string? note,
        Guid? actorUserId,
        string? actorRole,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        await _complaints.AddStatusHistoryAsync(new ComplaintStatusHistory
        {
            Id = Guid.NewGuid(),
            ComplaintId = complaintId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Note = note,
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            CreatedAt = createdAt
        }, cancellationToken);
    }

    private async Task<Guid> GetAuthoritativeBoothIdAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByCustomerAsync(customerId, orderId);
        if (order is null)
            throw AppException.NotFound("Order was not found.");

        return await _orders.GetBoothIdForCustomerOrderAsync(customerId, orderId, cancellationToken)
            ?? throw AppException.BadRequest("Order has no booth items.", "ORDER_HAS_NO_ITEMS");
    }

    private static bool CanWithdraw(ComplaintStatus status)
        => ActiveStatuses.Contains(status);

    private static bool CanAddEvidence(ComplaintStatus status)
        => status == ComplaintStatus.WaitingForCustomer;

    private static void ValidateStatusTransition(ComplaintStatus currentStatus, ComplaintStatus nextStatus)
    {
        if (currentStatus == nextStatus)
            throw AppException.BadRequest("Complaint is already in the requested status.");

        var allowed = currentStatus switch
        {
            ComplaintStatus.Pending => nextStatus is ComplaintStatus.UnderReview or ComplaintStatus.InProgress
                or ComplaintStatus.WaitingForCustomer or ComplaintStatus.Resolved or ComplaintStatus.Rejected
                or ComplaintStatus.Closed,
            ComplaintStatus.UnderReview => nextStatus is ComplaintStatus.InProgress or ComplaintStatus.WaitingForCustomer
                or ComplaintStatus.Resolved or ComplaintStatus.Rejected or ComplaintStatus.Closed,
            ComplaintStatus.InProgress => nextStatus is ComplaintStatus.UnderReview or ComplaintStatus.WaitingForCustomer
                or ComplaintStatus.Resolved or ComplaintStatus.Rejected or ComplaintStatus.Closed,
            ComplaintStatus.WaitingForCustomer => nextStatus is ComplaintStatus.UnderReview or ComplaintStatus.InProgress
                or ComplaintStatus.Resolved or ComplaintStatus.Rejected or ComplaintStatus.Closed,
            ComplaintStatus.Resolved => nextStatus is ComplaintStatus.Closed,
            ComplaintStatus.Rejected => false,
            ComplaintStatus.Closed => false,
            ComplaintStatus.Withdrawn => false,
            _ => false
        };

        if (!allowed)
            throw AppException.BadRequest($"Cannot change complaint status from {currentStatus} to {nextStatus}.");
    }

    private static void ValidateResolutionRequest(UpdateComplaintStatusRequest request)
    {
        var adminResponse = TextHelper.NormalizeOptionalText(request.AdminResponse);
        var evidenceNote = TextHelper.NormalizeOptionalText(request.EvidenceRequestNote);
        var policyViolation = TextHelper.NormalizeOptionalText(request.PolicyViolation);

        if (request.Status is ComplaintStatus.Resolved or ComplaintStatus.Rejected && adminResponse is null)
            throw AppException.BadRequest("Admin response is required when resolving or rejecting a complaint.");

        if (request.Status == ComplaintStatus.WaitingForCustomer && adminResponse is null && evidenceNote is null)
            throw AppException.BadRequest("Evidence request note or admin response is required when waiting for customer.");

        if (adminResponse is not null && adminResponse.Length < 10 &&
            request.Status is ComplaintStatus.Resolved or ComplaintStatus.Rejected)
            throw AppException.BadRequest("Admin response must be at least 10 characters.");

        if (request.Status == ComplaintStatus.Resolved)
        {
            if (request.ResolutionAction is null)
                throw AppException.BadRequest("Resolution action is required when resolving a complaint.");

            if (request.ResolutionAction is not (ComplaintResolutionAction.NoViolation or ComplaintResolutionAction.Warning or ComplaintResolutionAction.SuspendBooth))
                throw AppException.BadRequest("Invalid resolution action. Only no violation, warning, and ban booth are allowed.");

            if (request.ResolutionAction != ComplaintResolutionAction.NoViolation && policyViolation is null)
                throw AppException.BadRequest("Policy violation is required when applying a penalty.");
        }

        if (request.Status == ComplaintStatus.Rejected && (request.ResolutionAction is not null || policyViolation is not null))
            throw AppException.BadRequest("Rejected complaints cannot apply a booth penalty or policy violation.");
    }

    private static string GetCategoryTitle(ComplaintCategory category)
        => category switch
        {
            ComplaintCategory.FoodQuality => "Food quality / Chất lượng món ăn",
            ComplaintCategory.WrongItem => "Wrong item / Sai món",
            ComplaintCategory.MissingItem => "Missing item / Thiếu món",
            ComplaintCategory.DelayedOrder => "Delayed order / Đơn xử lý chậm",
            ComplaintCategory.BoothBehavior => "Booth behavior / Thái độ / hành vi gian hàng",
            ComplaintCategory.PaymentIssue => "Payment issue / Vấn đề thanh toán",
            ComplaintCategory.PromotionIssue => "Promotion issue / Vấn đề khuyến mãi",
            _ => "Other / Khác"
        };

    private ComplaintResponse ToResponse(Complaint complaint)
    {
        var response = _mapper.Map<ComplaintResponse>(complaint);
        response.ImageUrls = complaint.ComplaintImages?.Select(i => i.ImageUrl).ToList() ?? [];
        response.EvidenceRequestNote = complaint.CustomerEvidenceRequestNote;
        response.BoothOwnerResponse = complaint.BoothOwnerResponse;
        response.CanWithdraw = CanWithdraw(complaint.Status);
        response.CanAddEvidence = CanAddEvidence(complaint.Status);
        response.StatusHistory = (complaint.StatusHistories ?? [])
            .OrderBy(h => h.CreatedAt)
            .ThenBy(h => h.Id)
            .Select(h => new ComplaintStatusHistoryResponse
            {
                Id = h.Id,
                FromStatus = h.FromStatus?.ToString(),
                ToStatus = h.ToStatus.ToString(),
                Note = h.Note,
                ActorUserId = h.ActorUserId,
                ActorRole = h.ActorRole,
                CreatedAt = h.CreatedAt
            })
            .ToList();
        return response;
    }

    private async Task TryNotifyAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.NotifyAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Complaint was persisted but notification delivery failed: {exception.Message}");
        }
    }

    private async Task TryNotifyRoleAsync(RoleNotificationMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.NotifyRoleAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Complaint was persisted but role notification delivery failed: {exception.Message}");
        }
    }
}

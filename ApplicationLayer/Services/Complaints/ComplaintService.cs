using ApplicationLayer.DTOs;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;
using ApplicationLayer.Services.Notifications;
using System.Text.Json;

namespace ApplicationLayer.Services.Complaints;

public class ComplaintService : IComplaintService
{
    private readonly IComplaintRepository _complaints;
    private readonly IBoothRepository _booths;
    private readonly IOrderRepository _orders;
    private readonly INightMarketRepository _nightMarkets;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IModerationRepository _moderation;
    private readonly IMapper _mapper;
    private readonly INotificationService _notifications;

    public ComplaintService(
        IComplaintRepository complaints,
        IBoothRepository booths,
        IOrderRepository orders,
        INightMarketRepository nightMarkets,
        ISubscriptionRepository subscriptions,
        IModerationRepository moderation,
        IMapper mapper,
        INotificationService notifications)
    {
        _complaints = complaints;
        _booths = booths;
        _orders = orders;
        _nightMarkets = nightMarkets;
        _subscriptions = subscriptions;
        _moderation = moderation;
        _mapper = mapper;
        _notifications = notifications;
    }

    public async Task<ApiResponse<ComplaintResponse>> CreateAsync(Guid customerId, CreateComplaintRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateOrderForBoothAsync(customerId, request.OrderId, request.BoothId);

        if (await _complaints.HasActiveComplaintAsync(customerId, request.BoothId, request.OrderId))
            throw AppException.Conflict("There is already an active complaint for this order and booth.");

        var now = DateTime.UtcNow;
        var complaint = _mapper.Map<Complaint>(request);
        complaint.Id = Guid.NewGuid();
        complaint.CustomerId = customerId;
        complaint.Status = ComplaintStatus.Pending;
        complaint.CreatedAt = now;
        complaint.UpdatedAt = now;

        await _complaints.AddAsync(complaint);

        var images = request.Images
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
        {
            await _complaints.AddImagesAsync(images);
        }

        await _complaints.SaveChangesAsync();
        var booth = await _booths.GetByIdAsync(complaint.BoothId);
        if (booth is not null)
        {
            await _notifications.NotifyAsync(new NotificationMessage(
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
        await _notifications.NotifyRoleAsync(new RoleNotificationMessage(
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
                await _notifications.NotifyAsync(new NotificationMessage(
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

        var response = _mapper.Map<ComplaintResponse>(complaint);
        response.ImageUrls = images.Select(i => i.ImageUrl).ToList();
        return ApiResponse<ComplaintResponse>.SuccessResponse(response, "Complaint submitted successfully.");
    }

    public async Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetMineAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var page = await _complaints.GetPagedByCustomerWithImagesAsync(
            customerId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<ComplaintResponse>>.SuccessResponse(
            _mapper.MapPage<Complaint, ComplaintResponse>(page, pagination));
    }

    public async Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetAllAsync(PaginationReq pagination, ComplaintStatus? status = null, CancellationToken cancellationToken = default)
    {
        var page = await _complaints.GetPagedWithImagesAsync(
            pagination.Page, pagination.PageSize, status, cancellationToken);
        return ApiResponse<PaginationResp<ComplaintResponse>>.SuccessResponse(
            _mapper.MapPage<Complaint, ComplaintResponse>(page, pagination));
    }

    public async Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetAllFilteredAsync(AdminComplaintQueryRequest query, CancellationToken cancellationToken = default)
    {
        var page = await _complaints.GetPagedWithImagesFilteredAsync(
            query.Page, query.PageSize, query.Status, query.Keyword, query.BoothId, cancellationToken);
        return ApiResponse<PaginationResp<ComplaintResponse>>.SuccessResponse(
            _mapper.MapPage<Complaint, ComplaintResponse>(page, new PaginationReq { Page = query.Page, PageSize = query.PageSize }));
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
            _mapper.MapPage<Complaint, ComplaintResponse>(page, pagination));
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
            _mapper.MapPage<Complaint, ComplaintResponse>(page, query));
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

        var response = _mapper.Map<ComplaintResponse>(complaint);
        response.ImageUrls = complaint.ComplaintImages?.Select(i => i.ImageUrl).ToList() ?? new List<string>();
        return ApiResponse<ComplaintResponse>.SuccessResponse(response);
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
            Total = pending + resolved + rejected
        });
    }

    public async Task<ApiResponse<ComplaintResponse>> UpdateStatusAsync(Guid complaintId, UpdateComplaintStatusRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
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
                throw AppException.Forbidden("Only Admin can suspend a booth.", "ADMIN_SANCTION_REQUIRED");
        }

        if (complaint.Status != ComplaintStatus.Pending)
            throw AppException.Conflict("Complaint has already been processed.", "COMPLAINT_ALREADY_PROCESSED");

        ValidateStatusTransition(complaint.Status, request.Status);
        ValidateResolutionRequest(request);

        var adminResponse = TextHelper.NormalizeOptionalText(request.AdminResponse);
        var policyViolation = TextHelper.NormalizeOptionalText(request.PolicyViolation);
        var now = DateTime.UtcNow;

        await _complaints.BeginTransactionAsync();
        try
        {
            var resolutionAction = request.Status == ComplaintStatus.Resolved
                ? request.ResolutionAction
                : null;
            var policyViolationValue = request.Status == ComplaintStatus.Resolved
                ? policyViolation
                : null;

            var rowsAffected = await _complaints.UpdateStatusWithConcurrencyAsync(
                complaintId,
                ComplaintStatus.Pending,
                request.Status,
                adminResponse,
                resolutionAction,
                policyViolationValue,
                now);

            if (rowsAffected == 0)
                throw AppException.Conflict("Complaint has already been processed by another administrator.", "COMPLAINT_ALREADY_PROCESSED");

            // Sync tracked entity with ExecuteUpdateAsync results so re-fetch returns fresh data
            complaint.Status = request.Status;
            complaint.AdminResponse = adminResponse;
            complaint.ResolutionAction = resolutionAction;
            complaint.PolicyViolation = policyViolationValue;
            complaint.UpdatedAt = now;

            if (request.Status == ComplaintStatus.Resolved && request.ResolutionAction == ComplaintResolutionAction.SuspendBooth)
            {
                var booth = await _booths.GetByIdAsync(complaint.BoothId);
                if (booth is not null)
                {
                    var previousBoothStatus = booth.Status;
                    booth.Status = BoothStatus.Suspended;
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
                        NewStatus = BoothStatus.Suspended.ToString(),
                        Reason = $"Auto-suspended via complaint: {complaint.Title}",
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

        var notificationType = request.Status switch
        {
            ComplaintStatus.Resolved => NotificationType.ComplaintResolved,
            ComplaintStatus.Rejected => NotificationType.ComplaintRejected,
            _ => NotificationType.Complaint
        };

        var actionLabel = request.Status == ComplaintStatus.Resolved
            ? updated?.ResolutionAction?.ToString() ?? "NoViolation"
            : "Rejected";
        var customerContent = $"Your complaint \"{updated?.Title}\" has been {request.Status}.\n\nAdmin response:\n{adminResponse}";
        if (request.Status == ComplaintStatus.Resolved && updated?.ResolutionAction != ComplaintResolutionAction.NoViolation)
            customerContent += $"\n\nAction taken: {updated?.ResolutionAction}";
        if (!string.IsNullOrEmpty(policyViolation))
            customerContent += $"\n\nPolicy violation: {policyViolation}";

        try
        {
            await _notifications.NotifyAsync(new NotificationMessage(
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
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to notify customer: {ex.Message}");
        }

        var boothEntity = await _booths.GetByIdAsync(complaint.BoothId);
        if (boothEntity is not null)
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
                else if (request.Status == ComplaintStatus.Resolved)
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
                            "Booth suspended",
                            $"Your booth \"{boothEntity.BoothName}\" has been suspended following a complaint.\n\nViolation: {policyViolation}"),
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

        return ApiResponse<ComplaintResponse>.SuccessResponse(ToResponse(updated!), "Complaint status updated successfully.");
    }

    private async Task ValidateOrderForBoothAsync(Guid customerId, Guid orderId, Guid boothId)
    {
        if (await _booths.GetByIdAsync(boothId) is null)
            throw AppException.NotFound("Booth was not found.");

        var order = await _orders.GetByCustomerAsync(customerId, orderId);
        if (order is null)
            throw AppException.NotFound("Order was not found.");

        if (!await _orders.ContainsBoothItemsAsync(orderId, boothId))
            throw AppException.BadRequest("Order does not contain items from this booth.");
    }

    private static void ValidateStatusTransition(ComplaintStatus currentStatus, ComplaintStatus nextStatus)
    {
        if (currentStatus == nextStatus)
            throw AppException.BadRequest("Complaint is already in the requested status.");

        var allowed = currentStatus switch
        {
            ComplaintStatus.Pending => nextStatus is ComplaintStatus.Resolved or ComplaintStatus.Rejected,
            ComplaintStatus.Resolved => false,
            ComplaintStatus.Rejected => false,
            _ => false
        };

        if (!allowed)
            throw AppException.BadRequest($"Cannot change complaint status from {currentStatus} to {nextStatus}.");
    }

    private static void ValidateResolutionRequest(UpdateComplaintStatusRequest request)
    {
        var adminResponse = TextHelper.NormalizeOptionalText(request.AdminResponse);
        var policyViolation = TextHelper.NormalizeOptionalText(request.PolicyViolation);

        if (request.Status is ComplaintStatus.Resolved or ComplaintStatus.Rejected && adminResponse is null)
            throw AppException.BadRequest("Admin response is required when resolving or rejecting a complaint.");

        if (adminResponse is not null && adminResponse.Length < 10)
            throw AppException.BadRequest("Admin response must be at least 10 characters.");

        if (request.Status == ComplaintStatus.Resolved)
        {
            if (request.ResolutionAction is null)
                throw AppException.BadRequest("Resolution action is required when resolving a complaint.");

            if (request.ResolutionAction is not (ComplaintResolutionAction.NoViolation or ComplaintResolutionAction.Warning or ComplaintResolutionAction.SuspendBooth))
                throw AppException.BadRequest("Invalid resolution action. Only NoViolation, Warning, and SuspendBooth are allowed.");

            if (request.ResolutionAction != ComplaintResolutionAction.NoViolation && policyViolation is null)
                throw AppException.BadRequest("Policy violation is required when applying a penalty.");
        }

        if (request.Status == ComplaintStatus.Rejected && (request.ResolutionAction is not null || policyViolation is not null))
            throw AppException.BadRequest("Rejected complaints cannot apply a booth penalty or policy violation.");
    }

    private ComplaintResponse ToResponse(Complaint complaint)
        => _mapper.Map<ComplaintResponse>(complaint);

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
}

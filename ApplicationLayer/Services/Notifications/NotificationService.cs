using System.Text.Json;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using static DomainLayer.Enums.GeneralEnum;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Realtime;

namespace ApplicationLayer.Services.Notifications;

public class NotificationService : INotificationService
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin", "BoothOwner", "MarketOwner", "Customer"
    };

    private readonly INotificationRepository _notifications;
    private readonly IUserDeviceTokenRepository _deviceTokens;
    private readonly IUserRepository _users;
    private readonly IPushNotificationService _push;
    private readonly IRealtimeNotificationPublisher _realtime;
    private readonly IRealtimeEventPublisher _eventPublisher;
    private readonly IOnlinePresenceService _presence;
    private readonly IMapper _mapper;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository notifications,
        IUserDeviceTokenRepository deviceTokens,
        IUserRepository users,
        IPushNotificationService push,
        IRealtimeNotificationPublisher realtime,
        IRealtimeEventPublisher eventPublisher,
        IOnlinePresenceService presence,
        IMapper mapper,
        ILogger<NotificationService> logger)
    {
        _notifications = notifications;
        _deviceTokens = deviceTokens;
        _users = users;
        _push = push;
        _realtime = realtime;
        _eventPublisher = eventPublisher;
        _presence = presence;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ApiResponse<PaginationResp<NotificationListItemResponse>>> GetAsync(
        Guid userId,
        NotificationListRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await _notifications.GetPagedByUserAsync(
            userId,
            request.Type,
            request.IsRead,
            request.Page,
            request.PageSize,
            cancellationToken);

        return ApiResponse<PaginationResp<NotificationListItemResponse>>.SuccessResponse(
            _mapper.MapPage<Notification, NotificationListItemResponse>(page, request));
    }

    public async Task<ApiResponse<NotificationDetailResponse>> GetDetailAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await GetOwnedAsync(userId, notificationId, cancellationToken);
        return ApiResponse<NotificationDetailResponse>.SuccessResponse(
            _mapper.Map<NotificationDetailResponse>(notification));
    }

    public async Task<ApiResponse<NotificationDetailResponse>> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await GetOwnedAsync(userId, notificationId, cancellationToken);
        if (!notification.IsRead)
        {
            var now = DateTime.UtcNow;
            notification.IsRead = true;
            notification.ReadAt = now;
            notification.UpdatedAt = now;
            await _notifications.SaveChangesAsync();
            var unreadCount = await _notifications.CountUnreadAsync(userId, cancellationToken);
            try
            {
                await _eventPublisher.PublishAsync(new RealtimeEvent
                {
                    EventType = "NotificationRead",
                    RecipientId = userId,
                    Payload = new { notificationId, unreadCount }
                }, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    "Failed to publish NotificationRead event for {NotificationId}.",
                    notificationId);
            }
        }

        return ApiResponse<NotificationDetailResponse>.SuccessResponse(
            _mapper.Map<NotificationDetailResponse>(notification),
            "Notification marked as read.");
    }

    public async Task<ApiResponse<object>> MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var updatedCount = await _notifications.MarkAllAsReadAsync(
            userId,
            DateTime.UtcNow,
            cancellationToken);
        try
        {
            await _eventPublisher.PublishAsync(new RealtimeEvent
            {
                EventType = "NotificationRead",
                RecipientId = userId,
                Payload = new { notificationId = (Guid?)null, unreadCount = 0 }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish NotificationRead (all) event for user {UserId}.",
                userId);
        }
        return ApiResponse<object>.SuccessResponse(
            new { UpdatedCount = updatedCount },
            "All notifications marked as read.");
    }

    public async Task MarkReferenceReadAsync(
        Guid userId,
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(referenceType))
            return;

        var updatedCount = await _notifications.MarkReferenceAsReadAsync(
            userId,
            referenceType.Trim(),
            referenceId,
            DateTime.UtcNow,
            cancellationToken);
        if (updatedCount > 0)
        {
            var unreadCount = await _notifications.CountUnreadAsync(userId, cancellationToken);
            try
            {
                await _eventPublisher.PublishAsync(new RealtimeEvent
                {
                    EventType = "NotificationRead",
                    RecipientId = userId,
                    Payload = new { notificationId = (Guid?)null, unreadCount }
                }, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    "Failed to publish NotificationRead (reference) event for user {UserId}.",
                    userId);
            }
        }
    }

    public async Task<ApiResponse<UnreadNotificationCountResponse>> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => ApiResponse<UnreadNotificationCountResponse>.SuccessResponse(new()
        {
            UnreadCount = await _notifications.CountUnreadAsync(userId, cancellationToken)
        });



    public async Task NotifyAsync(
        NotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        ValidateMessage(message);
        var notification = CreateEntity(message, DateTime.UtcNow);
        await _notifications.AddAsync(notification);
        await _notifications.SaveChangesAsync();
        await DeliverAsync(notification, cancellationToken);
    }

    public async Task NotifyRoleAsync(
        RoleNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.Role)
            || !AllowedRoles.Contains(message.Role.Trim()))
        {
            throw AppException.BadRequest(
                "Notification role is invalid.",
                "NOTIFICATION_TARGET_INVALID");
        }

        var recipientIds = await _users.GetActiveRecipientIdsAsync(
            null,
            message.Role.Trim(),
            cancellationToken);
        if (recipientIds.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var notifications = recipientIds.Select(userId => CreateEntity(
            new NotificationMessage(
                userId,
                message.Type,
                message.Title,
                message.Content,
                message.BoothId,
                message.ReferenceType,
                message.ReferenceId,
                message.DataJson),
            now)).ToList();

        await _notifications.AddRangeAsync(notifications);
        await _notifications.SaveChangesAsync();
        foreach (var notification in notifications)
            await DeliverAsync(notification, cancellationToken);
    }

    private string? CanonicalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return null;
        var r = role.Trim();
        if (r.Equals("Admin", StringComparison.OrdinalIgnoreCase)) return "Admin";
        if (r.Equals("Customer", StringComparison.OrdinalIgnoreCase)) return "Customer";
        if (r.Equals("BoothOwner", StringComparison.OrdinalIgnoreCase)) return "BoothOwner";
        if (r.Equals("MarketOwner", StringComparison.OrdinalIgnoreCase)) return "MarketOwner";
        return r;
    }

    public async Task<ApiResponse<AdminNotificationResultResponse>> CreateByAdminAsync(
        Guid currentAdminId,
        AdminCreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateAdminRequest(request);
        var canonicalRole = CanonicalizeRole(request.Role);
        var recipientIds = request.Target switch
        {
            NotificationTarget.AllUsers => await _users.GetActiveRecipientIdsAsync(
                null, null, cancellationToken),
            NotificationTarget.Role => await _users.GetActiveRecipientIdsAsync(
                null, canonicalRole, cancellationToken),
            NotificationTarget.SpecificUser => await _users.GetActiveRecipientIdsAsync(
                request.UserId, null, cancellationToken),
            _ => throw AppException.BadRequest(
                "Notification target is invalid.",
                "NOTIFICATION_TARGET_INVALID")
        };

        if (recipientIds.Count == 0)
            throw AppException.NotFound(
                "No active notification recipients were found.",
                "NOTIFICATION_RECIPIENT_NOT_FOUND");

        var now = DateTime.UtcNow;
        var batchId = Guid.NewGuid();

        var notifications = recipientIds
            .Distinct()
            .Select(userId =>
            {
                var n = CreateEntity(new NotificationMessage(
                    userId,
                    NotificationType.SystemAnnouncement,
                    request.Title,
                    request.Content,
                    null,
                    null,
                    null,
                    null), now);
                n.BatchId = batchId;
                n.CreatedByUserId = currentAdminId;
                n.Target = request.Target;
                n.TargetRole = request.Target == NotificationTarget.Role ? canonicalRole : null;
                return n;
            })
            .ToList();

        await _notifications.AddRangeAsync(notifications);
        await _notifications.SaveChangesAsync();

        foreach (var notification in notifications)
            await DeliverAsync(notification, cancellationToken);

        return ApiResponse<AdminNotificationResultResponse>.SuccessResponse(new()
        {
            BatchId = batchId,
            RecipientCount = recipientIds.Count,
            NotificationCount = notifications.Count
        }, "Notification created successfully.");
    }

    public async Task<ApiResponse<PaginationResp<AdminNotificationListItemResponse>>> GetAdminNotificationsAsync(
        AdminNotificationListRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate.Value > request.ToDate.Value)
        {
            throw AppException.BadRequest("FromDate cannot be greater than ToDate.", "INVALID_DATE_RANGE");
        }

        var page = await _notifications.GetAdminPagedBatchesAsync(
            request.Keyword,
            request.Target,
            request.Role,
            request.Type,
            request.FromDate,
            request.ToDate,
            request.Page,
            request.PageSize,
            cancellationToken);

        var batchIds = page.Items.Select(x => x.BatchId).Where(b => b.HasValue).Select(b => b!.Value).ToList();

        // Count recipients per batch
        var recipientCounts = new Dictionary<Guid, int>();
        if (batchIds.Count > 0)
        {
            recipientCounts = await _notifications.GetBatchRecipientCountsAsync(batchIds, cancellationToken);
        }

        // Fetch admin names
        var adminIds = page.Items.Where(x => x.CreatedByUserId.HasValue).Select(x => x.CreatedByUserId!.Value).Distinct().ToList();
        var adminDict = new Dictionary<Guid, string>();
        if (adminIds.Count > 0)
        {
            adminDict = await _users.GetUserNamesByIdsAsync(adminIds, cancellationToken);
        }

        // Fetch specific user names
        var userIds = page.Items.Where(x => x.Target == NotificationTarget.SpecificUser).Select(x => x.UserId).Distinct().ToList();
        var userDict = new Dictionary<Guid, string>();
        if (userIds.Count > 0)
        {
            userDict = await _users.GetUserNamesByIdsAsync(userIds, cancellationToken);
        }

        var list = page.Items.Select(x => new AdminNotificationListItemResponse
        {
            BatchId = x.BatchId!.Value,
            Title = x.Title,
            ContentPreview = x.Content.Length > 100 ? x.Content.Substring(0, 97) + "..." : x.Content,
            Type = x.Type.ToString(),
            Target = x.Target.ToString() ?? "",
            TargetRole = x.TargetRole,
            SpecificUserName = x.Target == NotificationTarget.SpecificUser && userDict.ContainsKey(x.UserId) ? userDict[x.UserId] : null,
            RecipientCount = recipientCounts.TryGetValue(x.BatchId.Value, out var c) ? c : 1,
            CreatedByName = x.CreatedByUserId.HasValue && adminDict.ContainsKey(x.CreatedByUserId.Value) ? adminDict[x.CreatedByUserId.Value] : "Unknown",
            CreatedAt = x.CreatedAt
        }).ToList();

        var pagedResp = PaginationResp<AdminNotificationListItemResponse>.Create(
            list,
            page.TotalCount,
            request);

        return ApiResponse<PaginationResp<AdminNotificationListItemResponse>>.SuccessResponse(pagedResp);
    }

    public async Task<ApiResponse<AdminNotificationDetailResponse>> GetAdminNotificationDetailAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var n = await _notifications.GetAdminBatchDetailAsync(batchId, cancellationToken);
        if (n == null)
            throw AppException.NotFound("The notification could not be found.", "NOTIFICATION_NOT_FOUND");

        var count = await _notifications.CountByBatchIdAsync(batchId, cancellationToken);

        string adminName = "Unknown";
        if (n.CreatedByUserId.HasValue)
        {
            var admin = await _users.GetByIdAsync(n.CreatedByUserId.Value);
            if (admin != null) adminName = admin.FullName;
        }

        AdminNotificationSpecificUserResponse? specificUser = null;
        if (n.Target == NotificationTarget.SpecificUser && n.User != null)
        {
            specificUser = new AdminNotificationSpecificUserResponse
            {
                FullName = n.User.FullName,
                Email = n.User.Email
            };
        }

        var resp = new AdminNotificationDetailResponse
        {
            BatchId = batchId,
            Title = n.Title,
            Content = n.Content,
            Type = n.Type.ToString(),
            Target = n.Target.ToString() ?? "",
            TargetRole = n.TargetRole,
            SpecificUser = specificUser,
            RecipientCount = count,
            CreatedByName = adminName,
            CreatedAt = n.CreatedAt
        };

        return ApiResponse<AdminNotificationDetailResponse>.SuccessResponse(resp);
    }

    private async Task<Notification> GetOwnedAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken)
        => await _notifications.GetByUserAsync(notificationId, userId, cancellationToken)
            ?? throw AppException.NotFound(
                "Notification was not found.",
                "NOTIFICATION_NOT_FOUND");

    private async Task DeliverAsync(
        Notification notification,
        CancellationToken cancellationToken)
    {
        var response = _mapper.Map<NotificationListItemResponse>(notification);
        var unreadCount = await _notifications.CountUnreadAsync(
            notification.UserId,
            cancellationToken);

        try
        {
            await _realtime.PublishAsync(
                notification.UserId,
                response,
                unreadCount,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Legacy realtime notification delivery failed for notification {NotificationId}.",
                notification.Id);
        }

        try
        {
            await _eventPublisher.PublishAsync(new RealtimeEvent
            {
                EventType = "NotificationCreated",
                RecipientId = notification.UserId,
                Payload = new { notification = response, unreadCount }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Unified realtime notification delivery failed for notification {NotificationId}.",
                notification.Id);
        }

        if (_presence.IsOnline(notification.UserId))
            return;

        try
        {
            var tokens = await _deviceTokens.GetActiveByUserAsync(
                notification.UserId,
                cancellationToken);
            if (tokens.Count == 0)
                return;

            var data = new Dictionary<string, string>
            {
                ["notificationId"] = notification.Id.ToString(),
                ["type"] = notification.Type.ToString()
            };
            if (notification.ReferenceType is not null)
                data["referenceType"] = notification.ReferenceType;
            if (notification.ReferenceId.HasValue)
                data["referenceId"] = notification.ReferenceId.Value.ToString();
            if (notification.BoothId.HasValue)
                data["boothId"] = notification.BoothId.Value.ToString();
            if (notification.DataJson is not null)
                data["dataJson"] = notification.DataJson;

            var result = await _push.SendAsync(
                tokens.Select(item => item.Token).ToList(),
                new PushNotificationMessage(
                    notification.Title,
                    notification.Content,
                    data),
                cancellationToken);

            if (result.InvalidTokens.Count == 0)
                return;

            var invalid = tokens
                .Where(item => result.InvalidTokens.Contains(item.Token))
                .ToList();
            foreach (var token in invalid)
            {
                token.IsActive = false;
                token.UpdatedAt = DateTime.UtcNow;
            }

            _deviceTokens.UpdateRange(invalid);
            await _deviceTokens.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Push delivery failed for notification {NotificationId}; the in-app notification remains available.",
                notification.Id);
        }
    }

    private static Notification CreateEntity(NotificationMessage message, DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = message.UserId,
            BoothId = message.BoothId,
            Type = message.Type,
            Title = message.Title.Trim(),
            Content = message.Content.Trim(),
            IsRead = false,
            ReferenceType = Normalize(message.ReferenceType),
            ReferenceId = message.ReferenceId,
            DataJson = Normalize(message.DataJson),
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static void ValidateMessage(NotificationMessage message)
    {
        if (message.UserId == Guid.Empty)
            throw AppException.BadRequest(
                "Notification user is required.",
                "NOTIFICATION_USER_REQUIRED");
        if (string.IsNullOrWhiteSpace(message.Title)
            || string.IsNullOrWhiteSpace(message.Content))
        {
            throw AppException.BadRequest(
                "Notification title and content are required.",
                "NOTIFICATION_CONTENT_REQUIRED");
        }

        ValidateDataJson(message.DataJson);
    }

    private static void ValidateAdminRequest(AdminCreateNotificationRequest request)
    {
        ValidateMessage(new NotificationMessage(
            request.UserId ?? Guid.NewGuid(),
            NotificationType.SystemAnnouncement,
            request.Title,
            request.Content,
            null,
            null,
            null,
            null));

        if (request.Target == NotificationTarget.SpecificUser
            && (!request.UserId.HasValue || request.UserId == Guid.Empty))
        {
            throw AppException.BadRequest(
                "UserId is required for a specific-user notification.",
                "NOTIFICATION_TARGET_INVALID");
        }

        if (request.Target == NotificationTarget.Role
            && (string.IsNullOrWhiteSpace(request.Role)
                || !AllowedRoles.Contains(request.Role.Trim())))
        {
            throw AppException.BadRequest(
                "A valid role is required for a role notification.",
                "NOTIFICATION_TARGET_INVALID");
        }
    }

    private static void ValidateDataJson(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return;

        try
        {
            using var _ = JsonDocument.Parse(dataJson);
        }
        catch (JsonException)
        {
            throw AppException.BadRequest(
                "DataJson must contain valid JSON.",
                "NOTIFICATION_DATA_INVALID");
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

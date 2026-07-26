using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Notifications;

public interface INotificationService
{
    Task<ApiResponse<PaginationResp<NotificationListItemResponse>>> GetAsync(Guid userId, NotificationListRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<NotificationDetailResponse>> GetDetailAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);

    Task<ApiResponse<NotificationDetailResponse>> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ApiResponse<UnreadNotificationCountResponse>> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default);

    Task NotifyRoleAsync(RoleNotificationMessage message, CancellationToken cancellationToken = default);

    Task<ApiResponse<AdminNotificationResultResponse>> CreateByAdminAsync(Guid currentAdminId, AdminCreateNotificationRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<PaginationResp<AdminNotificationListItemResponse>>> GetAdminNotificationsAsync(AdminNotificationListRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<AdminNotificationDetailResponse>> GetAdminNotificationDetailAsync(Guid batchId, CancellationToken cancellationToken = default);
}

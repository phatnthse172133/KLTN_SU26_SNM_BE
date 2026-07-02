using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class NotificationListRequest : PaginationReq
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NotificationType? Type { get; set; }
    public bool? IsRead { get; set; }
}

public class RegisterDeviceTokenRequest
{
    [Required, StringLength(4096, MinimumLength = 10)]
    public string Token { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DevicePlatform Platform { get; set; }

    [StringLength(200)]
    public string? DeviceId { get; set; }
}

public class AdminCreateNotificationRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NotificationTarget Target { get; set; }

    public Guid? UserId { get; set; }

    [StringLength(50)]
    public string? Role { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NotificationType Type { get; set; } = NotificationType.SystemAnnouncement;

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Content { get; set; } = string.Empty;

    public Guid? BoothId { get; set; }

    [StringLength(100)]
    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    [StringLength(8000)]
    public string? DataJson { get; set; }
}

namespace ApplicationLayer.DTOs.Responses;

public class NotificationListItemResponse
{
    public Guid Id { get; set; }
    public Guid? BoothId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationDetailResponse : NotificationListItemResponse
{
    public string? DataJson { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UnreadNotificationCountResponse
{
    public int UnreadCount { get; set; }
}

public class DeviceTokenResponse
{
    public Guid Id { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastUsedAt { get; set; }
}

public class AdminNotificationResultResponse
{
    public int RecipientCount { get; set; }
    public int NotificationCount { get; set; }
}

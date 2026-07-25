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
    public Guid BatchId { get; set; }
    public int RecipientCount { get; set; }
    public int NotificationCount { get; set; }
}

public class AdminNotificationListItemResponse
{
    public Guid BatchId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? TargetRole { get; set; }
    public string? SpecificUserName { get; set; }
    public int RecipientCount { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AdminNotificationDetailResponse
{
    public Guid BatchId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? TargetRole { get; set; }
    public AdminNotificationSpecificUserResponse? SpecificUser { get; set; }
    public int RecipientCount { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AdminNotificationSpecificUserResponse
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

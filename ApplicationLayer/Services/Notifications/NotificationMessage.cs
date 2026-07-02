using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Notifications;

public sealed record NotificationMessage(
    Guid UserId,
    NotificationType Type,
    string Title,
    string Content,
    Guid? BoothId = null,
    string? ReferenceType = null,
    Guid? ReferenceId = null,
    string? DataJson = null);

public sealed record RoleNotificationMessage(
    string Role,
    NotificationType Type,
    string Title,
    string Content,
    Guid? BoothId = null,
    string? ReferenceType = null,
    Guid? ReferenceId = null,
    string? DataJson = null);

public sealed record PushNotificationMessage(
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data);

public sealed record PushDeliveryResult(IReadOnlyCollection<string> InvalidTokens)
{
    public static PushDeliveryResult Empty { get; } = new([]);
}

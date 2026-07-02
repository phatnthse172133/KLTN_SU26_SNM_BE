namespace ApplicationLayer.Services.Notifications;

public interface IPushNotificationService
{
    Task<PushDeliveryResult> SendAsync(IReadOnlyCollection<string> tokens, PushNotificationMessage message, CancellationToken cancellationToken = default);
}

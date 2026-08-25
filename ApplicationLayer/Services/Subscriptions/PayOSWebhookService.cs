using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.PayOS;
using ApplicationLayer.Services.Realtime;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Subscriptions
{
    public interface IPayOSWebhookService
    {
        Task<WebhookDispatchResult> HandleWebhookAsync(PayOSWebhookData verifiedData);
        Task<bool> HasSubscriptionWithCodeAsync(long orderCode);
    }

    public class PayOSWebhookService : IPayOSWebhookService
    {
        private readonly ISubscriptionRepository _repo;
        private readonly INotificationService _notifications;
        private readonly IRealtimeEventPublisher _eventPublisher;
        private readonly ILogger<PayOSWebhookService> _logger;

        public PayOSWebhookService(ISubscriptionRepository repo, INotificationService notifications, IRealtimeEventPublisher eventPublisher, ILogger<PayOSWebhookService> logger)
        {
            _repo = repo;
            _notifications = notifications;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task<WebhookDispatchResult> HandleWebhookAsync(PayOSWebhookData verifiedData)
        {
            if (!verifiedData.IsSuccessful)
            {
                _logger.LogInformation("PayOS webhook received non-success code: {Code} for order {OrderCode}", verifiedData.Code, verifiedData.OrderCode);
                return WebhookDispatchResult.NotSuccessful;
            }

            await _repo.BeginTransactionAsync();
            try
            {
                var boothSub = await _repo.GetBoothSubscriptionByOrderCodeAsync(verifiedData.OrderCode);
                if (boothSub != null)
                {
                    var result = await ProcessBoothSubscriptionAsync(boothSub, verifiedData);
                    await _repo.CommitTransactionAsync();

                    if (result == WebhookDispatchResult.SubscriptionHandled)
                    {
                        await SendNotificationAsync(boothSub.Booth?.BoothOwnerId, boothSub.Package?.PackageName, "Booth", boothSub.Id);
                        await PublishSubscriptionActivatedAsync(boothSub.Booth?.BoothOwnerId, "Booth", boothSub.Id);
                    }

                    return result;
                }

                var marketSub = await _repo.GetMarketSubscriptionByOrderCodeAsync(verifiedData.OrderCode);
                if (marketSub != null)
                {
                    var result = await ProcessMarketSubscriptionAsync(marketSub, verifiedData);
                    await _repo.CommitTransactionAsync();

                    if (result == WebhookDispatchResult.SubscriptionHandled)
                    {
                        await SendNotificationAsync(marketSub.MarketOwnerId, marketSub.Package?.PackageName, "Market", marketSub.Id);
                        await PublishSubscriptionActivatedAsync(marketSub.MarketOwnerId, "Market", marketSub.Id);
                    }

                    return result;
                }

                await _repo.RollbackTransactionAsync();
                _logger.LogInformation("PayOS webhook: no subscription found for order code {OrderCode}. May belong to Order.", verifiedData.OrderCode);
                return WebhookDispatchResult.NotFound;
            }
            catch (AppException)
            {
                await _repo.RollbackTransactionAsync();
                throw;
            }
            catch (Exception ex)
            {
                await _repo.RollbackTransactionAsync();
                _logger.LogError(ex, "Error processing PayOS webhook for order {OrderCode}", verifiedData.OrderCode);
                throw;
            }
        }

        private async Task<WebhookDispatchResult> ProcessBoothSubscriptionAsync(DomainLayer.Entities.BoothSubscription sub, PayOSWebhookData webhookData)
        {
            if (sub.Status != SubscriptionStatus.PendingPayment)
            {
                _logger.LogInformation("PayOS webhook: booth subscription {Id} already processed (status={Status})", sub.Id, sub.Status);
                return WebhookDispatchResult.AlreadyProcessed;
            }

            if ((int)Math.Round(webhookData.Amount) != (int)Math.Round(sub.PaidAmount))
            {
                _logger.LogWarning("PayOS webhook: amount mismatch for subscription {Id}. Expected={Expected}, Got={Actual}", sub.Id, sub.PaidAmount, webhookData.Amount);
                throw AppException.BadRequest("Payment amount mismatch.", "PAYOS_AMOUNT_MISMATCH");
            }

            var now = DateTime.UtcNow;
            var durationDays = (sub.EndDate - sub.StartDate).Days;
            if (durationDays <= 0 && sub.Package != null) durationDays = sub.Package.DurationDays;
            if (durationDays <= 0) durationDays = 30;

            var existingActive = await _repo.GetLatestApprovedBoothSubscriptionAsync(sub.BoothId);
            var startDate = now;
            if (existingActive != null && existingActive.EndDate > now && existingActive.Id != sub.Id)
            {
                if (sub.ChangeType is "Upgrade" or "ChangePackage")
                {
                    var expiredRows = await _repo.UpdateBoothSubscriptionStatusAsync(
                        existingActive.Id,
                        SubscriptionStatus.Active,
                        SubscriptionStatus.Expired,
                        existingActive.StartDate,
                        now,
                        "Replaced by an upgrade.");
                    if (expiredRows != 1)
                        throw AppException.Conflict("The current subscription changed before the upgrade could be applied.", "SUBSCRIPTION_CHANGE_CONFLICT");
                }
                else
                {
                    // Renewals and downgrades preserve the active plan until its end date.
                    startDate = existingActive.EndDate;
                }
            }

            var endDate = startDate.AddDays(durationDays);

            var rowsAffected = await _repo.ActivateBoothSubscriptionAsync(sub.Id, startDate, endDate, now);
            if (rowsAffected == 0)
            {
                _logger.LogInformation("PayOS webhook: booth subscription {Id} already activated by concurrent process", sub.Id);
                return WebhookDispatchResult.AlreadyProcessed;
            }

            return WebhookDispatchResult.SubscriptionHandled;
        }

        private async Task<WebhookDispatchResult> ProcessMarketSubscriptionAsync(DomainLayer.Entities.MarketSubscription sub, PayOSWebhookData webhookData)
        {
            if (sub.Status != SubscriptionStatus.PendingPayment)
            {
                _logger.LogInformation("PayOS webhook: market subscription {Id} already processed (status={Status})", sub.Id, sub.Status);
                return WebhookDispatchResult.AlreadyProcessed;
            }

            if ((int)Math.Round(webhookData.Amount) != (int)Math.Round(sub.PaidAmount))
            {
                _logger.LogWarning("PayOS webhook: amount mismatch for subscription {Id}. Expected={Expected}, Got={Actual}", sub.Id, sub.PaidAmount, webhookData.Amount);
                throw AppException.BadRequest("Payment amount mismatch.", "PAYOS_AMOUNT_MISMATCH");
            }

            var now = DateTime.UtcNow;
            var durationDays = (sub.EndDate - sub.StartDate).Days;
            if (durationDays <= 0 && sub.Package != null) durationDays = sub.Package.DurationDays;
            if (durationDays <= 0) durationDays = 30;

            var existingActive = await _repo.GetLatestApprovedMarketSubscriptionAsync(sub.MarketOwnerId);
            var startDate = now;
            if (existingActive != null && existingActive.EndDate > now && existingActive.Id != sub.Id)
            {
                if (sub.ChangeType is "Upgrade" or "ChangePackage")
                {
                    var expiredRows = await _repo.UpdateMarketSubscriptionStatusAsync(
                        existingActive.Id,
                        SubscriptionStatus.Active,
                        SubscriptionStatus.Expired,
                        existingActive.StartDate,
                        now,
                        "Replaced by an upgrade.");
                    if (expiredRows != 1)
                        throw AppException.Conflict("The current subscription changed before the upgrade could be applied.", "SUBSCRIPTION_CHANGE_CONFLICT");
                }
                else
                {
                    // Renewals and downgrades preserve the active plan until its end date.
                    startDate = existingActive.EndDate;
                }
            }

            var endDate = startDate.AddDays(durationDays);

            var rowsAffected = await _repo.ActivateMarketSubscriptionAsync(sub.Id, startDate, endDate, now);
            if (rowsAffected == 0)
            {
                _logger.LogInformation("PayOS webhook: market subscription {Id} already activated by concurrent process", sub.Id);
                return WebhookDispatchResult.AlreadyProcessed;
            }

            return WebhookDispatchResult.SubscriptionHandled;
        }

        private async Task SendNotificationAsync(Guid? userId, string? packageName, string ownerType, Guid subscriptionId)
        {
            if (!userId.HasValue) return;

            var title = "Payment Successful";
            var content = $"Your payment was successful. Your {ownerType} subscription for \"{packageName}\" is now active.";

            try
            {
                await _notifications.NotifyAsync(new NotificationMessage(
                    userId.Value,
                    NotificationType.PaymentSucceeded,
                    title,
                    content,
                    null,
                    "Subscription",
                    subscriptionId,
                    JsonSerializer.Serialize(new { subscriptionId, packageType = ownerType, activated = true })));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send payment success notification for subscription {Id}", subscriptionId);
            }
        }

        private async Task PublishSubscriptionActivatedAsync(Guid? userId, string ownerType, Guid subscriptionId)
        {
            if (!userId.HasValue) return;
            try
            {
                var payload = new { subscriptionId, ownerType, status = "Active", activated = true };
                await _eventPublisher.PublishAsync(new RealtimeEvent
                {
                    EventType = "SubscriptionActivated",
                    RecipientId = userId.Value,
                    Payload = payload
                });
                await _eventPublisher.PublishAsync(new RealtimeEvent
                {
                    EventType = "SubscriptionChanged",
                    RecipientId = userId.Value,
                    Payload = payload
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish SubscriptionActivated/Changed event for subscription {Id}", subscriptionId);
            }
        }

        public async Task<bool> HasSubscriptionWithCodeAsync(long orderCode)
        {
            var booth = await _repo.GetBoothSubscriptionByOrderCodeAsync(orderCode);
            if (booth != null) return true;
            var market = await _repo.GetMarketSubscriptionByOrderCodeAsync(orderCode);
            return market != null;
        }
    }
}

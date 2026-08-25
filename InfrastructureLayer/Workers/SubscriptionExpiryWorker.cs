using InfrastructureLayer.Data;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Realtime;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Workers;

public class SubscriptionExpiryWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SubscriptionExpiryWorker> _logger;

    public SubscriptionExpiryWorker(IServiceProvider serviceProvider, ILogger<SubscriptionExpiryWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExpireSubscriptionsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in SubscriptionExpiryWorker. The worker will retry on the next iteration.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("SubscriptionExpiryWorker stopped because the host is shutting down.");
        }
    }

    protected virtual TimeSpan Interval => TimeSpan.FromHours(1);

    protected virtual async Task ExpireSubscriptionsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var realtime = scope.ServiceProvider.GetService<IRealtimeEventPublisher>();
        var now = DateTime.UtcNow;

        await SendExpiryWarningsAsync(context, notifications, now, stoppingToken);

        var expiringBooth = await context.BoothSubscriptions
            .Include(bs => bs.Booth)
            .Where(bs => bs.Status == SubscriptionStatus.Active && bs.EndDate < now)
            .Select(bs => new { bs.Id, OwnerId = bs.Booth != null ? bs.Booth.BoothOwnerId : (Guid?)null })
            .ToListAsync(stoppingToken);

        var expiringMarket = await context.MarketSubscriptions
            .Where(ms => ms.Status == SubscriptionStatus.Active && ms.EndDate < now)
            .Select(ms => new { ms.Id, OwnerId = (Guid?)ms.MarketOwnerId })
            .ToListAsync(stoppingToken);

        var expiredBooth = await context.BoothSubscriptions
            .Where(bs => bs.Status == SubscriptionStatus.Active && bs.EndDate < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(bs => bs.Status, SubscriptionStatus.Expired)
                .SetProperty(bs => bs.UpdatedAt, now), stoppingToken);

        var expiredMarket = await context.MarketSubscriptions
            .Where(ms => ms.Status == SubscriptionStatus.Active && ms.EndDate < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(ms => ms.Status, SubscriptionStatus.Expired)
                .SetProperty(ms => ms.UpdatedAt, now), stoppingToken);

        if (realtime is not null)
        {
            foreach (var item in expiringBooth.Where(x => x.OwnerId.HasValue))
            {
                try
                {
                    await realtime.PublishAsync(new RealtimeEvent
                    {
                        EventType = "SubscriptionChanged",
                        RecipientId = item.OwnerId,
                        Payload = new { subscriptionId = item.Id, ownerType = "Booth", status = "Expired", activated = false }
                    }, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish SubscriptionChanged for expired booth subscription {Id}", item.Id);
                }
            }

            foreach (var item in expiringMarket.Where(x => x.OwnerId.HasValue))
            {
                try
                {
                    await realtime.PublishAsync(new RealtimeEvent
                    {
                        EventType = "SubscriptionChanged",
                        RecipientId = item.OwnerId,
                        Payload = new { subscriptionId = item.Id, ownerType = "Market", status = "Expired", activated = false }
                    }, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish SubscriptionChanged for expired market subscription {Id}", item.Id);
                }
            }
        }

        if (expiredBooth > 0 || expiredMarket > 0)
        {
            _logger.LogInformation("Expired {BoothCount} booth subscriptions and {MarketCount} market subscriptions.", expiredBooth, expiredMarket);
        }
    }

    private static async Task SendExpiryWarningsAsync(
        SNMDbContext context,
        INotificationService notifications,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var warningEnd = now.AddDays(7);

        var boothSubscriptions = await context.BoothSubscriptions
            .Include(subscription => subscription.Booth)
                .ThenInclude(booth => booth.BoothOwner)
            .Include(subscription => subscription.Package)
            .Where(subscription => subscription.Status == SubscriptionStatus.Active
                && subscription.EndDate >= now
                && subscription.EndDate <= warningEnd)
            .ToListAsync(cancellationToken);

        var marketSubscriptions = await context.MarketSubscriptions
            .Include(subscription => subscription.MarketOwner)
            .Include(subscription => subscription.Package)
            .Where(subscription => subscription.Status == SubscriptionStatus.Active
                && subscription.EndDate >= now
                && subscription.EndDate <= warningEnd)
            .ToListAsync(cancellationToken);

        foreach (var subscription in boothSubscriptions)
        {
            var user = subscription.Booth?.BoothOwner;
            if (user is null) continue;

            await SendExpiryWarningAsync(
                context,
                notifications,
                subscription.Id,
                user.Id,
                user.Email,
                subscription.Package?.PackageName ?? "Booth package",
                subscription.EndDate,
                "BoothSubscriptionExpiry7Days",
                cancellationToken);
        }

        foreach (var subscription in marketSubscriptions)
        {
            if (subscription.MarketOwner is null) continue;

            await SendExpiryWarningAsync(
                context,
                notifications,
                subscription.Id,
                subscription.MarketOwner.Id,
                subscription.MarketOwner.Email,
                subscription.Package?.PackageName ?? "Market package",
                subscription.EndDate,
                "MarketSubscriptionExpiry7Days",
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SendExpiryWarningAsync(
        SNMDbContext context,
        INotificationService notifications,
        Guid subscriptionId,
        Guid userId,
        string recipientEmail,
        string packageName,
        DateTime endDate,
        string emailType,
        CancellationToken cancellationToken)
    {
        var alreadyNotified = await context.Notifications.AnyAsync(notification =>
            notification.UserId == userId
            && notification.Type == NotificationType.SubscriptionExpiring
            && notification.ReferenceType == "Subscription"
            && notification.ReferenceId == subscriptionId
            && !notification.IsDeleted,
            cancellationToken);

        var daysRemaining = Math.Max(0, (int)Math.Ceiling((endDate - DateTime.UtcNow).TotalDays));
        var title = "Your package is ending soon";
        var content = $"Your {packageName} package ends in {daysRemaining} day(s), on {endDate:dd MMM yyyy}. Renew it to keep its benefits active.";

        if (!alreadyNotified)
        {
            await notifications.NotifyAsync(new NotificationMessage(
                userId,
                NotificationType.SubscriptionExpiring,
                title,
                content,
                ReferenceType: "Subscription",
                ReferenceId: subscriptionId), cancellationToken);
        }

        var emailExists = await context.EmailOutboxes.AnyAsync(email =>
            email.ReferenceId == subscriptionId && email.EmailType == emailType,
            cancellationToken);
        if (emailExists || string.IsNullOrWhiteSpace(recipientEmail)) return;

        context.EmailOutboxes.Add(new EmailOutbox
        {
            Id = Guid.NewGuid(),
            RecipientEmail = recipientEmail,
            Subject = title,
            HtmlBody = $"<p>{content}</p><p>Please sign in to Smart Night Market to review or renew your package.</p>",
            EmailType = emailType,
            ReferenceId = subscriptionId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }
}

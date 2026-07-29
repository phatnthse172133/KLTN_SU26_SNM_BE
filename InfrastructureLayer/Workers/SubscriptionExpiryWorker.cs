using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
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
        var now = DateTime.UtcNow;

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

        if (expiredBooth > 0 || expiredMarket > 0)
        {
            _logger.LogInformation("Expired {BoothCount} booth subscriptions and {MarketCount} market subscriptions.", expiredBooth, expiredMarket);
        }
    }
}

using ApplicationLayer.AI.V2.Configuration;
using ApplicationLayer.AI.V2.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Workers;

public sealed class AiMealPlanSessionCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MealPlanV2Options> options,
    TimeProvider timeProvider,
    ILogger<AiMealPlanSessionCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IMealPlanV2Repository>();
                var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-Math.Clamp(options.Value.ReadRetentionDays, 1, 365));
                var deleted = await repository.DeleteExpiredBatchAsync(cutoff,
                    Math.Clamp(options.Value.CleanupBatchSize, 1, 500), stoppingToken);
                if (deleted > 0) logger.LogInformation("Deleted {Count} retained AI meal-plan sessions.", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { logger.LogError(exception, "AI meal-plan cleanup failed; it will retry next cycle."); }
        }
    }
}

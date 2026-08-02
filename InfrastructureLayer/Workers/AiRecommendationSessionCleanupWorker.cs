using ApplicationLayer.AI.V2.Configuration;
using ApplicationLayer.AI.V2.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Workers;

public sealed class AiRecommendationSessionCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RecommendationV2Options> options,
    TimeProvider timeProvider,
    ILogger<AiRecommendationSessionCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private readonly RecommendationV2Options _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAiRecommendationSessionRepository>();
                var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-Math.Clamp(_options.RetentionDays, 1, 365));
                var deleted = await repository.DeleteExpiredBatchAsync(
                    cutoff,
                    Math.Clamp(_options.CleanupBatchSize, 1, 1000),
                    stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("AI V2 retention cleanup removed {DeletedCount} expired sessions.", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "AI V2 retention cleanup failed.");
            }

            await Task.Delay(Interval, timeProvider, stoppingToken);
        }
    }
}

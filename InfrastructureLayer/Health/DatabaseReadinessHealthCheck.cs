using InfrastructureLayer.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InfrastructureLayer.Health;

public sealed class DatabaseReadinessHealthCheck : IHealthCheck
{
    private readonly SNMDbContext _dbContext;

    public DatabaseReadinessHealthCheck(SNMDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL readiness check failed.", exception);
        }
    }
}

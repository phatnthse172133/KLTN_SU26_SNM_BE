using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using InfrastructureLayer.Health;

namespace TestingLayer;

public sealed class DatabaseReadinessHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenDatabaseCanConnect()
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase($"readiness-{Guid.NewGuid():N}")
            .Options;
        await using var context = new SNMDbContext(options);
        var check = new DatabaseReadinessHealthCheck(context);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}

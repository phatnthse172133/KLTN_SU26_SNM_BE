using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Backfill;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit.Abstractions;

namespace TestingLayer;

public sealed class PostgresAiV2ProductionCloneDryRunTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task FullLegacyClone_DryRunCoversEveryRelation_WithoutChangingSource()
    {
        var sourceConnection = Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES_CLONE_SOURCE");
        if (string.IsNullOrWhiteSpace(sourceConnection)) return;

        var sourceBuilder = new NpgsqlConnectionStringBuilder(sourceConnection);
        var sourceDatabase = sourceBuilder.Database;
        Assert.False(string.IsNullOrWhiteSpace(sourceDatabase));

        var cloneDatabase = $"snm_aiv2_gate0_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(sourceConnection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();

        var quotedSource = new NpgsqlCommandBuilder().QuoteIdentifier(sourceDatabase);
        var quotedClone = new NpgsqlCommandBuilder().QuoteIdentifier(cloneDatabase);
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {quotedClone} WITH TEMPLATE {quotedSource}", admin))
            await create.ExecuteNonQueryAsync();

        try
        {
            var cloneBuilder = new NpgsqlConnectionStringBuilder(sourceConnection) { Database = cloneDatabase };
            var options = new DbContextOptionsBuilder<SNMDbContext>()
                .UseNpgsql(cloneBuilder.ConnectionString)
                .Options;

            await using var db = new SNMDbContext(options);
            await db.Database.MigrateAsync();
            var report = await new AiV2FoodMetadataBackfillService(db)
                .RunAsync(new(AiV2BackfillMode.DryRun, 200));

            output.WriteLine(report.ToJson());
            Assert.Equal(136, report.TotalLegacyTags);
            Assert.Equal(report.FoodItemTagBefore + report.CustomerPreferenceBefore, report.CoveredLegacyRelations);
            Assert.Equal(0, report.UnmappedRelations);
            Assert.Equal(12, report.SoupResolutions.Count);
            Assert.Equal(2, report.SoupResolutions.Count(row => row.ResolutionStatus == InfrastructureLayer.Data.Migrations.SoupResolutionStatus.MANUAL_REVIEW));
            Assert.False(report.LegacyDataChanged);
            Assert.False(report.TransactionCommitted);

            var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var reportDirectory = Path.Combine(repositoryRoot, "docs", "ai-v2");
            Directory.CreateDirectory(reportDirectory);
            await File.WriteAllTextAsync(Path.Combine(reportDirectory, "GATE0_PRODUCTION_LIKE_DRY_RUN.json"), report.ToJson());
            await File.WriteAllTextAsync(Path.Combine(reportDirectory, "GATE0_PRODUCTION_LIKE_DRY_RUN.md"), report.ToMarkdown());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {quotedClone} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}

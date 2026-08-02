using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Backfill;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var values = Args.Parse(args);
var connectionString = Environment.GetEnvironmentVariable(values.ConnectionEnvironmentVariable);
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException($"Connection environment variable '{values.ConnectionEnvironmentVariable}' is missing.");

var connection = new NpgsqlConnectionStringBuilder(connectionString);
if (!string.Equals(connection.Database, values.AllowedDatabase, StringComparison.Ordinal))
    throw new InvalidOperationException("Database name does not match --allow-database. Backfill was not started.");
if (values.Mode == AiV2BackfillMode.Execute && !values.ConfirmExecute)
    throw new InvalidOperationException("Execute mode requires --confirm-execute.");

var options = new DbContextOptionsBuilder<SNMDbContext>().UseNpgsql(connection.ConnectionString).Options;
await using var db = new SNMDbContext(options);
var pending = await db.Database.GetPendingMigrationsAsync();
if (pending.Any())
    throw new InvalidOperationException($"Database has {pending.Count()} pending migration(s). Apply migrations explicitly before backfill.");

var report = await new AiV2FoodMetadataBackfillService(db).RunAsync(new(values.Mode, values.BatchSize));
var json = report.ToJson();
var markdown = report.ToMarkdown();
Console.WriteLine(json);
Console.Error.WriteLine(markdown);
if (values.JsonOutput is not null) await File.WriteAllTextAsync(values.JsonOutput, json);
if (values.MarkdownOutput is not null) await File.WriteAllTextAsync(values.MarkdownOutput, markdown);

internal sealed record Args(
    AiV2BackfillMode Mode,
    int BatchSize,
    string ConnectionEnvironmentVariable,
    string AllowedDatabase,
    bool ConfirmExecute,
    string? JsonOutput,
    string? MarkdownOutput)
{
    public static Args Parse(string[] args)
    {
        var dry = args.Contains("--dry-run", StringComparer.Ordinal);
        var execute = args.Contains("--execute", StringComparer.Ordinal);
        if (dry == execute) throw new ArgumentException("Specify exactly one of --dry-run or --execute.");
        var batch = Int("--batch-size", 200);
        var env = String("--connection-env") ?? "SNM_AI_V2_BACKFILL_CONNECTION";
        var database = String("--allow-database") ?? throw new ArgumentException("--allow-database is required.");
        return new(dry ? AiV2BackfillMode.DryRun : AiV2BackfillMode.Execute, batch, env, database,
            args.Contains("--confirm-execute", StringComparer.Ordinal), String("--json-out"), String("--markdown-out"));

        string? String(string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        int Int(string name, int fallback) => int.TryParse(String(name), out var value) ? value : fallback;
    }
}

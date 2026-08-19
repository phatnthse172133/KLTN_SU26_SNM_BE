using System.Diagnostics;
using ApplicationLayer.Services.Assistant;
using DomainLayer.Common;
using DomainLayer.Enums;
using InfrastructureLayer.Cores.Assistant;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace TestingLayer;

public sealed class AssistantBatchBenchmarkTests
{
    private const string DefaultPrompt = "Món ngon gần đây, không quá cay, khoảng 80k";
    private readonly ITestOutputHelper _output;

    public AssistantBatchBenchmarkTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [Trait("Category", "AssistantBenchmark")]
    public async Task BenchmarkBatchSize(int batchSize)
    {
        var context = await CreateBenchmarkContextAsync();
        if (context is null) return;

        await using (context)
        {
            var eligible = await LoadEligibleAsync(context.Db);
            if (eligible.Count == 0) return;

            var intent = ParsedIntent();
            var runs = 2;
            for (var run = 1; run <= runs; run++)
            {
                var matcher = CreateMatcher(context.ApiKey, batchSize, concurrency: 3);
                var watch = Stopwatch.StartNew();
                var result = await matcher.ScoreAsync(DefaultPrompt, intent, eligible, CancellationToken.None);
                watch.Stop();
                LogTurn($"batchSize={batchSize} run={run}", watch.ElapsedMilliseconds, eligible.Count, result);
                Assert.False(HasScoreCountMismatch(result, eligible.Count));
            }
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [Trait("Category", "AssistantBenchmark")]
    public async Task BenchmarkConcurrency(int concurrency)
    {
        var context = await CreateBenchmarkContextAsync();
        if (context is null) return;

        await using (context)
        {
            var eligible = await LoadEligibleAsync(context.Db);
            if (eligible.Count == 0) return;

            var batchSize = int.TryParse(Environment.GetEnvironmentVariable("SNM_BENCHMARK_BATCH_SIZE"), out var configured)
                ? configured
                : 8;
            var intent = ParsedIntent();
            var runs = 2;
            for (var run = 1; run <= runs; run++)
            {
                var matcher = CreateMatcher(context.ApiKey, batchSize, concurrency);
                var watch = Stopwatch.StartNew();
                var result = await matcher.ScoreAsync(DefaultPrompt, intent, eligible, CancellationToken.None);
                watch.Stop();
                LogTurn($"concurrency={concurrency} batchSize={batchSize} run={run}", watch.ElapsedMilliseconds, eligible.Count, result);
                Assert.False(HasScoreCountMismatch(result, eligible.Count));
            }
        }
    }

    private void LogTurn(string label, long totalMs, int eligibleCount, AssistantSemanticMatchResult result)
    {
        _output.WriteLine(
            $"{label} totalMs={totalMs} eligible={eligibleCount} batches={result.BatchCount} semanticMs={result.SemanticTotalMs} retries={result.SemanticRetryCount} idsSent={result.IdsSent.Count} idsEvaluated={result.Scores.Count}");
        foreach (var batch in result.BatchDiagnostics)
        {
            _output.WriteLine(
                $"  batch[{batch.BatchIndex}] size={batch.BatchSize} ms={batch.DurationMs} retries={batch.RetryCount} sent={batch.IdsSent} returned={batch.IdsReturned} finish={batch.FinishReason}");
            Assert.Equal(batch.IdsSent, batch.IdsReturned);
        }
    }

    private static bool HasScoreCountMismatch(AssistantSemanticMatchResult result, int eligibleCount)
        => result.Scores.Count != eligibleCount || result.IdsSent.Count != eligibleCount;

    private static AssistantSemanticMatcher CreateMatcher(string apiKey, int batchSize, int concurrency)
    {
        var assistantOptions = Options.Create(new AssistantOptions
        {
            CandidateBatchSize = batchSize,
            SemanticBatchMaxConcurrency = concurrency,
            SemanticBatchRetryCount = 1
        });
        var openAiOptions = Options.Create(new OpenAiOptions
        {
            Enabled = true,
            ApiKey = apiKey,
            MaxInputCharacters = 32000,
            MaxOutputTokensSemantic = 2500,
            TimeoutSeconds = 120
        });
        var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var llm = new OpenAiLanguageModelClient(httpClient, openAiOptions, NullLogger<OpenAiLanguageModelClient>.Instance);
        return new AssistantSemanticMatcher(llm, assistantOptions, openAiOptions, NullLogger<AssistantSemanticMatcher>.Instance);
    }

    private static Task<BenchmarkContext?> CreateBenchmarkContextAsync()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var connectionString = Environment.GetEnvironmentVariable("SNM_APPLIED_POSTGRES")
            ?? Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(connectionString))
            return Task.FromResult<BenchmarkContext?>(null);

        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var db = new SNMDbContext(options);
        return Task.FromResult<BenchmarkContext?>(new BenchmarkContext(db, apiKey));
    }

    private static async Task<IReadOnlyList<AssistantEligibleFood>> LoadEligibleAsync(SNMDbContext db)
    {
        var repository = new AssistantFoodQueryRepository(db);
        var criteria = new AssistantFoodQueryCriteria
        {
            Latitude = 10.7769,
            Longitude = 106.7009,
            MaxDistanceMeters = 5000,
            UtcNow = DateTime.UtcNow
        };
        var result = await repository.GetEligibleFoodsAsync(criteria, CancellationToken.None);
        return result.Foods;
    }

    private static ParsedAssistantIntent ParsedIntent()
        => new()
        {
            Intent = AssistantIntentKind.FOOD_RECOMMENDATION,
            NeedsLocation = false,
            BudgetMax = 80_000m,
            HardConstraints = new AssistantHardConstraints(),
            StructuredPreferences = new AssistantStructuredPreferences(),
            SemanticPreferences = ["ngon", "không quá cay"],
            SemanticAvoidances = []
        };

    private sealed class BenchmarkContext(SNMDbContext db, string apiKey) : IAsyncDisposable
    {
        public SNMDbContext Db { get; } = db;
        public string ApiKey { get; } = apiKey;

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

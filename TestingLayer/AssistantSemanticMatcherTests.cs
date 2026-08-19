using System.Text.Json;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Assistant;
using DomainLayer.Common;
using DomainLayer.Entities;
using Microsoft.Extensions.Options;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class AssistantSemanticMatcherTests
{
    [Fact]
    public void GetBatchSizes_NinetySevenWithThirty_YieldsFourBatches()
    {
        var sizes = AssistantSemanticMatcher.GetBatchSizes(97, 30);
        Assert.Equal([30, 30, 30, 7], sizes);
        Assert.Equal(97, sizes.Sum());
    }

    [Fact]
    public void GetBatchSizes_FourteenWithThirty_IsSinglePartialBatch()
        => Assert.Equal([14], AssistantSemanticMatcher.GetBatchSizes(14, 30));

    [Fact]
    public void GetBatchSizes_FortySevenWithThirty_SplitsRemainder()
        => Assert.Equal([30, 17], AssistantSemanticMatcher.GetBatchSizes(47, 30));

    [Fact]
    public async Task Score_BatchesNinetySevenFoodsIntoFourCalls_AndEvaluatesEveryId()
    {
        var llm = new Mock<ILanguageModelClient>();
        var batchSizes = new List<int>();
        var sentIds = new List<Guid>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()))
            .Returns((string _, string user, int _, CancellationToken _, LanguageModelJsonSchemaOptions? _) =>
            {
                var ids = CandidateIds(user);
                batchSizes.Add(ids.Count);
                sentIds.AddRange(ids);
                return Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(ids));
            });
        var matcher = Create(llm.Object, 30);
        var eligible = Enumerable.Range(0, 97).Select(index => Eligible($"Món {index}")).ToArray();
        var expectedIds = eligible.Select(item => item.FoodItem.Id).ToArray();

        var result = await matcher.ScoreAsync("gợi ý", Intent(), eligible, CancellationToken.None);

        Assert.Equal([30, 30, 30, 7], batchSizes);
        Assert.Equal(4, result.BatchCount);
        Assert.Equal(97, result.IdsSent.Count);
        Assert.Equal(97, result.Scores.Count);
        Assert.Equal(expectedIds, result.IdsSent);
        Assert.Equal(expectedIds.ToHashSet(), sentIds.ToHashSet());
        Assert.Equal(expectedIds.ToHashSet(), result.Scores.Keys.ToHashSet());
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()), Times.Exactly(4));
    }

    [Fact]
    public async Task Score_UsesConfiguredConcurrencyBound()
    {
        var llm = new Mock<ILanguageModelClient>();
        var current = 0;
        var peak = 0;
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()))
            .Returns(async (string _, string user, int _, CancellationToken _, LanguageModelJsonSchemaOptions? _) =>
            {
                var running = Interlocked.Increment(ref current);
                SpinWait.SpinUntil(() =>
                {
                    var snapshot = peak;
                    return running <= snapshot || Interlocked.CompareExchange(ref peak, running, snapshot) == snapshot;
                });
                await Task.Delay(40);
                Interlocked.Decrement(ref current);
                return (LanguageModelJsonCompletion)ScoresJson(CandidateIds(user));
            });
        var matcher = Create(llm.Object, batchSize: 10, concurrency: 3);
        var eligible = Enumerable.Range(0, 40).Select(index => Eligible($"Món {index}")).ToArray();

        await matcher.ScoreAsync("gợi ý", Intent(), eligible, CancellationToken.None);

        Assert.InRange(peak, 1, 3);
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()), Times.Exactly(4));
    }

    [Fact]
    public async Task Score_BatchEight_ReturnsEightPositionalScores()
    {
        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()))
            .Returns((string _, string user, int _, CancellationToken _, LanguageModelJsonSchemaOptions? _) =>
                Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(CandidateIds(user), 0.5)));
        var matcher = Create(llm.Object, batchSize: 8);
        var eligible = Enumerable.Range(0, 8).Select(index => Eligible($"Món {index}")).ToArray();

        var result = await matcher.ScoreAsync("gợi ý", Intent(), eligible, CancellationToken.None);

        Assert.Equal(8, result.Scores.Count);
        Assert.Equal(1, result.BatchCount);
        Assert.Equal(eligible.Select(item => item.FoodItem.Id), result.IdsSent);
    }

    [Fact]
    public async Task Score_BatchSeven_ReturnsSevenPositionalScores()
    {
        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()))
            .Returns((string _, string user, int _, CancellationToken _, LanguageModelJsonSchemaOptions? _) =>
                Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(CandidateIds(user), 0.6)));
        var matcher = Create(llm.Object, batchSize: 8);
        var eligible = Enumerable.Range(0, 7).Select(index => Eligible($"Món {index}")).ToArray();

        var result = await matcher.ScoreAsync("gợi ý", Intent(), eligible, CancellationToken.None);

        Assert.Equal(7, result.Scores.Count);
        Assert.Equal(1, result.BatchCount);
    }

    [Fact]
    public void ParseBatch_IndexMapping_MapsScoresToOrderedCandidates()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var raw = """{"scores":[0.9,0.4,0.7]}""";

        var scores = AssistantSemanticMatcher.ParseBatch(raw, [first, second, third]);

        Assert.Equal(3, scores.Count);
        Assert.Equal(first, scores[0].FoodItemId);
        Assert.Equal(second, scores[1].FoodItemId);
        Assert.Equal(third, scores[2].FoodItemId);
        Assert.Equal(0.9d, scores[0].SemanticCompatibility);
        Assert.Equal(0.4d, scores[1].SemanticCompatibility);
        Assert.Equal(0.7d, scores[2].SemanticCompatibility);
    }

    [Fact]
    public async Task Score_ScoreCountMismatch_RetriesOnceThenPasses()
    {
        var llm = new Mock<ILanguageModelClient>();
        var calls = 0;
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()))
            .Returns((string _, string user, int _, CancellationToken _, LanguageModelJsonSchemaOptions? _) =>
            {
                calls++;
                var ids = CandidateIds(user);
                if (calls == 1)
                    return Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(ids.Take(ids.Count - 1).ToArray(), 0.5));
                return Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(ids, 0.5));
            });
        var matcher = Create(llm.Object, 8, semanticBatchRetryCount: 1);
        var eligible = Enumerable.Range(0, 8).Select(index => Eligible($"Món {index}")).ToArray();

        var result = await matcher.ScoreAsync("gợi ý", Intent(), eligible, CancellationToken.None);

        Assert.Equal(8, result.Scores.Count);
        Assert.Equal(2, calls);
        Assert.Equal(1, result.SemanticRetryCount);
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Score_ScoreCountMismatchAfterRetry_Throws503()
    {
        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()))
            .Returns((string _, string user, int _, CancellationToken _, LanguageModelJsonSchemaOptions? _) =>
            {
                var ids = CandidateIds(user).Take(7).ToArray();
                return Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(ids, 0.5));
            });
        var matcher = Create(llm.Object, 8, semanticBatchRetryCount: 1);
        var eligible = Enumerable.Range(0, 8).Select(index => Eligible($"Món {index}")).ToArray();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            matcher.ScoreAsync("gợi ý", Intent(), eligible, CancellationToken.None));

        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.SemanticScoreCountMismatch, AssistantProviderFailure.Reason(exception));
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Score_MultiBatchRetryOnlyFailedBatch_CompletedBatchCalledOnce()
    {
        var llm = new Mock<ILanguageModelClient>();
        var batchCalls = new Dictionary<int, int>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()))
            .Returns((string _, string user, int _, CancellationToken _, LanguageModelJsonSchemaOptions? _) =>
            {
                var ids = CandidateIds(user);
                var batchIndex = ids.Count == 10 ? 0 : 1;
                batchCalls.TryGetValue(batchIndex, out var count);
                batchCalls[batchIndex] = count + 1;
                if (batchIndex == 1 && count == 0)
                    ids = ids.Take(ids.Count - 1).ToArray();
                return Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(ids, 0.5));
            });
        var matcher = Create(llm.Object, batchSize: 10, semanticBatchRetryCount: 1);
        var eligible = Enumerable.Range(0, 15).Select(index => Eligible($"Món {index}")).ToArray();

        var result = await matcher.ScoreAsync("gợi ý", Intent(), eligible, CancellationToken.None);

        Assert.Equal(15, result.Scores.Count);
        Assert.Equal(1, batchCalls[0]);
        Assert.Equal(2, batchCalls[1]);
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Score_ScoreCountMismatch_DoesNotInventDefaultScores()
    {
        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()))
            .Returns((string _, string user, int _, CancellationToken _, LanguageModelJsonSchemaOptions? _) =>
                Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(CandidateIds(user).Take(1).ToArray(), 0.5)));
        var matcher = Create(llm.Object, 8, semanticBatchRetryCount: 0);
        var eligible = new[] { Eligible("A"), Eligible("B") };

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            matcher.ScoreAsync("gợi ý", Intent(), eligible, CancellationToken.None));

        Assert.Equal(AssistantErrors.SemanticScoreCountMismatch, AssistantProviderFailure.Reason(exception));
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()), Times.Once);
    }

    [Fact]
    public void ParseBatch_ScoreCountMismatch_IncludesSemanticDiagnostics()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var raw = """{"scores":[0.4]}""";
        var completion = new LanguageModelJsonCompletion
        {
            Content = raw,
            FinishReason = "stop",
            ConfiguredMaxOutputTokens = 2500,
            OutputTokenCount = 12,
            ResponseCharacterCount = raw.Length
        };

        var exception = Assert.Throws<AppException>(() =>
            AssistantSemanticMatcher.ParseBatch(completion, ids));

        Assert.Equal(AssistantErrors.SemanticScoreCountMismatch, AssistantProviderFailure.Reason(exception));
        Assert.Equal(AssistantLlmStages.Semantic, AssistantProviderFailure.Stage(exception));
        Assert.Equal("stop", AssistantProviderFailure.FinishReason(exception));
        Assert.Equal(2500, AssistantProviderFailure.ConfiguredMaxOutputTokens(exception));
    }

    [Fact]
    public void ParseBatch_ScoreCountMismatch_Throws503()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var raw = """{"scores":[0.4]}""";

        var exception = Assert.Throws<AppException>(() =>
            AssistantSemanticMatcher.ParseBatch(raw, ids));

        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.SemanticScoreCountMismatch, AssistantProviderFailure.Reason(exception));
    }

    [Fact]
    public void ParseBatch_InvalidJson_Throws503()
    {
        var exception = Assert.Throws<AppException>(() => AssistantSemanticMatcher.ParseBatch("{", []));
        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.InvalidJson, AssistantProviderFailure.Reason(exception));
        Assert.Equal(AssistantJsonParseClassifier.OutputTruncated, AssistantProviderFailure.ParseFailureCategory(exception));
        Assert.Equal(AssistantLlmStages.Semantic, AssistantProviderFailure.Stage(exception));
    }

    [Fact]
    public void ParseBatch_TruncatedWithFinishLength_HasTokenDiagnostics()
    {
        var truncated = """{"scores":[0.9,0.8""";
        var completion = new LanguageModelJsonCompletion
        {
            Content = truncated,
            Model = "gpt-4o-mini",
            FinishReason = "length",
            ConfiguredMaxOutputTokens = 400,
            OutputTokenCount = 400,
            ResponseCharacterCount = truncated.Length
        };

        var exception = Assert.Throws<AppException>(() =>
            AssistantSemanticMatcher.ParseBatch(completion, [Guid.NewGuid(), Guid.NewGuid()]));

        Assert.Equal(AssistantErrors.InvalidJson, AssistantProviderFailure.Reason(exception));
        Assert.Equal(AssistantJsonParseClassifier.OutputTruncated, AssistantProviderFailure.ParseFailureCategory(exception));
        Assert.Equal(400, AssistantProviderFailure.ConfiguredMaxOutputTokens(exception));
        Assert.Equal(400, AssistantProviderFailure.OutputTokenCount(exception));
        Assert.Equal(truncated.Length, AssistantProviderFailure.ResponseCharacterCount(exception));
        Assert.Equal("length", AssistantProviderFailure.FinishReason(exception));
    }

    [Fact]
    public void ParseBatch_SchemaMismatch_ThrowsInvalidJson()
    {
        var exception = Assert.Throws<AppException>(() =>
            AssistantSemanticMatcher.ParseBatch("""{"scores":{"foodItemId":"x"}}""", [Guid.NewGuid()]));

        Assert.Equal(AssistantErrors.InvalidJson, AssistantProviderFailure.Reason(exception));
        Assert.Equal(AssistantJsonParseClassifier.SchemaMismatch, AssistantProviderFailure.ParseFailureCategory(exception));
    }

    [Fact]
    public void ParseBatch_Malformed_DoesNotInventScores()
    {
        var exception = Assert.Throws<AppException>(() =>
            AssistantSemanticMatcher.ParseBatch("not-json{{{", [Guid.NewGuid()]));

        Assert.Equal(AssistantErrors.InvalidJson, AssistantProviderFailure.Reason(exception));
        Assert.Equal(AssistantJsonParseClassifier.MalformedJson, AssistantProviderFailure.ParseFailureCategory(exception));
    }

    [Fact]
    public async Task Score_RequestsSemanticTokenBudgetAndJsonSchema()
    {
        var llm = new Mock<ILanguageModelClient>();
        LanguageModelJsonSchemaOptions? capturedSchema = null;
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()))
            .Callback<string, string, int, CancellationToken, LanguageModelJsonSchemaOptions?>((_, _, _, _, schema) => capturedSchema = schema)
            .Returns((string _, string user, int _, CancellationToken _, LanguageModelJsonSchemaOptions? _) =>
                Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(CandidateIds(user), 0.5)));
        var matcher = Create(llm.Object, 8);

        await matcher.ScoreAsync("gợi ý", Intent(), [Eligible("A")], CancellationToken.None);

        llm.Verify(client => client.CompleteJsonAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            2500,
            It.IsAny<CancellationToken>(),
            It.IsNotNull<LanguageModelJsonSchemaOptions>()), Times.Once);
        Assert.NotNull(capturedSchema);
        Assert.Contains("\"minItems\":1", capturedSchema!.SchemaJson, StringComparison.Ordinal);
        Assert.Contains("\"maxItems\":1", capturedSchema.SchemaJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Score_AnyBatchTimeout_FailsEntireTurn()
    {
        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()))
            .ThrowsAsync(new TaskCanceledException());
        var matcher = Create(llm.Object, 8);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            matcher.ScoreAsync("gợi ý", Intent(), new[] { Eligible("A"), Eligible("B") }, CancellationToken.None));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.Timeout, AssistantProviderFailure.Reason(exception));
    }

    [Fact]
    public async Task Score_EmptyEligible_DoesNotCallProvider()
    {
        var llm = new Mock<ILanguageModelClient>();
        var matcher = Create(llm.Object, 8);

        var result = await matcher.ScoreAsync("gợi ý", Intent(), [], CancellationToken.None);

        Assert.Empty(result.Scores);
        Assert.Equal(0, result.BatchCount);
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()), Times.Never);
    }

    [Fact]
    public void ProjectFood_IncludesSalesRatingAndPriceFacts()
    {
        var eligible = Eligible("Bún bò");
        eligible.FoodItem.AverageRating = 4.2m;
        eligible.FoodItem.ReviewCount = 6;
        eligible.FoodItem.IsFeatured = true;
        var matcher = Create(new Mock<ILanguageModelClient>().Object, 8);
        var compact = matcher.Project(
            new AssistantEligibleFood
            {
                FoodItem = eligible.FoodItem,
                EffectivePrice = 45_000m,
                SoldToday = 4,
                OrderCount = 11,
                HasActivePromotion = true
            },
            new CompactCandidateProjectionOptions());

        Assert.Equal(45_000m, compact.EffectivePrice);
        Assert.Equal(4.2m, compact.AverageRating);
        Assert.Equal(6, compact.ReviewCount);
        Assert.True(compact.HasActivePromotion);
        Assert.True(compact.IsFeatured);
        Assert.Equal("Bún bò", compact.Name);
    }

    private static AssistantSemanticMatcher Create(ILanguageModelClient llm, int batchSize, int concurrency = 4, int semanticBatchRetryCount = 1)
        => new(
            llm,
            Options.Create(new AssistantOptions
            {
                CandidateBatchSize = batchSize,
                SemanticBatchMaxConcurrency = concurrency,
                SemanticBatchRetryCount = semanticBatchRetryCount
            }),
            Options.Create(new OpenAiOptions { ApiKey = "test", MaxOutputTokensSemantic = 2500, UseJsonSchemaStrict = true }));

    private static ParsedAssistantIntent Intent()
        => new() { Intent = DomainLayer.Enums.AssistantIntentKind.FOOD_RECOMMENDATION };

    private static IReadOnlyList<Guid> CandidateIds(string user)
    {
        using var document = JsonDocument.Parse(user);
        return document.RootElement.GetProperty("candidates")
            .EnumerateArray()
            .Select(item => item.GetProperty("foodItemId").GetGuid())
            .ToArray();
    }

    private static LanguageModelJsonCompletion ScoresJson(IReadOnlyList<Guid> ids, double score = 0.5)
        => LanguageModelJsonCompletion.FromContent(JsonSerializer.Serialize(new
        {
            scores = ids.Select(_ => score).ToArray()
        }));

    private static AssistantEligibleFood Eligible(string name)
    {
        var market = new NightMarket { Id = Guid.NewGuid(), Name = "Chợ Test", Status = NightMarketStatus.Active };
        var booth = new Booth { Id = Guid.NewGuid(), BoothName = "Quầy", NightMarket = market, NightMarketId = market.Id, Status = BoothStatus.Active };
        var category = new FoodCategory { Id = Guid.NewGuid(), Name = "Món", Code = "MAIN" };
        var food = new FoodItem
        {
            Id = Guid.NewGuid(),
            Name = name,
            Booth = booth,
            BoothId = booth.Id,
            Category = category,
            Ingredients = [],
            Allergens = [],
            DietaryAttributes = [],
            TasteProfiles = [],
            PreparationMethods = [],
            Courses = []
        };
        return new AssistantEligibleFood { FoodItem = food, EffectivePrice = 35_000m };
    }
}

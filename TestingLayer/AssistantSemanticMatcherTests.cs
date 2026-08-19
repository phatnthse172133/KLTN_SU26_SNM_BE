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
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string user, int _, CancellationToken _) =>
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
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public async Task Score_UsesConfiguredConcurrencyBound()
    {
        var llm = new Mock<ILanguageModelClient>();
        var current = 0;
        var peak = 0;
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string user, int _, CancellationToken _) =>
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
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public void ParseBatch_ExtraIds_Throws503()
    {
        var allowed = Guid.NewGuid();
        var extra = Guid.NewGuid();
        var raw = $$"""
            {
              "scores": [
                { "foodItemId": "{{allowed}}", "semanticCompatibility": 0.9, "reasons": ["khớp vị cay"] },
                { "foodItemId": "{{extra}}", "semanticCompatibility": 0.99, "reasons": ["id bịa"] }
              ]
            }
            """;

        var exception = Assert.Throws<AppException>(() =>
            AssistantSemanticMatcher.ParseBatch(raw, new HashSet<Guid> { allowed }));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.HallucinatedId, AssistantProviderFailure.Reason(exception));
    }

    [Fact]
    public void ParseBatch_DuplicateReturnedId_Throws503()
    {
        var id = Guid.NewGuid();
        var raw = $$"""
            {
              "scores": [
                { "foodItemId": "{{id}}", "semanticCompatibility": 0.9, "reasons": ["a"] },
                { "foodItemId": "{{id}}", "semanticCompatibility": 0.5, "reasons": ["b"] }
              ]
            }
            """;

        var exception = Assert.Throws<AppException>(() =>
            AssistantSemanticMatcher.ParseBatch(raw, new HashSet<Guid> { id }));

        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.DuplicateId, AssistantProviderFailure.Reason(exception));
    }

    [Fact]
    public async Task Score_IncompleteBatch_RetriesOnceThenPasses()
    {
        var llm = new Mock<ILanguageModelClient>();
        var calls = 0;
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string user, int _, CancellationToken _) =>
            {
                calls++;
                var ids = CandidateIds(user);
                if (calls == 1)
                    ids = ids.Take(ids.Count - 1).ToArray();
                return Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(ids));
            });
        var matcher = Create(llm.Object, 30, semanticBatchRetryCount: 1);
        var eligible = new[] { Eligible("A"), Eligible("B") };

        var result = await matcher.ScoreAsync("gợi ý", Intent(), eligible, CancellationToken.None);

        Assert.Equal(2, result.Scores.Count);
        Assert.Equal(2, calls);
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Score_IncompleteBatchAfterRetry_Throws503()
    {
        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string user, int _, CancellationToken _) =>
            {
                var ids = CandidateIds(user).Take(1).ToArray();
                return Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(ids));
            });
        var matcher = Create(llm.Object, 30, semanticBatchRetryCount: 1);
        var eligible = new[] { Eligible("A"), Eligible("B") };

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            matcher.ScoreAsync("gợi ý", Intent(), eligible, CancellationToken.None));

        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.IncompleteIdSet, AssistantProviderFailure.Reason(exception));
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Score_MultiBatchRetryOnlyFailedBatch_CompletedBatchCalledOnce()
    {
        var llm = new Mock<ILanguageModelClient>();
        var batchCalls = new Dictionary<int, int>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string user, int _, CancellationToken _) =>
            {
                var ids = CandidateIds(user);
                var batchIndex = ids.Count == 10 ? 0 : 1;
                batchCalls.TryGetValue(batchIndex, out var count);
                batchCalls[batchIndex] = count + 1;
                if (batchIndex == 1 && count == 0)
                    ids = ids.Take(ids.Count - 1).ToArray();
                return Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(ids));
            });
        var matcher = Create(llm.Object, batchSize: 10, semanticBatchRetryCount: 1);
        var eligible = Enumerable.Range(0, 15).Select(index => Eligible($"Món {index}")).ToArray();

        var result = await matcher.ScoreAsync("gợi ý", Intent(), eligible, CancellationToken.None);

        Assert.Equal(15, result.Scores.Count);
        Assert.Equal(1, batchCalls[0]);
        Assert.Equal(2, batchCalls[1]);
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Score_IncompleteBatch_DoesNotInventDefaultScores()
    {
        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string user, int _, CancellationToken _) =>
                Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(CandidateIds(user).Take(1).ToArray())));
        var matcher = Create(llm.Object, 30, semanticBatchRetryCount: 0);
        var eligible = new[] { Eligible("A"), Eligible("B") };

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            matcher.ScoreAsync("gợi ý", Intent(), eligible, CancellationToken.None));

        Assert.Equal(AssistantErrors.IncompleteIdSet, AssistantProviderFailure.Reason(exception));
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ParseBatch_MissingAllowedId_IncludesSemanticDiagnostics()
    {
        var allowed = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var raw = $$"""
            {
              "scores": [
                { "foodItemId": "{{allowed}}", "semanticCompatibility": 0.4, "reasons": ["ok"] }
              ]
            }
            """;
        var completion = new LanguageModelJsonCompletion
        {
            Content = raw,
            FinishReason = "stop",
            ConfiguredMaxOutputTokens = 2500,
            OutputTokenCount = 120,
            ResponseCharacterCount = raw.Length
        };

        var exception = Assert.Throws<AppException>(() =>
            AssistantSemanticMatcher.ParseBatch(completion, new HashSet<Guid> { allowed, missing }));

        Assert.Equal(AssistantErrors.IncompleteIdSet, AssistantProviderFailure.Reason(exception));
        Assert.Equal(AssistantLlmStages.Semantic, AssistantProviderFailure.Stage(exception));
        Assert.Equal("stop", AssistantProviderFailure.FinishReason(exception));
        Assert.Equal(2500, AssistantProviderFailure.ConfiguredMaxOutputTokens(exception));
    }

    [Fact]
    public void ParseBatch_MissingAllowedId_Throws503()
    {
        var allowed = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var raw = $$"""
            {
              "scores": [
                { "foodItemId": "{{allowed}}", "semanticCompatibility": 0.4, "reasons": ["ok"] }
              ]
            }
            """;

        var exception = Assert.Throws<AppException>(() =>
            AssistantSemanticMatcher.ParseBatch(raw, new HashSet<Guid> { allowed, missing }));

        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.IncompleteIdSet, AssistantProviderFailure.Reason(exception));
    }

    [Fact]
    public void ParseBatch_PreservesUnknownCaloriesFacet()
    {
        var id = Guid.NewGuid();
        var raw = $$"""
            {
              "scores": [
                {
                  "foodItemId": "{{id}}",
                  "semanticCompatibility": 0.6,
                  "confidence": 0.4,
                  "reasons": ["MISSING_DB_FIELD"],
                  "unknownDataFacets": ["calories", "oiliness"]
                }
              ]
            }
            """;

        var scores = AssistantSemanticMatcher.ParseBatch(raw, new HashSet<Guid> { id });

        Assert.Contains("calories", scores[0].UnknownDataFacets);
        Assert.Contains("oiliness", scores[0].UnknownDataFacets);
        Assert.DoesNotContain(scores[0].Reasons, reason => reason.Contains("350 kcal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParseBatch_InvalidJson_Throws503()
    {
        var exception = Assert.Throws<AppException>(() => AssistantSemanticMatcher.ParseBatch("{", new HashSet<Guid>()));
        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.InvalidJson, AssistantProviderFailure.Reason(exception));
        Assert.Equal(AssistantJsonParseClassifier.OutputTruncated, AssistantProviderFailure.ParseFailureCategory(exception));
        Assert.Equal(AssistantLlmStages.Semantic, AssistantProviderFailure.Stage(exception));
    }

    [Fact]
    public void ParseBatch_TruncatedWithFinishLength_HasTokenDiagnostics()
    {
        var truncated = """{"scores":[{"foodItemId":"11111111-1111-1111-1111-111111111111","semanticCompatibility":0.9,"reasons":["khớp vị""";
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
            AssistantSemanticMatcher.ParseBatch(completion, new HashSet<Guid> { Guid.Parse("11111111-1111-1111-1111-111111111111") }));

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
            AssistantSemanticMatcher.ParseBatch("""{"scores":{"foodItemId":"x"}}""", new HashSet<Guid>()));

        Assert.Equal(AssistantErrors.InvalidJson, AssistantProviderFailure.Reason(exception));
        Assert.Equal(AssistantJsonParseClassifier.SchemaMismatch, AssistantProviderFailure.ParseFailureCategory(exception));
    }

    [Fact]
    public void ParseBatch_Malformed_DoesNotInventScores()
    {
        var exception = Assert.Throws<AppException>(() =>
            AssistantSemanticMatcher.ParseBatch("not-json{{{", new HashSet<Guid> { Guid.NewGuid() }));

        Assert.Equal(AssistantErrors.InvalidJson, AssistantProviderFailure.Reason(exception));
        Assert.Equal(AssistantJsonParseClassifier.MalformedJson, AssistantProviderFailure.ParseFailureCategory(exception));
    }

    [Fact]
    public async Task Score_RequestsSemanticTokenBudget()
    {
        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string user, int _, CancellationToken _) =>
                Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(CandidateIds(user))));
        var matcher = Create(llm.Object, 30);

        await matcher.ScoreAsync("gợi ý", Intent(), [Eligible("A")], CancellationToken.None);

        llm.Verify(client => client.CompleteJsonAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            2500,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Score_AnyBatchTimeout_FailsEntireTurn()
    {
        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException());
        var matcher = Create(llm.Object, 30);

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
        var matcher = Create(llm.Object, 30);

        var result = await matcher.ScoreAsync("gợi ý", Intent(), [], CancellationToken.None);

        Assert.Empty(result.Scores);
        Assert.Equal(0, result.BatchCount);
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ProjectFood_IncludesSalesRatingAndPriceFacts()
    {
        var eligible = Eligible("Bún bò");
        eligible.FoodItem.AverageRating = 4.2m;
        eligible.FoodItem.ReviewCount = 6;
        eligible.FoodItem.IsFeatured = true;
        var matcher = Create(new Mock<ILanguageModelClient>().Object, 30);
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
        Assert.Equal(4, compact.SoldToday);
        Assert.Equal(11, compact.OrderCount);
        Assert.True(compact.HasActivePromotion);
        Assert.True(compact.IsFeatured);
    }

    private static AssistantSemanticMatcher Create(ILanguageModelClient llm, int batchSize, int concurrency = 3, int semanticBatchRetryCount = 1)
        => new(
            llm,
            Options.Create(new AssistantOptions
            {
                CandidateBatchSize = batchSize,
                SemanticBatchMaxConcurrency = concurrency,
                SemanticBatchRetryCount = semanticBatchRetryCount
            }),
            Options.Create(new OpenAiOptions { ApiKey = "test", MaxOutputTokensSemantic = 2500 }));

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

    private static string ScoresJson(IReadOnlyList<Guid> ids)
        => JsonSerializer.Serialize(new
        {
            scores = ids.Select(id => new
            {
                foodItemId = id,
                semanticCompatibility = 0.5,
                reasons = new[] { "khớp mô tả" }
            }).ToArray()
        });

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

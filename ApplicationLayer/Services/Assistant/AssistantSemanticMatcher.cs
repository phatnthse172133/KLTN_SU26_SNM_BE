using System.Diagnostics;
using System.Text.Json;
using ApplicationLayer.Exceptions;
using DomainLayer.Common;
using DomainLayer.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services.Assistant;

public sealed class AssistantSemanticMatcher(
    ILanguageModelClient languageModel,
    IOptions<AssistantOptions> assistantOptions,
    IOptions<OpenAiOptions> openAiOptions,
    ILogger<AssistantSemanticMatcher>? logger = null)
{
    private readonly AssistantOptions _assistant = assistantOptions.Value;
    private readonly OpenAiOptions _openAi = openAiOptions.Value;

    public async Task<AssistantSemanticMatchResult> ScoreAsync(
        string originalMessage,
        ParsedAssistantIntent intent,
        IReadOnlyList<AssistantEligibleFood> eligible,
        CancellationToken cancellationToken)
    {
        if (eligible.Count == 0)
            return new AssistantSemanticMatchResult();

        var semanticWatch = Stopwatch.StartNew();
        var batchSize = Math.Max(1, _assistant.CandidateBatchSize);
        var batches = SplitBatches(eligible, batchSize);
        var idsSent = eligible.Select(item => item.FoodItem.Id).ToArray();
        var batchScores = new IReadOnlyList<AssistantSemanticScore>[batches.Count];
        var batchDiagnostics = new AssistantSemanticBatchDiagnostics[batches.Count];
        var totalRetries = 0;

        if (batches.Count == 1)
        {
            var (batchResult, diagnostics) = await ScoreBatchAsync(originalMessage, intent, batches[0], batchIndex: 0, cancellationToken);
            batchScores[0] = batchResult;
            batchDiagnostics[0] = diagnostics;
            totalRetries += diagnostics.RetryCount;
        }
        else
        {
            var concurrency = Math.Max(1, _assistant.SemanticBatchMaxConcurrency);
            using var gate = new SemaphoreSlim(concurrency);
            var tasks = new Task[batches.Count];
            for (var index = 0; index < batches.Count; index++)
            {
                var captured = index;
                tasks[captured] = RunBoundedBatchAsync(
                    gate,
                    async () =>
                    {
                        var (batchResult, diagnostics) = await ScoreBatchAsync(originalMessage, intent, batches[captured], captured, cancellationToken);
                        batchScores[captured] = batchResult;
                        batchDiagnostics[captured] = diagnostics;
                    },
                    cancellationToken);
            }

            await Task.WhenAll(tasks);
            totalRetries = batchDiagnostics.Sum(item => item.RetryCount);
        }

        semanticWatch.Stop();

        var scores = new Dictionary<Guid, AssistantSemanticScore>();
        foreach (var batch in batchScores)
        {
            foreach (var score in batch)
                scores[score.FoodItemId] = score;
        }

        if (scores.Count != eligible.Count || idsSent.Any(id => !scores.ContainsKey(id)))
        {
            logger?.LogWarning(
                "Assistant semantic aggregate score count mismatch. EligibleCount={EligibleCount} BatchCount={BatchCount} IdsSent={IdsSent} IdsEvaluated={IdsEvaluated} CandidateBatchSize={CandidateBatchSize} SemanticBatchRetryCount={SemanticBatchRetryCount} SemanticMaxOutputTokens={SemanticMaxOutputTokens}",
                eligible.Count,
                batches.Count,
                idsSent.Length,
                scores.Count,
                batchSize,
                _assistant.SemanticBatchRetryCount,
                _openAi.MaxOutputTokensSemantic);
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.SemanticScoreCountMismatch);
        }

        return new AssistantSemanticMatchResult
        {
            Scores = scores,
            BatchCount = batches.Count,
            LogicalBatchCount = batches.Count,
            ProviderCallCount = batches.Count + totalRetries,
            IdsSent = idsSent,
            SemanticTotalMs = semanticWatch.ElapsedMilliseconds,
            SemanticRetryCount = totalRetries,
            BatchDiagnostics = batchDiagnostics
        };
    }

    public static IReadOnlyList<int> GetBatchSizes(int eligibleCount, int candidateBatchSize)
    {
        if (eligibleCount <= 0)
            return [];

        var size = Math.Max(1, candidateBatchSize);
        var sizes = new List<int>();
        var remaining = eligibleCount;
        while (remaining > 0)
        {
            var take = Math.Min(size, remaining);
            sizes.Add(take);
            remaining -= take;
        }
        return sizes;
    }

    public CompactAssistantFoodCandidate Project(AssistantEligibleFood eligible, CompactCandidateProjectionOptions projection)
        => ProjectFood(eligible, projection);

    internal static CompactAssistantFoodCandidate ProjectFood(
        AssistantEligibleFood eligible,
        CompactCandidateProjectionOptions projection)
    {
        var food = eligible.FoodItem;
        return new CompactAssistantFoodCandidate
        {
            FoodItemId = food.Id,
            Name = food.Name,
            Description = projection.IncludeDescription
                ? Trim(food.Description, projection.MaxDescriptionCharacters)
                : null,
            CategoryName = food.Category.Name,
            SpiceLevel = food.SpiceLevel.ToString(),
            ServingTemperature = food.ServingTemperature?.ToString(),
            EstimatedServingCount = food.EstimatedServingCount,
            IsShareable = food.IsShareable,
            EffectivePrice = eligible.EffectivePrice,
            AverageRating = food.AverageRating,
            ReviewCount = food.ReviewCount,
            HasActivePromotion = eligible.HasActivePromotion,
            IsFeatured = food.IsFeatured,
            IngredientCodes = projection.IncludeIngredients
                ? food.Ingredients.Select(item => item.Ingredient.Code).Distinct().ToArray()
                : [],
            AllergenCodes = projection.IncludeAllergens
                ? food.Allergens.Select(item => item.Allergen.Code).Distinct().ToArray()
                : [],
            DietaryCodes = projection.IncludeDietary
                ? food.DietaryAttributes
                    .Where(item => item.SuitabilityStatus == DietarySuitabilityStatus.SUITABLE)
                    .Select(item => item.DietaryAttribute.Code)
                    .Distinct()
                    .ToArray()
                : [],
            TasteCodes = projection.IncludeTastes
                ? food.TasteProfiles.Select(item => item.TasteProfile.Code).Distinct().ToArray()
                : [],
            PreparationCodes = projection.IncludePreparation
                ? food.PreparationMethods.Select(item => item.PreparationMethod.Code).Distinct().ToArray()
                : [],
            CourseCodes = projection.IncludeCourses
                ? food.Courses.Select(item => item.Course.ToString()).Distinct().ToArray()
                : []
        };
    }

    private static IReadOnlyList<IReadOnlyList<AssistantEligibleFood>> SplitBatches(
        IReadOnlyList<AssistantEligibleFood> eligible,
        int batchSize)
    {
        var batches = new List<IReadOnlyList<AssistantEligibleFood>>();
        for (var offset = 0; offset < eligible.Count; offset += batchSize)
            batches.Add(eligible.Skip(offset).Take(batchSize).ToArray());
        return batches;
    }

    private static async Task RunBoundedBatchAsync(
        SemaphoreSlim gate,
        Func<Task> run,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await run();
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<(IReadOnlyList<AssistantSemanticScore> Scores, AssistantSemanticBatchDiagnostics Diagnostics)> ScoreBatchAsync(
        string originalMessage,
        ParsedAssistantIntent intent,
        IReadOnlyList<AssistantEligibleFood> batch,
        int batchIndex,
        CancellationToken cancellationToken)
    {
        var compact = batch.Select(item => Project(item, _assistant.CompactCandidateProjection)).ToArray();
        var orderedIds = compact.Select(item => item.FoodItemId).ToArray();
        EnsureUniqueBatchInput(orderedIds);

        var batchWatch = Stopwatch.StartNew();
        var extraAttempts = Math.Clamp(_assistant.SemanticBatchRetryCount, 0, 1);
        LanguageModelJsonCompletion? lastCompletion = null;
        var retryCount = 0;
        IReadOnlyList<AssistantSemanticScore>? parsed = null;

        for (var attempt = 0; attempt <= extraAttempts; attempt++)
        {
            lastCompletion = await CompleteBatchAsync(originalMessage, intent, compact, cancellationToken);
            try
            {
                parsed = ParseBatch(lastCompletion, orderedIds, logger, batchIndex);
                break;
            }
            catch (AppException exception) when (attempt < extraAttempts && AssistantErrors.IsProviderReason(exception, AssistantErrors.SemanticScoreCountMismatch))
            {
                retryCount++;
                logger?.LogWarning(
                    "Assistant semantic batch score count mismatch; retrying batch. BatchIndex={BatchIndex} Attempt={Attempt} BatchSize={BatchSize} FinishReason={FinishReason} SemanticMaxOutputTokens={SemanticMaxOutputTokens} RequestId={RequestId}",
                    batchIndex,
                    attempt + 1,
                    orderedIds.Length,
                    lastCompletion.FinishReason,
                    _openAi.MaxOutputTokensSemantic,
                    lastCompletion.RequestId);
            }
        }

        parsed ??= ParseBatch(lastCompletion!, orderedIds, logger, batchIndex);
        batchWatch.Stop();
        return (parsed, new AssistantSemanticBatchDiagnostics
        {
            BatchIndex = batchIndex,
            BatchSize = orderedIds.Length,
            DurationMs = batchWatch.ElapsedMilliseconds,
            RetryCount = retryCount,
            FinishReason = lastCompletion?.FinishReason,
            IdsSent = orderedIds.Length,
            IdsReturned = parsed.Count
        });
    }

    internal static void EnsureUniqueBatchInput(IReadOnlyList<Guid> orderedIds)
    {
        if (orderedIds.Count == 0)
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.InvalidBatchInput);
        if (orderedIds.Any(id => id == Guid.Empty))
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.InvalidBatchInput);
        if (orderedIds.ToHashSet().Count != orderedIds.Count)
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.InvalidBatchInput);
    }

    private async Task<LanguageModelJsonCompletion> CompleteBatchAsync(
        string originalMessage,
        ParsedAssistantIntent intent,
        IReadOnlyList<CompactAssistantFoodCandidate> batch,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            originalMessage,
            parsedIntent = intent,
            candidates = batch
        }, AssistantJson.Options);

        var schemaJson = AssistantSemanticSchemaBuilder.Build(batch.Count);
        LanguageModelJsonSchemaOptions? schemaOptions = _openAi.UseJsonSchemaStrict
            ? new LanguageModelJsonSchemaOptions
            {
                SchemaJson = schemaJson,
                Name = "semantic_scores",
                Strict = true
            }
            : null;

        try
        {
            return await languageModel.CompleteJsonAsync(
                AssistantPromptCatalog.SemanticSystem + "\nJSON schema:\n" + schemaJson,
                payload,
                _openAi.MaxOutputTokensSemantic,
                cancellationToken,
                schemaOptions);
        }
        catch (AppException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.Timeout, exception);
        }
        catch (Exception exception)
        {
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.HttpError, exception);
        }
    }

    public static IReadOnlyList<AssistantSemanticScore> ParseBatch(string raw, IReadOnlyList<Guid> orderedCandidateIds)
        => ParseBatch(LanguageModelJsonCompletion.FromContent(raw), orderedCandidateIds);

    public static IReadOnlyList<AssistantSemanticScore> ParseBatch(
        LanguageModelJsonCompletion completion,
        IReadOnlyList<Guid> orderedCandidateIds,
        ILogger? logger = null,
        int? batchIndex = null)
    {
        var dto = AssistantJsonParseClassifier.DeserializeOrThrow<SemanticBatchDto>(
            completion,
            AssistantLlmStages.Semantic,
            logger);

        if (dto.Scores is null)
        {
            throw AssistantErrors.InvalidProviderJson(
                AssistantLlmStages.Semantic,
                completion,
                AssistantJsonParseClassifier.MissingRequiredField,
                logger: logger);
        }

        if (dto.Scores.Count != orderedCandidateIds.Count)
        {
            throw AssistantErrors.SemanticScoreCountMismatchUnavailable(
                completion,
                orderedCandidateIds.Count,
                dto.Scores.Count,
                logger,
                batchIndex);
        }

        var result = new List<AssistantSemanticScore>(orderedCandidateIds.Count);
        for (var index = 0; index < orderedCandidateIds.Count; index++)
        {
            result.Add(new AssistantSemanticScore
            {
                FoodItemId = orderedCandidateIds[index],
                SemanticCompatibility = Math.Clamp(dto.Scores[index], 0d, 1d)
            });
        }

        return result;
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private sealed class SemanticBatchDto
    {
        public List<double>? Scores { get; set; }
    }
}

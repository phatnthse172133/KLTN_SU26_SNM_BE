using System.Text.Json;
using ApplicationLayer.Exceptions;
using DomainLayer.Common;
using DomainLayer.Enums;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services.Assistant;

public sealed class AssistantSemanticMatcher(
    ILanguageModelClient languageModel,
    IOptions<AssistantOptions> assistantOptions,
    IOptions<OpenAiOptions> openAiOptions)
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

        var batchSize = Math.Max(1, _assistant.CandidateBatchSize);
        var batches = SplitBatches(eligible, batchSize);
        var idsSent = eligible.Select(item => item.FoodItem.Id).ToArray();
        var batchScores = new IReadOnlyList<AssistantSemanticScore>[batches.Count];

        if (batches.Count == 1)
        {
            batchScores[0] = await ScoreBatchAsync(originalMessage, intent, batches[0], cancellationToken);
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
                    () => ScoreBatchAsync(originalMessage, intent, batches[captured], cancellationToken),
                    scores => batchScores[captured] = scores,
                    cancellationToken);
            }

            await Task.WhenAll(tasks);
        }

        var scores = new Dictionary<Guid, AssistantSemanticScore>();
        foreach (var batch in batchScores)
        {
            foreach (var score in batch)
                scores[score.FoodItemId] = score;
        }

        if (scores.Count != eligible.Count || idsSent.Any(id => !scores.ContainsKey(id)))
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.IncompleteIdSet);

        return new AssistantSemanticMatchResult
        {
            Scores = scores,
            BatchCount = batches.Count,
            IdsSent = idsSent
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
            BoothName = food.Booth.BoothName,
            NightMarketName = food.Booth.NightMarket.Name,
            SpiceLevel = food.SpiceLevel.ToString(),
            ServingTemperature = food.ServingTemperature?.ToString(),
            EstimatedServingCount = food.EstimatedServingCount,
            IsShareable = food.IsShareable,
            EffectivePrice = eligible.EffectivePrice,
            AverageRating = food.AverageRating,
            ReviewCount = food.ReviewCount,
            SoldToday = eligible.SoldToday,
            OrderCount = eligible.OrderCount,
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
        Func<Task<IReadOnlyList<AssistantSemanticScore>>> run,
        Action<IReadOnlyList<AssistantSemanticScore>> assign,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            assign(await run());
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<AssistantSemanticScore>> ScoreBatchAsync(
        string originalMessage,
        ParsedAssistantIntent intent,
        IReadOnlyList<AssistantEligibleFood> batch,
        CancellationToken cancellationToken)
    {
        var compact = batch.Select(item => Project(item, _assistant.CompactCandidateProjection)).ToArray();
        var allowed = compact.Select(item => item.FoodItemId).ToHashSet();
        var raw = await CompleteBatchAsync(originalMessage, intent, compact, cancellationToken);
        return ParseBatch(raw, allowed);
    }

    private async Task<string> CompleteBatchAsync(
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

        try
        {
            return await languageModel.CompleteJsonAsync(
                AssistantPromptCatalog.SemanticSystem + "\nJSON schema:\n" + AssistantPromptCatalog.SemanticSchema,
                payload,
                _openAi.MaxOutputTokensSemantic,
                cancellationToken);
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

    public static IReadOnlyList<AssistantSemanticScore> ParseBatch(string raw, ISet<Guid> allowedIds)
    {
        SemanticBatchDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<SemanticBatchDto>(raw, AssistantJson.Options)
                ?? throw new JsonException("Semantic payload was null.");
        }
        catch (JsonException exception)
        {
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.InvalidJson, exception);
        }

        if (dto.Scores is null)
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.InvalidJson);

        var result = new List<AssistantSemanticScore>();
        var seen = new HashSet<Guid>();
        foreach (var item in dto.Scores)
        {
            if (item.FoodItemId == Guid.Empty || !allowedIds.Contains(item.FoodItemId))
                throw AssistantErrors.ProviderUnavailable(AssistantErrors.HallucinatedId);
            if (!seen.Add(item.FoodItemId))
                throw AssistantErrors.ProviderUnavailable(AssistantErrors.DuplicateId);

            result.Add(new AssistantSemanticScore
            {
                FoodItemId = item.FoodItemId,
                SemanticCompatibility = Math.Clamp(item.SemanticCompatibility, 0d, 1d),
                Confidence = Math.Clamp(item.Confidence ?? 1d, 0d, 1d),
                Reasons = (item.Reasons ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray(),
                UnknownDataFacets = (item.UnknownDataFacets ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray()
            });
        }

        if (result.Count != allowedIds.Count)
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.IncompleteIdSet);

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
        public List<SemanticScoreDto>? Scores { get; set; }
    }

    private sealed class SemanticScoreDto
    {
        public Guid FoodItemId { get; set; }
        public double SemanticCompatibility { get; set; }
        public double? Confidence { get; set; }
        public List<string>? Reasons { get; set; }
        public List<string>? UnknownDataFacets { get; set; }
    }
}

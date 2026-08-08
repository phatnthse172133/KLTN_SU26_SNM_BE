namespace ApplicationLayer.Services.Menus;

public interface IFoodAiProfileEnrichmentService
{
    /// <summary>Post-commit side effect: enrich one food AI profile via OpenAI. Never throws to callers.</summary>
    Task EnrichOneAsync(Guid foodItemId, CancellationToken cancellationToken = default);

    /// <summary>Force regenerate one profile (admin). Skips DISABLED. Never throws.</summary>
    Task EnrichOneAsync(Guid foodItemId, bool force, CancellationToken cancellationToken = default);

    /// <summary>
    /// Controlled backfill of PENDING/STALE/(FAILED if retryFailed) profiles.
    /// Never runs on startup; never re-enriches READY with unchanged source hash.
    /// </summary>
    Task<FoodAiProfileBackfillResult> BackfillAsync(int batchSize, bool retryFailed, CancellationToken cancellationToken = default);
}

public sealed record FoodAiProfileBackfillResult(int Processed, int Succeeded, int Failed, int Skipped);

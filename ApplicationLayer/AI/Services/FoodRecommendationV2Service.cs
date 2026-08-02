using System.Diagnostics;
using System.Text.Json;
using ApplicationLayer.AI.V2.Configuration;
using ApplicationLayer.AI.V2;
using ApplicationLayer.AI.V2.Models;
using ApplicationLayer.AI.V2.Recommendations;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.NightMarkets;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.AI.V2.Services;

public sealed class FoodRecommendationV2Service(
    IAiIntentExtractor intentExtractor,
    IAiExplanationGenerator explanationGenerator,
    IDeterministicRecommendationReasonBuilder deterministicReason,
    IFoodRecommendationIntentNormalizer normalizer,
    IFoodSemanticMatcher semanticMatcher,
    IFoodRecommendationRanker ranker,
    IFoodRecommendationDiversityReranker diversity,
    IFoodRecommendationReadRepository candidates,
    IAiRecommendationSessionRepository sessions,
    IFoodSemanticMetadataRepository metadata,
    IOptions<RecommendationV2Options> options,
    TimeProvider timeProvider,
    ILogger<FoodRecommendationV2Service> logger) : IFoodRecommendationV2Service
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RecommendationV2Options _options = options.Value;

    public async Task<ApiResponse<FoodRecommendationV2Response>> RecommendAsync(Guid customerId, CreateFoodRecommendationV2Request request, CancellationToken cancellationToken)
    {
        AiV2Telemetry.RecommendationRequests.Add(1);
        ValidateRequest(request);
        var started = Stopwatch.StartNew();
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var catalogs = await metadata.GetActiveCatalogsAsync(cancellationToken);
        var profile = await metadata.GetCustomerProfileAsync(customerId, false, cancellationToken);
        var taxonomy = new AiTaxonomyCodes(catalogs.Ingredients.Select(value => value.Code).ToArray(), catalogs.Allergens.Select(value => value.Code).ToArray(),
            catalogs.DietaryAttributes.Select(value => value.Code).ToArray(), catalogs.PreparationMethods.Select(value => value.Code).ToArray(),
            catalogs.TasteProfiles.Select(value => value.Code).ToArray(), Enum.GetNames<FoodCourse>(), Enum.GetNames<DiningPurpose>());
        var extraction = await intentExtractor.ExtractFoodRecommendationIntentAsync(
            new(request.Query.Trim(), taxonomy, request.InputLanguage, request.ResponseLanguage), cancellationToken);
        if (extraction.FailureCategory == AiProviderFailureCategory.NONE && !extraction.UsedFallback)
            AiV2Telemetry.ProviderSuccesses.Add(1, new KeyValuePair<string, object?>("operation", "intent"));
        else
            AiV2Telemetry.ProviderFailures.Add(1, new KeyValuePair<string, object?>("operation", "intent"), new KeyValuePair<string, object?>("category", extraction.FailureCategory.ToString()));
        if (extraction.UsedFallback) AiV2Telemetry.Fallbacks.Add(1, new KeyValuePair<string, object?>("operation", "intent"));
        if (extraction.FailureCategory is AiProviderFailureCategory.INVALID_RESPONSE or AiProviderFailureCategory.VALIDATION_FAILED)
            AiV2Telemetry.InvalidParses.Add(1);
        if (!extraction.IsSuccess || extraction.ParsedResult is null)
        {
            if (extraction.ValidationWarnings.Contains("AI_LANGUAGE_PROVIDER_REQUIRED"))
                throw AppException.UnprocessableEntity("This language requires the advanced AI provider.", "AI_LANGUAGE_PROVIDER_REQUIRED");
            if (extraction.FailureCategory == AiProviderFailureCategory.CANCELLED)
                throw new OperationCanceledException(cancellationToken);
            if (extraction.FailureCategory == AiProviderFailureCategory.INVALID_RESPONSE)
                throw AppException.ServiceUnavailable("The provider response was invalid and deterministic fallback was insufficient.", "AI_PROVIDER_INVALID_RESPONSE");
            if (extraction.FailureCategory is AiProviderFailureCategory.TIMEOUT or AiProviderFailureCategory.RATE_LIMITED
                or AiProviderFailureCategory.TRANSIENT_ERROR or AiProviderFailureCategory.PERMANENT_ERROR)
                throw AppException.ServiceUnavailable("The provider was unavailable and deterministic fallback was insufficient.", "AI_PROVIDER_UNAVAILABLE");
            throw AppException.UnprocessableEntity("The food request could not be understood sufficiently.", "AI_FALLBACK_PARSE_INSUFFICIENT");
        }
        var normalized = normalizer.Normalize(extraction.ParsedResult, new(request.Query.Trim(), request.MaximumPrice,
            request.MaxDistanceMeters, request.Latitude.HasValue, profile, catalogs));
        if (!normalized.IsValid || normalized.Intent is null)
            throw AppException.UnprocessableEntity(string.Join("; ", normalized.Errors), "AI_INVALID_REQUEST");
        var intent = normalized.Intent;

        if (intent.PreferNearMe && !request.Latitude.HasValue)
        {
            var empty = Response(Guid.NewGuid(), "LOCATION_REQUIRED", extraction.UsedFallback, intent, request, [], [],
                intent.Warnings.Concat(["LOCATION_REQUIRED"]).Distinct().ToArray());
            await Persist(empty.SessionId, customerId, request.Query, intent, extraction, utcNow, [], cancellationToken);
            AiV2Telemetry.NoResults.Add(1, new KeyValuePair<string, object?>("status", "LOCATION_REQUIRED"));
            AiV2Telemetry.RecommendationDurationMs.Record(started.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("status", "LOCATION_REQUIRED"));
            return ApiResponse<FoodRecommendationV2Response>.SuccessResponse(empty);
        }

        var loaded = await candidates.GetCandidatesAsync(utcNow, Math.Clamp(_options.CandidateLimit, 1, 500), cancellationToken);
        AiV2Telemetry.Candidates.Add(loaded.Count);
        var ranked = new List<RankedRecommendationCandidate>();
        foreach (var candidate in loaded)
        {
            var distance = CalculateDistance(request.Latitude, request.Longitude, candidate.MarketLatitude, candidate.MarketLongitude);
            if (!Eligible(candidate, intent, request.Latitude.HasValue, distance, utcNow)) continue;
            var semantic = semanticMatcher.Match(intent, candidate);
            ranked.Add(ranker.Rank(intent, candidate, semantic, distance));
        }
        var reranked = diversity.Rerank(ranked, intent.SortPreference).Where(value => value.Tier != RecommendationMatchTier.LOW_MATCH).ToList();
        var strong = reranked.Where(value => value.Tier == RecommendationMatchTier.STRONG_MATCH).ToList();
        var near = reranked.Where(value => value.Tier == RecommendationMatchTier.NEAR_MATCH).ToList();
        var usedExplanationFallback = false;
        var reasonMap = new Dictionary<Guid, string>();
        foreach (var value in reranked)
            reasonMap[value.Candidate.FoodId] = deterministicReason.Build(ExplanationContext(intent, value));
        if (reranked.Count > 0 && Math.Clamp(_options.MaximumExplanationCalls, 0, 1) > 0)
        {
            var top = reranked[0];
            var generated = await explanationGenerator.GenerateFoodRecommendationReasonAsync(ExplanationContext(intent, top), cancellationToken);
            if (generated.FailureCategory == AiProviderFailureCategory.CANCELLED)
                throw new OperationCanceledException(cancellationToken);
            if (generated.IsSuccess && !string.IsNullOrWhiteSpace(generated.Text))
            {
                AiV2Telemetry.ProviderSuccesses.Add(1, new KeyValuePair<string, object?>("operation", "explanation"));
                reasonMap[top.Candidate.FoodId] = generated.Text;
            }
            else
            {
                AiV2Telemetry.ProviderFailures.Add(1, new KeyValuePair<string, object?>("operation", "explanation"), new KeyValuePair<string, object?>("category", generated.FailureCategory.ToString()));
                usedExplanationFallback = true;
            }
        }

        var page = request.Page; var size = request.PageSize;
        var strongPage = strong.Skip((page - 1) * size).Take(size).Select(value => Item(value, reasonMap[value.Candidate.FoodId])).ToArray();
        var nearPage = near.Skip((page - 1) * size).Take(size).Select(value => Item(value, reasonMap[value.Candidate.FoodId])).ToArray();
        var status = strong.Count > 0 ? "SUCCESS" : near.Count > 0 ? "NO_STRONG_MATCH" : "NO_SUITABLE_RESULTS";
        if (usedExplanationFallback) AiV2Telemetry.ExplanationFallbacks.Add(1);
        if (strong.Count > 0) AiV2Telemetry.StrongMatches.Add(strong.Count);
        if (reranked.Count == 0) AiV2Telemetry.NoResults.Add(1, new KeyValuePair<string, object?>("status", status));
        var sessionId = Guid.NewGuid();
        var response = Response(sessionId, status, extraction.UsedFallback || usedExplanationFallback, intent, request, strongPage, nearPage,
            intent.Warnings.Concat(extraction.ValidationWarnings).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(), strong.Count, near.Count);
        var persistable = reranked.Select((value, index) => new AiRecommendationResult
        {
            SessionId = sessionId, FoodItemId = value.Candidate.FoodId, Rank = index + 1,
            Score = value.BaseScore, MatchTier = value.Tier, CreatedAt = utcNow
        }).ToArray();
        await Persist(sessionId, customerId, request.Query, intent, extraction, utcNow, persistable, cancellationToken);
        logger.LogInformation("AI V2 recommendation completed. SessionId={SessionId} CustomerId={CustomerId} ProviderStatus={ProviderStatus} UsedFallback={UsedFallback} Candidates={CandidateCount} Strong={StrongCount} Near={NearCount} DurationMs={DurationMs}",
            sessionId, customerId, extraction.FailureCategory, response.UsedProviderFallback, loaded.Count, strong.Count, near.Count, started.ElapsedMilliseconds);
        AiV2Telemetry.RecommendationDurationMs.Record(started.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("status", status));
        return ApiResponse<FoodRecommendationV2Response>.SuccessResponse(response);
    }

    public async Task<ApiResponse<RecommendationFeedbackV2Response>> FeedbackAsync(Guid customerId, Guid sessionId, RecommendationFeedbackV2Request request, CancellationToken cancellationToken)
    {
        if (request.FoodId == Guid.Empty || !Enum.TryParse<AiRecommendationFeedbackAction>(request.Action?.Trim(), false, out var action)
            || action is AiRecommendationFeedbackAction.SELECTED or AiRecommendationFeedbackAction.DISMISSED)
            throw AppException.BadRequest("Feedback action is invalid.", "AI_INVALID_REQUEST");
        var result = await sessions.RecordFeedbackAsync(customerId, sessionId, request.FoodId, action, timeProvider.GetUtcNow().UtcDateTime,
            Math.Clamp(_options.FeedbackWindowMinutes, 1, 1440), cancellationToken);
        if (result.Status == RecommendationFeedbackRecordStatus.NOT_FOUND)
            throw AppException.NotFound("Recommendation session was not found.", "AI_RECOMMENDATION_SESSION_NOT_FOUND");
        if (result.Status == RecommendationFeedbackRecordStatus.EXPIRED)
            throw AppException.UnprocessableEntity("The recommendation feedback window has expired.", "AI_RECOMMENDATION_SESSION_EXPIRED");
        if (result.Status == RecommendationFeedbackRecordStatus.FOOD_NOT_IN_SESSION)
            throw AppException.UnprocessableEntity("The food was not part of this recommendation session.", "AI_RECOMMENDATION_FOOD_NOT_IN_SESSION");
        var feedback = result.Feedback!;
        AiV2Telemetry.FeedbackActions.Add(1, new KeyValuePair<string, object?>("action", action.ToString()));
        return ApiResponse<RecommendationFeedbackV2Response>.SuccessResponse(new()
        { FeedbackId = feedback.Id, SessionId = sessionId, FoodId = request.FoodId, Action = action.ToString() });
    }

    private bool Eligible(FoodRecommendationCandidate value, FoodRecommendationIntent intent, bool hasLocation, int? distance, DateTime utcNow)
    {
        if (value.IsDeleted || value.CategoryDeleted || !value.CategoryIsActive || !value.CategoryIsSelectable
            || !value.IsAvailable || value.CurrentPrice <= 0 || value.BoothStatus != BoothStatus.Active
            || value.MarketDeleted || value.MarketModerationStatus != ModerationStatus.Active || value.MarketStatus != NightMarketStatus.Active) return false;
        var local = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(utcNow));
        if (!CustomerAvailability.IsOpenNow(true, value.MarketOpenTime, value.MarketCloseTime, value.BoothOpenTime, value.BoothCloseTime, local)) return false;
        if (intent.MaximumPrice.HasValue && value.CurrentPrice > intent.MaximumPrice || intent.MinimumPrice.HasValue && value.CurrentPrice < intent.MinimumPrice) return false;
        if (intent.ExcludedIngredientCodes.Intersect(value.IngredientCodes, StringComparer.Ordinal).Any()) return false;
        if (intent.AvoidedPreparationMethodCodes.Intersect(value.PreparationMethodCodes, StringComparer.Ordinal).Any()) return false;
        if (intent.DietaryRequirementCodes.Any(required => !value.DietaryAttributes.Any(attribute => attribute.Code == required
            && attribute.IsConfirmed && attribute.Status == DietarySuitabilityStatus.SUITABLE))) return false;
        // The model has positive CONTAINS/MAY_CONTAIN declarations but no authoritative "free from" declaration.
        // Therefore both a matching declaration and an absent declaration are unsafe/unknown for an explicit exclusion.
        if (intent.AllergenExclusionCodes.Count > 0) return false;
        if (hasLocation && intent.MaximumDistanceMeters.HasValue && (!distance.HasValue || distance > intent.MaximumDistanceMeters)) return false;
        return true;
    }

    private void ValidateRequest(CreateFoodRecommendationV2Request request)
    {
        request.Query = request.Query?.Trim() ?? string.Empty;
        request.InputLanguage = NormalizeLanguage(request.InputLanguage, true);
        request.ResponseLanguage = NormalizeLanguage(request.ResponseLanguage, false);
        if (request.Query.Length is < 2 || request.Query.Length > Math.Clamp(_options.MaximumQueryCharacters, 100, 4000))
            throw AppException.BadRequest("Query length is invalid.", "AI_INVALID_REQUEST");
        if (request.Latitude.HasValue != request.Longitude.HasValue) throw AppException.BadRequest("Latitude and longitude must be supplied together.", "AI_INVALID_LOCATION");
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180) throw AppException.BadRequest("Coordinates are invalid.", "AI_INVALID_LOCATION");
        if (request.MaxDistanceMeters is <= 0 || request.MaxDistanceMeters > Math.Clamp(_options.MaximumDistanceMeters, 100, 500_000))
            throw AppException.BadRequest("Maximum distance is invalid.", "AI_INVALID_LOCATION");
        if (request.MaximumPrice is <= 0 || request.MaximumPrice > Math.Clamp(_options.MaximumSupportedPrice, 1m, 1_000_000_000m))
            throw AppException.BadRequest("Maximum price is invalid.", "AI_INVALID_REQUEST");
        if (request.Page < 1 || request.PageSize < 1 || request.PageSize > Math.Clamp(_options.MaximumPageSize, 1, 50))
            throw AppException.BadRequest("Paging is invalid.", "AI_INVALID_REQUEST");
    }

    private static string NormalizeLanguage(string? value, bool allowAuto)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? (allowAuto ? "auto" : "vi");
        if ((allowAuto && normalized == "auto") || normalized is "vi" or "en" or "ja" or "ko") return normalized;
        throw AppException.BadRequest("Language is not supported.", "AI_LANGUAGE_NOT_SUPPORTED");
    }

    private async Task Persist(Guid id, Guid customerId, string query, FoodRecommendationIntent intent,
        FoodRecommendationIntentExtractionResult extraction, DateTime now, IReadOnlyCollection<AiRecommendationResult> results, CancellationToken cancellationToken)
    {
        var lifetime = TimeSpan.FromMinutes(Math.Clamp(_options.SessionLifetimeMinutes, 5, 1440));
        var session = new AiRecommendationSession
        {
            Id = id, CustomerId = customerId, OriginalQuery = query.Length <= 1000 ? query : query[..1000],
            ParsedPreferenceJson = JsonSerializer.Serialize(intent, JsonOptions), MaxDistanceMeters = intent.MaximumDistanceMeters,
            Status = AiSessionStatus.COMPLETED, ProviderName = extraction.ProviderName, ProviderModelName = extraction.ModelName,
            ProviderRequestId = extraction.ProviderRequestId, ProviderFailureCategory = extraction.FailureCategory.ToString(),
            UsedFallback = extraction.UsedFallback, CreatedAt = now, ExpiresAt = now.Add(lifetime)
        };
        await sessions.SaveSessionAsync(session, results, cancellationToken);
    }

    private static FoodRecommendationExplanationContext ExplanationContext(FoodRecommendationIntent intent, RankedRecommendationCandidate value)
        => new(intent.Summary, value.Candidate.FoodName, value.Candidate.CategoryName, value.Candidate.CurrentPrice,
            value.Evidence.Ingredients, value.Evidence.TastesAndSpice, value.Evidence.Preparations, value.Evidence.CoursesAndPurposes,
            value.Evidence.Budget, value.Evidence.Distance, value.Evidence.Rating, value.Evidence.Dietary, [], intent.Warnings,
            intent.ResponseLanguage);

    private static FoodRecommendationItemResponse Item(RankedRecommendationCandidate value, string reason) => new()
    {
        FoodId = value.Candidate.FoodId, FoodName = value.Candidate.FoodName, ImageUrl = value.Candidate.ImageUrl,
        CurrentPrice = value.Candidate.CurrentPrice, CompatibilityScore = value.BaseScore, MatchTier = value.Tier.ToString(),
        Reason = reason, Rating = value.Candidate.Rating, ReviewCount = value.Candidate.ReviewCount,
        Course = value.Candidate.Courses.Count > 0 ? value.Candidate.Courses.First().ToString() : null, IsOrderable = true,
        Booth = new() { Id = value.Candidate.BoothId, Name = value.Candidate.BoothName },
        Market = new() { Id = value.Candidate.MarketId, Name = value.Candidate.MarketName, DistanceMeters = value.DistanceMeters }
    };

    private static FoodRecommendationV2Response Response(Guid sessionId, string status, bool fallback, FoodRecommendationIntent intent,
        CreateFoodRecommendationV2Request request, IReadOnlyCollection<FoodRecommendationItemResponse> strong,
        IReadOnlyCollection<FoodRecommendationItemResponse> near, IReadOnlyCollection<string> warnings, int strongTotal = 0, int nearTotal = 0) => new()
    {
        SessionId = sessionId, Status = status, UsedProviderFallback = fallback,
        UnderstoodRequest = new() { Summary = intent.Summary, MinimumPrice = intent.MinimumPrice, MaximumPrice = intent.MaximumPrice,
            InputLanguageHint = intent.InputLanguageHint, DetectedLanguage = intent.DetectedLanguage,
            ResponseLanguage = intent.ResponseLanguage, LanguageConfidence = intent.LanguageConfidence,
            LanguageWarnings = intent.LanguageWarnings,
            PreferNearMe = intent.PreferNearMe, MaximumDistanceMeters = intent.MaximumDistanceMeters,
            PreferredIngredients = intent.PreferredIngredientCodes, ExcludedIngredients = intent.ExcludedIngredientCodes,
            DietaryRequirements = intent.DietaryRequirementCodes, AllergenExclusions = intent.AllergenExclusionCodes,
            TastePreferences = intent.PreferredTasteCodes, PreparationPreferences = intent.PreparationMethodCodes, Warnings = intent.Warnings },
        Items = strong, NearMatches = near, Warnings = warnings,
        Paging = new() { Page = request.Page, PageSize = request.PageSize, TotalStrongMatches = strongTotal, TotalNearMatches = nearTotal }
    };

    private static int? CalculateDistance(decimal? fromLatitude, decimal? fromLongitude, decimal? toLatitude, decimal? toLongitude)
    {
        if (!fromLatitude.HasValue || !fromLongitude.HasValue || !toLatitude.HasValue || !toLongitude.HasValue
            || toLatitude is < -90 or > 90 || toLongitude is < -180 or > 180) return null;
        const double radius = 6_371_000; static double Radians(double degrees) => degrees * Math.PI / 180d;
        var fromLat = Radians((double)fromLatitude.Value); var toLat = Radians((double)toLatitude.Value);
        var dLat = toLat - fromLat; var dLon = Radians((double)(toLongitude.Value - fromLongitude.Value));
        var a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(fromLat) * Math.Cos(toLat) * Math.Pow(Math.Sin(dLon / 2), 2);
        return (int)Math.Round(2 * radius * Math.Asin(Math.Min(1, Math.Sqrt(a))));
    }
}

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
using Microsoft.Extensions.Hosting;
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
    IHostEnvironment hostEnvironment,
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
        var extractedIntent = extraction.ParsedResult;
        if (request.PreviousSessionId.HasValue)
        {
            var previousJson = await sessions.GetActiveParsedIntentJsonAsync(customerId, request.PreviousSessionId.Value, utcNow, cancellationToken);
            if (!string.IsNullOrWhiteSpace(previousJson))
            {
                try
                {
                    var previous = JsonSerializer.Deserialize<FoodRecommendationIntent>(previousJson, JsonOptions);
                    if (previous is not null) extractedIntent = MergeFollowUp(previous, extractedIntent);
                }
                catch (JsonException) { logger.LogWarning("AI V2 ignored invalid structured context. SessionId={SessionId}", request.PreviousSessionId); }
            }
        }
        ApplyRemovals(extractedIntent, request.RemovedIntentSignals);
        var normalized = normalizer.Normalize(extractedIntent, new(request.Query.Trim(), request.MaximumPrice,
            request.MaxDistanceMeters, request.Latitude.HasValue, profile, catalogs));
        if (!normalized.IsValid || normalized.Intent is null)
            throw AppException.UnprocessableEntity(string.Join("; ", normalized.Errors), "AI_INVALID_REQUEST");
        var intent = normalized.Intent;
        ApplyRemovals(intent, request.RemovedIntentSignals);

        if (intent.ClarificationNeeded)
        {
            var empty = Response(Guid.NewGuid(), "CLARIFICATION_NEEDED", extraction.UsedFallback, intent, request, [], [],
                intent.Warnings.Concat(intent.Ambiguities).Distinct().ToArray());
            empty.ProviderRuntime = extraction.RuntimeTrace;
            await Persist(empty.SessionId, customerId, request.Query, intent, extraction, utcNow, [], cancellationToken);
            return ApiResponse<FoodRecommendationV2Response>.SuccessResponse(empty);
        }

        var loaded = await candidates.GetCandidatesAsync(utcNow, Math.Clamp(_options.CandidateLimit, 1, 500), cancellationToken);
        AiV2Telemetry.Candidates.Add(loaded.Count);
        var ranked = new List<RankedRecommendationCandidate>();
        var diagnostics = new RecommendationDiagnosticsAccumulator(loaded.Count);
        intent.DistanceRankingEnabled = request.UseDistanceRanking && request.Latitude.HasValue;
        foreach (var candidate in loaded)
        {
            var distance = CalculateDistance(request.Latitude, request.Longitude, candidate.MarketLatitude, candidate.MarketLongitude);
            var rejection = RejectionReason(candidate, intent, request.Latitude.HasValue, distance, utcNow);
            diagnostics.Record(rejection);
            if (rejection is not null) continue;
            var semantic = semanticMatcher.Match(intent, candidate);
            ranked.Add(ranker.Rank(intent, candidate, semantic, distance));
        }
        var sortPreference = ResolveSortPreference(request.SortPreference, intent.SortPreference, request.Latitude.HasValue);
        var allReranked = diversity.Rerank(ranked, sortPreference).ToList();
        var reranked = allReranked.Where(value => value.Tier != RecommendationMatchTier.LOW_MATCH).ToList();
        if (reranked.Count == 0 && extraction.UsedFallback && allReranked.Count > 0)
        {
            // Provider failure must not turn an otherwise orderable, hard-filtered
            // catalogue into an empty screen. Promote a small diversified set to
            // near matches and clearly disclose that semantic precision is reduced.
            var fallbackScore = Math.Clamp(_options.NearMatchThreshold, 1m, 99m);
            reranked = allReranked.Take(Math.Clamp(request.PageSize * 2, 1, 20)).ToList();
            foreach (var value in reranked) { value.Breakdown.FinalScore = fallbackScore; value.Tier = RecommendationMatchTier.NEAR_MATCH; }
            intent.Warnings = intent.Warnings.Append("PROVIDER_FALLBACK_BROAD_RESULTS").Distinct(StringComparer.Ordinal).ToArray();
        }
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
        var responseWarnings = intent.Warnings.Concat(extraction.ValidationWarnings);
        if (!request.Latitude.HasValue) responseWarnings = responseWarnings.Append("LOCATION_NOT_PROVIDED_DISTANCE_UNAVAILABLE");
        var response = Response(sessionId, status, extraction.UsedFallback || usedExplanationFallback, intent, request, strongPage, nearPage,
            responseWarnings.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(), strong.Count, near.Count);
        response.ProviderRuntime = extraction.RuntimeTrace;
        var highestScore = ranked.Count == 0 ? 0m : ranked.Max(value => value.BaseScore);
        if (hostEnvironment.IsDevelopment())
            response.Diagnostics = diagnostics.ToResponse(intent, highestScore, strong.Count, near.Count);
        var persistable = reranked.Select((value, index) => new AiRecommendationResult
        {
            SessionId = sessionId, FoodItemId = value.Candidate.FoodId, Rank = index + 1,
            Score = value.BaseScore, MatchTier = value.Tier, CreatedAt = utcNow
        }).ToArray();
        await Persist(sessionId, customerId, request.Query, intent, extraction, utcNow, persistable, cancellationToken);
        if (hostEnvironment.IsDevelopment())
            logger.LogInformation("AI V2 recommendation diagnostics. SessionId={SessionId} DesiredFoodTerms={DesiredFoodTerms} OriginalNormalizedQuery={OriginalNormalizedQuery} TotalCandidates={TotalCandidates} EligibleCandidates={EligibleCandidates} FilteredByFoodStatus={FilteredByFoodStatus} FilteredByBoothStatus={FilteredByBoothStatus} FilteredByMarketStatus={FilteredByMarketStatus} FilteredByOpenHours={FilteredByOpenHours} FilteredByPrice={FilteredByPrice} FilteredByDistance={FilteredByDistance} FilteredByDietaryOrAllergen={FilteredByDietaryOrAllergen} HighestScore={HighestScore} Strong={StrongCount} Near={NearCount} TopRejectionReasons={TopRejectionReasons}",
                sessionId, string.Join('|', intent.DesiredFoodTerms), intent.OriginalNormalizedQuery, loaded.Count, diagnostics.EligibleCandidates,
                diagnostics.FilteredByFoodStatus, diagnostics.FilteredByBoothStatus, diagnostics.FilteredByMarketStatus,
                diagnostics.FilteredByOpenHours, diagnostics.FilteredByPrice, diagnostics.FilteredByDistance,
                diagnostics.FilteredByDietaryOrAllergen, highestScore, strong.Count, near.Count,
                string.Join(',', diagnostics.RejectionCounts.OrderByDescending(value => value.Value).ThenBy(value => value.Key).Select(value => $"{value.Key}:{value.Value}")));
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

    private static string? RejectionReason(FoodRecommendationCandidate value, FoodRecommendationIntent intent, bool hasLocation, int? distance, DateTime utcNow)
    {
        if (value.IsDeleted || value.CategoryDeleted || !value.CategoryIsActive || !value.CategoryIsSelectable
            || !value.IsAvailable) return "foodStatus";
        if (value.BoothStatus != BoothStatus.Active) return "boothStatus";
        if (value.MarketDeleted || value.MarketModerationStatus != ModerationStatus.Active || value.MarketStatus != NightMarketStatus.Active) return "marketStatus";
        var local = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(utcNow));
        if (!CustomerAvailability.IsOpenNow(true, value.MarketOpenTime, value.MarketCloseTime, value.BoothOpenTime, value.BoothCloseTime, local)) return "openHours";
        if (value.CurrentPrice <= 0 || intent.MaximumPrice.HasValue && value.CurrentPrice > intent.MaximumPrice
            || intent.MinimumPrice.HasValue && value.CurrentPrice < intent.MinimumPrice) return "price";
        if (intent.ExcludedIngredientCodes.Intersect(value.IngredientCodes, StringComparer.Ordinal).Any()) return "excludedIngredient";
        if (intent.AvoidedPreparationMethodCodes.Intersect(value.PreparationMethodCodes, StringComparer.Ordinal).Any()) return "avoidedPreparation";
        if (intent.DietaryRequirementCodes.Any(required => !value.DietaryAttributes.Any(attribute => attribute.Code == required
            && attribute.IsConfirmed && attribute.Status == DietarySuitabilityStatus.SUITABLE))) return "dietary";
        // The model has positive CONTAINS/MAY_CONTAIN declarations but no authoritative "free from" declaration.
        // Therefore both a matching declaration and an absent declaration are unsafe/unknown for an explicit exclusion.
        if (intent.AllergenExclusionCodes.Count > 0) return "allergenUnknown";
        if (hasLocation && intent.MaximumDistanceMeters.HasValue && (!distance.HasValue || distance > intent.MaximumDistanceMeters)) return "distance";
        return null;
    }

    private sealed class RecommendationDiagnosticsAccumulator(int totalCandidates)
    {
        public int TotalCandidates { get; } = totalCandidates;
        public int EligibleCandidates { get; private set; }
        public int FilteredByFoodStatus => Count("foodStatus");
        public int FilteredByBoothStatus => Count("boothStatus");
        public int FilteredByMarketStatus => Count("marketStatus");
        public int FilteredByOpenHours => Count("openHours");
        public int FilteredByPrice => Count("price");
        public int FilteredByDistance => Count("distance");
        public int FilteredByDietaryOrAllergen => RejectionCounts.Where(value => value.Key is "excludedIngredient" or "avoidedPreparation" or "dietary" or "allergenUnknown").Sum(value => value.Value);
        public Dictionary<string, int> RejectionCounts { get; } = new(StringComparer.Ordinal);

        public void Record(string? reason)
        {
            if (reason is null) { EligibleCandidates++; return; }
            RejectionCounts[reason] = RejectionCounts.GetValueOrDefault(reason) + 1;
        }

        public RecommendationDiagnosticsResponse ToResponse(FoodRecommendationIntent intent, decimal highestScore, int strongCount, int nearCount)
        {
            var afterFood = TotalCandidates - FilteredByFoodStatus;
            var afterBooth = afterFood - FilteredByBoothStatus;
            var afterMarket = afterBooth - FilteredByMarketStatus;
            var afterHours = afterMarket - FilteredByOpenHours;
            var afterPrice = afterHours - FilteredByPrice;
            var afterDistance = afterPrice - FilteredByDistance;
            return new()
            {
                DesiredFoodTerms = intent.DesiredFoodTerms, OriginalNormalizedQuery = intent.OriginalNormalizedQuery,
                TotalCandidates = TotalCandidates, EligibleCandidates = EligibleCandidates,
                FilteredByFoodStatus = FilteredByFoodStatus, FilteredByBoothStatus = FilteredByBoothStatus,
                FilteredByMarketStatus = FilteredByMarketStatus, FilteredByOpenHours = FilteredByOpenHours,
                FilteredByPrice = FilteredByPrice, FilteredByDistance = FilteredByDistance,
                FilteredByDietaryOrAllergen = FilteredByDietaryOrAllergen,
                RemainingAfterFoodStatus = afterFood, RemainingAfterBoothStatus = afterBooth,
                RemainingAfterMarketStatus = afterMarket, RemainingAfterOpenHours = afterHours,
                RemainingAfterPrice = afterPrice, RemainingAfterDistance = afterDistance,
                RemainingAfterDietaryOrAllergen = EligibleCandidates, HighestScore = highestScore,
                StrongCount = strongCount, NearCount = nearCount,
                TopRejectionReasons = RejectionCounts.OrderByDescending(value => value.Value).ThenBy(value => value.Key)
                    .Take(10).ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal)
            };
        }

        private int Count(string reason) => RejectionCounts.GetValueOrDefault(reason);
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
        request.MaxDistanceMeters ??= request.MaximumDistanceMeters;
        if (request.MaxDistanceMeters is <= 0 || request.MaxDistanceMeters > Math.Clamp(_options.MaximumDistanceMeters, 100, 500_000))
            throw AppException.BadRequest("Maximum distance is invalid.", "AI_INVALID_LOCATION");
        if (request.LocationAccuracyMeters is <= 0 or > 100_000)
            throw AppException.BadRequest("Location accuracy is invalid.", "AI_INVALID_LOCATION");
        if (request.MaximumPrice is <= 0 || request.MaximumPrice > Math.Clamp(_options.MaximumSupportedPrice, 1m, 1_000_000_000m))
            throw AppException.BadRequest("Maximum price is invalid.", "AI_INVALID_REQUEST");
        if (request.PreviousSessionId == Guid.Empty)
            throw AppException.BadRequest("Previous session is invalid.", "AI_INVALID_REQUEST");
        if (request.RemovedIntentSignals.Count > 30 || request.RemovedIntentSignals.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 100))
            throw AppException.BadRequest("Removed intent signals are invalid.", "AI_INVALID_REQUEST");
        if (request.Page < 1 || request.PageSize < 1 || request.PageSize > Math.Clamp(_options.MaximumPageSize, 1, 50))
            throw AppException.BadRequest("Paging is invalid.", "AI_INVALID_REQUEST");
        var allowedSorts = new[] { "BEST", "BEST_MATCH", "NEAREST", "NEAREST_RELEVANT", "PRICE", "LOWEST_PRICE", "LOWEST_PRICE_RELEVANT", "RATING", "HIGHEST_RATED_RELEVANT" };
        if (!string.IsNullOrWhiteSpace(request.SortPreference) && !allowedSorts.Contains(request.SortPreference.Trim(), StringComparer.OrdinalIgnoreCase))
            throw AppException.BadRequest("Sort preference is invalid.", "AI_INVALID_REQUEST");
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
        MarketDistanceMeters = value.DistanceMeters, DistanceAvailable = value.DistanceMeters.HasValue,
        RankingReasons = new[] { value.Evidence.Distance, value.Evidence.Budget, value.Evidence.Rating }.Where(reason => !string.IsNullOrWhiteSpace(reason)).Cast<string>().ToArray(),
        Booth = new() { Id = value.Candidate.BoothId, Name = value.Candidate.BoothName },
        Market = new() { Id = value.Candidate.MarketId, Name = value.Candidate.MarketName, DistanceMeters = value.DistanceMeters, DistanceAvailable = value.DistanceMeters.HasValue }
    };

    private static FoodRecommendationSortPreference ResolveSortPreference(string? requested, FoodRecommendationSortPreference fallback, bool hasLocation)
    {
        var parsed = requested?.Trim().ToUpperInvariant() switch
        {
            "NEAREST" or "NEAREST_RELEVANT" => FoodRecommendationSortPreference.NEAREST_RELEVANT,
            "PRICE" or "LOWEST_PRICE" or "LOWEST_PRICE_RELEVANT" => FoodRecommendationSortPreference.LOWEST_PRICE_RELEVANT,
            "RATING" or "HIGHEST_RATED_RELEVANT" => FoodRecommendationSortPreference.HIGHEST_RATED_RELEVANT,
            "BEST" or "BEST_MATCH" => FoodRecommendationSortPreference.BEST_MATCH,
            _ => fallback
        };
        return parsed == FoodRecommendationSortPreference.NEAREST_RELEVANT && !hasLocation ? fallback : parsed;
    }

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
            TastePreferences = intent.PreferredTasteCodes, AvoidedTasteProfiles = intent.AvoidedTasteCodes,
            PreparationPreferences = intent.PreparationMethodCodes, AvoidedPreparationMethods = intent.AvoidedPreparationMethodCodes,
            PreferredCourses = intent.PreferredCourseCodes, PreferredServingTemperatures = intent.PreferredServingTemperatures.Select(value => value.ToString()).ToArray(),
            MealPurposes = intent.MealPurposeCodes, SocialContext = intent.SocialContext, DesiredFullness = intent.DesiredFullness,
            PartySize = intent.PartySize, IsShareablePreferred = intent.IsShareablePreferred, TakeawayPreferred = intent.TakeawayPreferred,
            QuickServicePreferred = intent.QuickServicePreferred, HealthyPreference = intent.HealthyPreference,
            FreshPreference = intent.FreshPreference, PopularityPreference = intent.PopularityPreference,
            FreeTextContext = intent.ContextualTerms, UnmappedTerms = intent.UnmappedMeaningfulTerms,
            Confidence = intent.Confidence, ClarificationNeeded = intent.ClarificationNeeded, Ambiguities = intent.Ambiguities,
            Warnings = intent.Warnings, SignalEvidence = intent.SignalEvidence },
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

    private static FoodRecommendationIntent MergeFollowUp(FoodRecommendationIntent previous, FoodRecommendationIntent current)
    {
        static string[] Union(IEnumerable<string> before, IEnumerable<string> after) => before.Concat(after).Distinct(StringComparer.Ordinal).Take(20).ToArray();
        current.DesiredFoodTerms = Union(previous.DesiredFoodTerms, current.DesiredFoodTerms);
        current.ContextualTerms = Union(previous.ContextualTerms, current.ContextualTerms);
        current.UnmappedMeaningfulTerms = Union(previous.UnmappedMeaningfulTerms, current.UnmappedMeaningfulTerms);
        current.PreferredIngredientCodes = Union(previous.PreferredIngredientCodes, current.PreferredIngredientCodes);
        current.ExcludedIngredientCodes = Union(previous.ExcludedIngredientCodes, current.ExcludedIngredientCodes);
        current.AllergenExclusionCodes = Union(previous.AllergenExclusionCodes, current.AllergenExclusionCodes);
        current.DietaryRequirementCodes = Union(previous.DietaryRequirementCodes, current.DietaryRequirementCodes);
        current.PreferredTasteCodes = Union(previous.PreferredTasteCodes, current.PreferredTasteCodes);
        current.AvoidedTasteCodes = Union(previous.AvoidedTasteCodes, current.AvoidedTasteCodes);
        current.PreparationMethodCodes = Union(previous.PreparationMethodCodes, current.PreparationMethodCodes);
        current.AvoidedPreparationMethodCodes = Union(previous.AvoidedPreparationMethodCodes, current.AvoidedPreparationMethodCodes);
        current.PreferredCourseCodes = Union(previous.PreferredCourseCodes, current.PreferredCourseCodes);
        current.ExcludedCourseCodes = Union(previous.ExcludedCourseCodes, current.ExcludedCourseCodes);
        current.MealPurposeCodes = Union(previous.MealPurposeCodes, current.MealPurposeCodes);
        current.PreferredServingTemperatures = previous.PreferredServingTemperatures.Concat(current.PreferredServingTemperatures).Distinct().ToArray();
        current.PreferredSpiceLevel ??= previous.PreferredSpiceLevel; current.MinimumPrice ??= previous.MinimumPrice;
        current.MaximumPrice ??= previous.MaximumPrice; current.PartySize ??= previous.PartySize;
        current.SocialContext ??= previous.SocialContext; current.DesiredFullness ??= previous.DesiredFullness;
        current.IsShareablePreferred ??= previous.IsShareablePreferred; current.TakeawayPreferred ??= previous.TakeawayPreferred;
        current.QuickServicePreferred ??= previous.QuickServicePreferred; current.HealthyPreference ??= previous.HealthyPreference;
        current.FreshPreference ??= previous.FreshPreference; current.PopularityPreference ??= previous.PopularityPreference;
        current.PreferNearMe |= previous.PreferNearMe; current.MaximumDistanceMeters ??= previous.MaximumDistanceMeters;
        current.Ambiguities = Union(previous.Ambiguities, current.Ambiguities);
        current.ClarificationNeeded |= previous.ClarificationNeeded; current.Warnings = Union(previous.Warnings, current.Warnings);
        current.SignalEvidence = previous.SignalEvidence.Concat(current.SignalEvidence)
            .GroupBy(value => (value.Field, value.Value, value.Source)).Select(group => group.OrderByDescending(value => value.Confidence).First()).Take(100).ToArray();
        return current;
    }

    private static void ApplyRemovals(FoodRecommendationIntent intent, IEnumerable<string> removals)
    {
        var values = removals.Select(value => value.Trim().ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
        static string[] Without(IEnumerable<string> source, ISet<string> removed) => source.Where(value => !removed.Contains(value)).ToArray();
        intent.PreferredIngredientCodes = Without(intent.PreferredIngredientCodes, values); intent.ExcludedIngredientCodes = Without(intent.ExcludedIngredientCodes, values);
        intent.AllergenExclusionCodes = Without(intent.AllergenExclusionCodes, values); intent.DietaryRequirementCodes = Without(intent.DietaryRequirementCodes, values);
        intent.PreferredTasteCodes = Without(intent.PreferredTasteCodes, values); intent.AvoidedTasteCodes = Without(intent.AvoidedTasteCodes, values);
        intent.PreparationMethodCodes = Without(intent.PreparationMethodCodes, values); intent.AvoidedPreparationMethodCodes = Without(intent.AvoidedPreparationMethodCodes, values);
        intent.PreferredCourseCodes = Without(intent.PreferredCourseCodes, values); intent.ExcludedCourseCodes = Without(intent.ExcludedCourseCodes, values);
        intent.MealPurposeCodes = Without(intent.MealPurposeCodes, values);
        intent.PreferredServingTemperatures = intent.PreferredServingTemperatures.Where(value => !values.Contains($"TEMPERATURE_{value}")).ToArray();
        if (values.Contains("MAXIMUM_PRICE")) intent.MaximumPrice = null; if (values.Contains("PARTY_SIZE")) intent.PartySize = null;
        if (values.Contains("SHAREABLE")) intent.IsShareablePreferred = null; if (values.Contains("TAKEAWAY")) intent.TakeawayPreferred = null;
        if (values.Contains("QUICK_SERVICE")) intent.QuickServicePreferred = null;
    }
}

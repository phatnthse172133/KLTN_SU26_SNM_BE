using System.Text.RegularExpressions;
using ApplicationLayer.AI.V2.Configuration;
using ApplicationLayer.AI.V2.Models;
using ApplicationLayer.AI.V2.Recommendations;
using DomainLayer.Enums;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.AI.V2.Services;

public sealed class DeterministicFoodSemanticMatcher : IFoodSemanticMatcher
{
    private static readonly HashSet<string> StopWords = ["toi", "minh", "muon", "mon", "an", "cho", "va", "voi", "duoi", "khong", "gia", "gan", "nhe", "i", "want", "to", "eat", "a", "the", "something", "please"];
    public SemanticMatchResult Match(FoodRecommendationIntent intent, FoodRecommendationCandidate candidate)
    {
        var haystack = DeterministicFoodIntentParser.NormalizeText(string.Join(' ', candidate.FoodName, candidate.CategoryName,
            candidate.CategoryCode, candidate.Description, candidate.SearchText, string.Join(' ', candidate.IngredientCodes),
            string.Join(' ', candidate.TasteCodes), string.Join(' ', candidate.PreparationMethodCodes),
            string.Join(' ', candidate.Courses), string.Join(' ', candidate.DiningPurposes), candidate.SpiceLevel,
            candidate.ServingTemperature, candidate.ServingTemperature == ServingTemperature.COLD ? "cold cool refreshing thanh mat" : null,
            candidate.IsShareable == true ? "shareable shared friend group" : null,
            candidate.EstimatedServingCount >= 2 ? "serves group shared" : null));
        var desired = intent.DesiredFoodTerms.Select(DeterministicFoodIntentParser.NormalizeText).Where(value => value.Length > 0).Distinct().ToArray();
        var matchedDesired = desired.Where(term => haystack.Contains(term, StringComparison.Ordinal)).ToArray();
        var searchQuery = string.Join(' ', intent.OriginalNormalizedQuery, string.Join(' ', intent.ContextualTerms), string.Join(' ', intent.UnmappedMeaningfulTerms));
        var queryTokens = Tokens(searchQuery).Where(token => !StopWords.Contains(token)).Distinct().Take(30).ToArray();
        var haystackTokens = Tokens(haystack).ToHashSet(StringComparer.Ordinal);
        var matchedTokens = queryTokens.Where(haystackTokens.Contains).ToArray();
        var contextTokens = intent.ContextualTerms.SelectMany(Tokens).Distinct().ToArray();
        var matchedContext = contextTokens.Where(haystackTokens.Contains).ToArray();
        var desiredRatio = desired.Length == 0 ? 0m : matchedDesired.Length / (decimal)desired.Length;
        var lexicalRatio = queryTokens.Length == 0 ? 0m : matchedTokens.Length / (decimal)queryTokens.Length;
        var contextRatio = contextTokens.Length == 0 ? 0m : matchedContext.Length / (decimal)contextTokens.Length;
        lexicalRatio = Math.Max(lexicalRatio, contextRatio);
        var score = desired.Length == 0 ? lexicalRatio : desiredRatio * .6m + lexicalRatio * .4m;
        return new(Math.Clamp(score, 0m, 1m),
            matchedDesired.Concat(matchedTokens).Concat(matchedContext).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }
    // Enum/code metadata uses separators (FULL_MEAL, MAIN_COURSE). Treat them as
    // token boundaries so natural-language context can match normalized metadata.
    private static IEnumerable<string> Tokens(string value) => Regex.Matches(value, @"[\p{L}\p{Nd}]+", RegexOptions.CultureInvariant).Select(match => match.Value);

}

public sealed class FoodRecommendationRanker(IOptions<RecommendationV2Options> options) : IFoodRecommendationRanker
{
    private readonly RecommendationV2Options _options = options.Value;
    public RankedRecommendationCandidate Rank(FoodRecommendationIntent intent, FoodRecommendationCandidate candidate, SemanticMatchResult semantic, int? distanceMeters)
    {
        var ingredients = Intersect(intent.PreferredIngredientCodes, candidate.IngredientCodes);
        var tastes = Intersect(intent.PreferredTasteCodes, candidate.TasteCodes);
        var methods = Intersect(intent.PreparationMethodCodes, candidate.PreparationMethodCodes);
        var courseCodes = candidate.Courses.Select(value => value.ToString()).ToArray();
        var purposes = candidate.DiningPurposes.Select(value => value.ToString()).ToArray();
        var courseMatches = Intersect(intent.PreferredCourseCodes, courseCodes)
            .Concat(Intersect(intent.MealPurposeCodes, purposes))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var spiceMatch = intent.PreferredSpiceLevel.HasValue && intent.PreferredSpiceLevel == candidate.SpiceLevel;

        var semanticRatio = semantic.Score;
        var nameCategoryRatio = NameCategoryScore(intent, candidate, courseMatches);
        var ingredientRatio = Ratio(ingredients.Count, intent.PreferredIngredientCodes.Count);
        var tasteSignals = intent.PreferredTasteCodes.Count + (intent.PreferredSpiceLevel.HasValue ? 1 : 0);
        var tasteMatches = tastes.Count + (spiceMatch ? 1 : 0);
        var tasteRatio = Ratio(tasteMatches, tasteSignals);
        if (intent.AvoidedTasteCodes.Intersect(candidate.TasteCodes, StringComparer.Ordinal).Any()) tasteRatio = Math.Max(0, tasteRatio - .3m);
        var preparationRatio = Ratio(methods.Count, intent.PreparationMethodCodes.Count);
        var courseRatio = Ratio(courseMatches.Length, intent.PreferredCourseCodes.Count + intent.MealPurposeCodes.Count);
        var temperatureMatch = intent.PreferredServingTemperatures.Count > 0 && candidate.ServingTemperature.HasValue
            && intent.PreferredServingTemperatures.Contains(candidate.ServingTemperature.Value);
        var temperatureRatio = intent.PreferredServingTemperatures.Count == 0 ? 0m : temperatureMatch ? 1m : 0m;
        var shareSignals = (intent.IsShareablePreferred == true ? 1 : 0) + (intent.PartySize.HasValue ? 1 : 0);
        var shareMatches = (intent.IsShareablePreferred == true && candidate.IsShareable == true ? 1 : 0)
            + (intent.PartySize.HasValue && candidate.EstimatedServingCount >= intent.PartySize ? 1 : 0);
        var shareRatio = Ratio(shareMatches, shareSignals);
        var budgetRatio = BudgetScore(intent, candidate.CurrentPrice);
        var dietaryMatches = candidate.DietaryAttributes.Count(value => intent.DietaryRequirementCodes.Contains(value.Code)
            && value.IsConfirmed && value.Status == DietarySuitabilityStatus.SUITABLE);
        var dietaryRatio = Ratio(dietaryMatches, intent.DietaryRequirementCodes.Count);
        var distanceRatio = DistanceScore(intent, distanceMeters);
        var ratingRatio = RatingScore(candidate.Rating, candidate.ReviewCount);

        decimal earned = 0, applicable = 0;
        void Add(decimal ratio, decimal weight) { earned += Math.Clamp(ratio, 0m, 1m) * weight; applicable += weight; }
        var hasLexicalQuery = intent.OriginalNormalizedQuery.Length > 0 || intent.ContextualTerms.Count > 0 || intent.UnmappedMeaningfulTerms.Count > 0;
        if (hasLexicalQuery) Add(semanticRatio, intent.DesiredFoodTerms.Count > 0 ? 30m : 55m);
        if (intent.DesiredFoodTerms.Count > 0) Add(nameCategoryRatio, 40m);
        if (intent.PreferredIngredientCodes.Count > 0) Add(ingredientRatio, 15m);
        if (tasteSignals > 0) Add(tasteRatio, 10m);
        if (intent.PreparationMethodCodes.Count > 0) Add(preparationRatio, 8m);
        if (intent.PreferredCourseCodes.Count + intent.MealPurposeCodes.Count > 0) Add(courseRatio, 10m);
        if (intent.PreferredServingTemperatures.Count > 0) Add(temperatureRatio, 10m);
        if (shareSignals > 0) Add(shareRatio, 10m);
        if (intent.MinimumPrice.HasValue || intent.MaximumPrice.HasValue) Add(budgetRatio, 10m);
        if (intent.DietaryRequirementCodes.Count > 0) Add(dietaryRatio, 15m);
        if (distanceMeters.HasValue && intent.DistanceRankingEnabled) Add(distanceRatio, intent.PreferNearMe ? 15m : 6m);
        if (intent.PopularityPreference == true || intent.ContextualTerms.Contains("popular", StringComparer.Ordinal)
            || intent.SortPreference == FoodRecommendationSortPreference.HIGHEST_RATED_RELEVANT) Add(ratingRatio, 20m);
        var final = Round(applicable == 0 ? 0 : earned / applicable * 100m);
        var semanticPoints = Round(semanticRatio * 30m);
        var nameCategoryPoints = Round(nameCategoryRatio * 40m);
        var ingredientPoints = Round(ingredientRatio * 15m);
        var tastePoints = Round(tasteRatio * 10m);
        var preparationPoints = Round(preparationRatio * 8m);
        var coursePoints = Round(courseRatio * 10m);
        var budgetPoints = Round(budgetRatio * 10m);
        var dietaryPoints = Round(dietaryRatio * 15m);
        var distancePoints = intent.DistanceRankingEnabled ? Round(distanceRatio * (intent.PreferNearMe ? 15m : 6m)) : 0m;
        var ratingPoints = Round(ratingRatio * 5m);
        var strong = Math.Clamp(_options.StrongMatchThreshold, 1m, 100m);
        var near = Math.Clamp(_options.NearMatchThreshold, 0m, strong);
        var tier = final >= strong ? RecommendationMatchTier.STRONG_MATCH : final >= near ? RecommendationMatchTier.NEAR_MATCH : RecommendationMatchTier.LOW_MATCH;
        return new RankedRecommendationCandidate
        {
            Candidate = candidate, DistanceMeters = distanceMeters, Tier = tier,
            Breakdown = new RecommendationScoreBreakdown
            {
                SemanticTextScore = semanticPoints, FoodNameCategoryScore = Round(nameCategoryPoints + coursePoints),
                IngredientScore = Round(ingredientPoints), TasteSpiceScore = Round(tastePoints), PreparationScore = Round(preparationPoints),
                BudgetScore = Round(budgetPoints), DietaryScore = Round(dietaryPoints), DistanceScore = Round(distancePoints),
                RatingScore = Round(ratingPoints), CustomerHistoryScore = 0, FinalScore = final
            },
            Evidence = new RecommendationReasonEvidence
            {
                DesiredTerms = semantic.MatchedTerms, Ingredients = ingredients, TastesAndSpice = tastes.Concat(spiceMatch ? [candidate.SpiceLevel.ToString()] : []).ToArray(),
                Preparations = methods, CoursesAndPurposes = courseMatches
                    .Concat(temperatureMatch ? [candidate.ServingTemperature!.Value.ToString()] : [])
                    .Concat(intent.IsShareablePreferred == true && candidate.IsShareable == true ? ["SHAREABLE"] : [])
                    .Concat(intent.PartySize.HasValue && candidate.EstimatedServingCount >= intent.PartySize ? [$"SERVES_{candidate.EstimatedServingCount}"] : [])
                    .ToArray(),
                Dietary = candidate.DietaryAttributes.Where(value => intent.DietaryRequirementCodes.Contains(value.Code) && value.IsConfirmed && value.Status == DietarySuitabilityStatus.SUITABLE).Select(value => value.Code).ToArray(),
                Budget = intent.MaximumPrice.HasValue ? $"giá {candidate.CurrentPrice:0} nằm trong ngân sách {intent.MaximumPrice.Value:0}" : null,
                Distance = distanceMeters.HasValue ? $"cách khoảng {distanceMeters.Value} m" : null,
                Rating = candidate.Rating.HasValue && candidate.ReviewCount > 0 ? $"điểm {candidate.Rating.Value:0.0} từ {candidate.ReviewCount} đánh giá" : null
            }
        };
    }

    private static decimal NameCategoryScore(FoodRecommendationIntent intent, FoodRecommendationCandidate candidate, IReadOnlyCollection<string> courseMatches)
    {
        var name = DeterministicFoodIntentParser.NormalizeText(candidate.FoodName);
        var text = DeterministicFoodIntentParser.NormalizeText($"{candidate.FoodName} {candidate.CategoryName} {candidate.CategoryCode}");
        foreach (var rawTerm in intent.DesiredFoodTerms)
        {
            var term = DeterministicFoodIntentParser.NormalizeText(rawTerm);
            if (term == name) return 1m;
            if (name.Contains(term, StringComparison.Ordinal)) return .9m;
            if (text.Contains(term, StringComparison.Ordinal)) return .7m;
        }
        if (courseMatches.Count > 0) return .7m;
        return 0m;
    }
    private static decimal BudgetScore(FoodRecommendationIntent intent, decimal price)
    {
        if (!intent.MinimumPrice.HasValue && !intent.MaximumPrice.HasValue) return 0m;
        var target = intent.MaximumPrice ?? intent.MinimumPrice!.Value;
        var closeness = target <= 0 ? 0m : 1m - Math.Min(1m, Math.Abs(target - price) / target);
        return closeness;
    }
    private static decimal DistanceScore(FoodRecommendationIntent intent, int? distance)
    {
        if (!distance.HasValue) return 0m;
        var scale = intent.MaximumDistanceMeters.GetValueOrDefault(10_000);
        if (scale <= 0) return 0m;
        return Math.Clamp(1m - distance.Value / (decimal)scale, 0m, 1m);
    }
    private static decimal RatingScore(decimal? rating, int reviews)
    {
        if (!rating.HasValue || reviews <= 0) return 0m;
        const decimal prior = 4m; const decimal weight = 10m;
        var bayesian = (rating.Value * reviews + prior * weight) / (reviews + weight);
        return Math.Clamp((bayesian - 1m) / 4m, 0m, 1m);
    }
    private static IReadOnlyCollection<string> Intersect(IEnumerable<string> requested, IEnumerable<string> actual)
        => requested.Intersect(actual, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    private static decimal Ratio(int matches, int signals) => signals == 0 ? 0m : Math.Clamp(matches / (decimal)signals, 0m, 1m);
    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed class FoodRecommendationDiversityReranker(IOptions<RecommendationV2Options> options) : IFoodRecommendationDiversityReranker
{
    private readonly RecommendationV2Options _options = options.Value;
    public IReadOnlyCollection<RankedRecommendationCandidate> Rerank(IReadOnlyCollection<RankedRecommendationCandidate> candidates, FoodRecommendationSortPreference sortPreference)
    {
        var remaining = Ordered(candidates, sortPreference).ToList();
        var result = new List<RankedRecommendationCandidate>(remaining.Count);
        var boothCounts = new Dictionary<Guid, int>();
        var categoryCounts = new Dictionary<Guid, int>();
        var maximum = Math.Clamp(_options.MaximumSameBoothInTopResults, 1, 10);
        var categoryMaximum = Math.Clamp(_options.MaximumSameCategoryInTopResults, 1, 10);
        var window = Math.Clamp(_options.DiversityScoreWindow, 0m, 10m);
        while (remaining.Count > 0)
        {
            var first = remaining[0]; var selected = first;
            if (boothCounts.GetValueOrDefault(first.Candidate.BoothId) >= maximum
                || categoryCounts.GetValueOrDefault(first.Candidate.CategoryId) >= categoryMaximum)
            {
                selected = remaining.FirstOrDefault(value => boothCounts.GetValueOrDefault(value.Candidate.BoothId) < maximum
                    && categoryCounts.GetValueOrDefault(value.Candidate.CategoryId) < categoryMaximum
                    && value.Tier == first.Tier && Math.Abs(first.BaseScore - value.BaseScore) <= window) ?? first;
                if (selected != first) selected.Breakdown.DiversityAdjustment = Round(first.BaseScore - selected.BaseScore);
            }
            result.Add(selected); remaining.Remove(selected);
            boothCounts[selected.Candidate.BoothId] = boothCounts.GetValueOrDefault(selected.Candidate.BoothId) + 1;
            categoryCounts[selected.Candidate.CategoryId] = categoryCounts.GetValueOrDefault(selected.Candidate.CategoryId) + 1;
        }
        return result;
    }
    private IEnumerable<RankedRecommendationCandidate> Ordered(IEnumerable<RankedRecommendationCandidate> values, FoodRecommendationSortPreference preference)
    {
        var bandSize = Math.Max(1m, Math.Clamp(_options.DiversityScoreWindow, 0m, 10m));
        var ordered = values.OrderByDescending(value => value.Tier == RecommendationMatchTier.STRONG_MATCH ? 2
                : value.Tier == RecommendationMatchTier.NEAR_MATCH ? 1 : 0)
            .ThenByDescending(value => Math.Floor(value.BaseScore / bandSize));
        return preference switch
        {
            FoodRecommendationSortPreference.NEAREST_RELEVANT => ordered.ThenBy(value => value.DistanceMeters ?? int.MaxValue).ThenByDescending(value => value.BaseScore).ThenBy(value => value.Candidate.FoodId),
            FoodRecommendationSortPreference.HIGHEST_RATED_RELEVANT => ordered.ThenByDescending(value => value.Candidate.Rating ?? decimal.MinValue).ThenByDescending(value => value.Candidate.ReviewCount).ThenByDescending(value => value.BaseScore).ThenBy(value => value.Candidate.FoodId),
            FoodRecommendationSortPreference.LOWEST_PRICE_RELEVANT => ordered.ThenBy(value => value.Candidate.CurrentPrice).ThenByDescending(value => value.BaseScore).ThenBy(value => value.Candidate.FoodId),
            _ => ordered.ThenByDescending(value => value.BaseScore).ThenBy(value => value.Candidate.FoodId)
        };
    }
    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

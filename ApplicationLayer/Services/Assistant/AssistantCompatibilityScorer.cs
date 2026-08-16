using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.NightMarkets;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services.Assistant;

public sealed class AssistantScoredFood
{
    public required AssistantEligibleFood Eligible { get; init; }
    public double FinalScore { get; init; }
    public double SemanticScore { get; init; }
    public double StructuredScore { get; init; }
    public double PriceScore { get; init; }
    public double RatingScore { get; init; }
    public double DistanceScore { get; init; }
    public bool IsOpenNow { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = [];
    public IReadOnlyList<string> UnknownDataFacets { get; init; } = [];
}

public sealed class AssistantCompatibilityScorer(IOptions<AssistantOptions> options)
{
    private readonly AssistantOptions _options = options.Value;

    public IReadOnlyList<AssistantScoredFood> Score(
        IReadOnlyList<AssistantEligibleFood> eligible,
        ParsedAssistantIntent intent,
        CustomerFoodProfile? profile,
        AssistantSemanticMatchResult semantic,
        DateTime utcNow)
    {
        var hasGps = eligible.Any(item => item.DistanceMeters.HasValue);
        var weights = hasGps ? CurrentWeights() : RedistributeWithoutDistance();
        var preferredIngredients = Union(
            profile?.PreferredIngredients.Select(item => item.Ingredient.Code),
            intent.StructuredPreferences.PreferredIngredientCodes);
        var preferredTastes = Union(
            profile?.PreferredTasteProfiles.Select(item => item.TasteProfile.Code),
            intent.StructuredPreferences.PreferredTasteCodes);
        var preferredPrep = Union(
            profile?.PreferredPreparationMethods.Select(item => item.PreparationMethod.Code),
            intent.StructuredPreferences.PreferredPreparationCodes);
        var preferredCourses = Union(
            profile?.PreferredCourses.Select(item => item.Course.ToString()),
            intent.StructuredPreferences.PreferredCourseCodes);
        var budgetMin = intent.BudgetMin ?? profile?.PreferredPriceMin;
        var budgetMax = intent.BudgetMax ?? profile?.PreferredPriceMax;
        var localTime = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(utcNow));

        var scored = new List<AssistantScoredFood>(eligible.Count);
        foreach (var item in eligible)
        {
            semantic.Scores.TryGetValue(item.FoodItem.Id, out var match);
            var semanticScore = match?.SemanticCompatibility ?? 0d;
            var structured = StructuredOverlap(item.FoodItem, preferredIngredients, preferredTastes, preferredPrep, preferredCourses);
            var price = PriceFit(item.EffectivePrice, budgetMin, budgetMax);
            var rating = item.FoodItem.ReviewCount <= 0 ? 0d : (double)item.FoodItem.AverageRating / 5d;
            var distance = DistanceFit(item.DistanceMeters, _options.DefaultMaxDistanceMeters);
            var openNow = IsOpenNow(item.FoodItem, localTime);
            var featured = item.FoodItem.IsFeatured ? 1d : 0d;
            var promo = item.HasActivePromotion ? 1d : 0d;
            var final =
                weights.Semantic * semanticScore
                + weights.Structured * structured
                + weights.Price * price
                + weights.Rating * rating
                + weights.Distance * distance
                + weights.Featured * featured
                + weights.OpenNow * (openNow ? 1d : 0d)
                + weights.Promo * promo;

            var reasons = new List<string>(match?.Reasons ?? []);
            foreach (var facet in match?.UnknownDataFacets ?? [])
            {
                if (!reasons.Any(reason => reason.Contains(facet, StringComparison.OrdinalIgnoreCase)
                    || reason.Contains("MISSING_DB_FIELD", StringComparison.OrdinalIgnoreCase)))
                    reasons.Add($"MISSING_DB_FIELD:{facet}");
            }

            scored.Add(new AssistantScoredFood
            {
                Eligible = item,
                FinalScore = Math.Clamp(final, 0d, 1d),
                SemanticScore = semanticScore,
                StructuredScore = structured,
                PriceScore = price,
                RatingScore = rating,
                DistanceScore = distance,
                IsOpenNow = openNow,
                Reasons = reasons,
                UnknownDataFacets = match?.UnknownDataFacets ?? []
            });
        }

        return scored
            .Where(item => item.FinalScore >= _options.MinimumCompatibilityScore)
            .OrderByDescending(item => item.FinalScore)
            .ThenBy(item => item.Eligible.FoodItem.Name)
            .ThenBy(item => item.Eligible.FoodItem.Id)
            .ToArray();
    }

    private (double Semantic, double Structured, double Price, double Rating, double Distance, double Featured, double OpenNow, double Promo) CurrentWeights()
        => (_options.SemanticWeight, _options.StructuredPreferenceWeight, _options.PriceWeight, _options.RatingWeight,
            _options.DistanceWeight, _options.FeaturedWeight, _options.OpenNowWeight, _options.PromoWeight);

    private (double Semantic, double Structured, double Price, double Rating, double Distance, double Featured, double OpenNow, double Promo) RedistributeWithoutDistance()
    {
        var rest = _options.SemanticWeight + _options.StructuredPreferenceWeight + _options.PriceWeight + _options.RatingWeight
            + _options.FeaturedWeight + _options.OpenNowWeight + _options.PromoWeight;
        var scale = rest <= 0 ? 1d : 1d / rest;
        return (
            _options.SemanticWeight * scale,
            _options.StructuredPreferenceWeight * scale,
            _options.PriceWeight * scale,
            _options.RatingWeight * scale,
            0d,
            _options.FeaturedWeight * scale,
            _options.OpenNowWeight * scale,
            _options.PromoWeight * scale);
    }

    private static double StructuredOverlap(
        FoodItem food,
        HashSet<string> ingredients,
        HashSet<string> tastes,
        HashSet<string> prep,
        HashSet<string> courses)
    {
        var wanted = ingredients.Count + tastes.Count + prep.Count + courses.Count;
        if (wanted == 0) return 0d;
        var hits = 0;
        if (ingredients.Count > 0)
            hits += food.Ingredients.Count(item => ingredients.Contains(item.Ingredient.Code));
        if (tastes.Count > 0)
            hits += food.TasteProfiles.Count(item => tastes.Contains(item.TasteProfile.Code));
        if (prep.Count > 0)
            hits += food.PreparationMethods.Count(item => prep.Contains(item.PreparationMethod.Code));
        if (courses.Count > 0)
            hits += food.Courses.Count(item => courses.Contains(item.Course.ToString()));
        return Math.Clamp((double)hits / wanted, 0d, 1d);
    }

    private static double PriceFit(decimal price, decimal? min, decimal? max)
    {
        if (min is null && max is null) return 1d;
        var low = min ?? 0m;
        var high = max ?? Math.Max(price, low);
        if (high <= low) return price <= high ? 1d : 0d;
        var mid = (low + high) / 2m;
        var span = high - low;
        var distance = Math.Abs(price - mid);
        return Math.Clamp(1d - (double)(distance / span), 0d, 1d);
    }

    private static double DistanceFit(double? meters, int maxDistance)
    {
        if (!meters.HasValue || maxDistance <= 0) return 0d;
        return Math.Clamp(1d - meters.Value / maxDistance, 0d, 1d);
    }

    private static bool IsOpenNow(FoodItem food, TimeOnly localTime)
        => CustomerAvailability.IsOpenNow(
            food.Booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Active,
            food.Booth.NightMarket.OpeningHours,
            food.Booth.NightMarket.ClosingHours,
            food.Booth.OpenTime,
            food.Booth.CloseTime,
            localTime);

    private static HashSet<string> Union(IEnumerable<string>? left, IEnumerable<string>? right)
        => (left ?? []).Concat(right ?? []).Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

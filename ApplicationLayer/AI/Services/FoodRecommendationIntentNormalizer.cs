using ApplicationLayer.AI.V2.Configuration;
using ApplicationLayer.AI.V2.Models;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.AI.V2.Services;

public sealed class FoodRecommendationIntentNormalizer(IOptions<RecommendationV2Options> options) : IFoodRecommendationIntentNormalizer
{
    private const int MaximumArrayCount = 20;
    private readonly RecommendationV2Options _options = options.Value;

    public FoodRecommendationIntentNormalizationResult Normalize(FoodRecommendationIntent raw, FoodRecommendationNormalizationContext context)
    {
        var warnings = new HashSet<string>(raw.Warnings ?? [], StringComparer.Ordinal);
        var errors = new List<string>();
        var catalogs = context.Catalogs;
        var ingredients = CatalogCodes(raw.PreferredIngredientCodes, catalogs.Ingredients, "INGREDIENT", warnings);
        var excludedIngredients = CatalogCodes(raw.ExcludedIngredientCodes, catalogs.Ingredients, "INGREDIENT", warnings);
        var allergens = CatalogCodes(raw.AllergenExclusionCodes, catalogs.Allergens, "ALLERGEN", warnings);
        var dietary = CatalogCodes(raw.DietaryRequirementCodes, catalogs.DietaryAttributes, "DIETARY", warnings);
        var tastes = CatalogCodes(raw.PreferredTasteCodes, catalogs.TasteProfiles, "TASTE", warnings);
        var avoidedTastes = CatalogCodes(raw.AvoidedTasteCodes, catalogs.TasteProfiles, "TASTE", warnings);
        var methods = CatalogCodes(raw.PreparationMethodCodes, catalogs.PreparationMethods, "PREPARATION", warnings);
        var avoidedMethods = CatalogCodes(raw.AvoidedPreparationMethodCodes, catalogs.PreparationMethods, "PREPARATION", warnings);
        var courses = EnumCodes<FoodCourse>(raw.PreferredCourseCodes, "COURSE", warnings);
        var purposes = EnumCodes<DiningPurpose>(raw.MealPurposeCodes, "DINING_PURPOSE", warnings);
        MergeProfile(context.CustomerProfile, ingredients, excludedIngredients, allergens, dietary, tastes, avoidedTastes, methods, courses, purposes);
        ingredients.ExceptWith(excludedIngredients); tastes.ExceptWith(avoidedTastes); methods.ExceptWith(avoidedMethods);

        var minimum = Price(raw.MinimumPrice, "MINIMUM_PRICE", warnings);
        var parsedMaximum = Price(raw.MaximumPrice, "MAXIMUM_PRICE", warnings);
        var explicitMaximum = Price(context.ExplicitMaximumPrice, "EXPLICIT_MAXIMUM_PRICE", warnings);
        var maximum = explicitMaximum.HasValue && parsedMaximum.HasValue ? Math.Min(explicitMaximum.Value, parsedMaximum.Value) : explicitMaximum ?? parsedMaximum;
        if (minimum.HasValue && maximum.HasValue && minimum > maximum) errors.Add("AI_PRICE_RANGE_CONFLICT");

        var distance = context.ExplicitMaximumDistanceMeters ?? PositiveDistance(raw.MaximumDistanceMeters, warnings)
            ?? (raw.PreferNearMe ? context.CustomerProfile?.DefaultMaxDistanceMeters : null);
        var distanceLimit = Math.Clamp(_options.MaximumDistanceMeters, 100, 500_000);
        if (distance > distanceLimit) { distance = distanceLimit; warnings.Add("MAXIMUM_DISTANCE_CLAMPED"); }
        if (distance.HasValue && !context.HasLocation) warnings.Add("LOCATION_REQUIRED_FOR_DISTANCE_CONSTRAINT");

        if (raw.PreferredSpiceLevel.HasValue && !Enum.IsDefined(raw.PreferredSpiceLevel.Value)) errors.Add("AI_INVALID_SPICE_LEVEL");
        if (!Enum.IsDefined(raw.SortPreference)) errors.Add("AI_INVALID_SORT_PREFERENCE");
        var queryLimit = Math.Clamp(_options.MaximumQueryCharacters, 100, 4000);
        var summary = string.IsNullOrWhiteSpace(raw.Summary) ? context.Query.Trim() : raw.Summary.Trim();
        if (summary.Length > queryLimit) summary = summary[..queryLimit];
        var intent = new FoodRecommendationIntent
        {
            Summary = summary,
            DesiredFoodTerms = (raw.DesiredFoodTerms ?? []).Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(DeterministicFoodIntentParser.NormalizeText).Where(value => value.Length is > 0 and <= 100)
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).Take(MaximumArrayCount).ToArray(),
            PreferredIngredientCodes = Ordered(ingredients), ExcludedIngredientCodes = Ordered(excludedIngredients),
            AllergenExclusionCodes = Ordered(allergens), DietaryRequirementCodes = Ordered(dietary),
            PreferredTasteCodes = Ordered(tastes), AvoidedTasteCodes = Ordered(avoidedTastes),
            PreferredSpiceLevel = raw.PreferredSpiceLevel ?? context.CustomerProfile?.PreferredSpiceLevel,
            PreparationMethodCodes = Ordered(methods), AvoidedPreparationMethodCodes = Ordered(avoidedMethods),
            PreferredCourseCodes = Ordered(courses), MealPurposeCodes = Ordered(purposes), MinimumPrice = minimum, MaximumPrice = maximum,
            PreferNearMe = raw.PreferNearMe, MaximumDistanceMeters = distance,
            SortPreference = Enum.IsDefined(raw.SortPreference) ? raw.SortPreference : FoodRecommendationSortPreference.BEST_MATCH,
            Confidence = Math.Clamp(raw.Confidence, 0m, 1m), Warnings = warnings.OrderBy(value => value, StringComparer.Ordinal).ToArray()
        };
        if (!HasSignal(intent)) errors.Add("AI_FALLBACK_PARSE_INSUFFICIENT");
        return new() { IsValid = errors.Count == 0, Intent = errors.Count == 0 ? intent : null,
            Warnings = intent.Warnings, Errors = errors.Distinct(StringComparer.Ordinal).ToArray() };
    }

    private static HashSet<string> CatalogCodes<T>(IEnumerable<string>? values, IEnumerable<T> catalog, string label, ISet<string> warnings) where T : SemanticCatalogEntity
    {
        var allowed = catalog.Where(value => value.IsActive).Select(value => value.Code).ToHashSet(StringComparer.Ordinal);
        var result = RawCodes(values);
        foreach (var unknown in result.Where(value => !allowed.Contains(value)).ToArray()) { result.Remove(unknown); warnings.Add($"UNKNOWN_{label}_CODE:{unknown}"); }
        return result;
    }
    private static HashSet<string> EnumCodes<T>(IEnumerable<string>? values, string label, ISet<string> warnings) where T : struct, Enum
    {
        var result = RawCodes(values);
        foreach (var unknown in result.Where(value => !Enum.TryParse<T>(value, false, out _)).ToArray()) { result.Remove(unknown); warnings.Add($"UNKNOWN_{label}_CODE:{unknown}"); }
        return result;
    }
    private static HashSet<string> RawCodes(IEnumerable<string>? values) => (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim().ToUpperInvariant()).Where(value => value.Length <= 100).Distinct(StringComparer.Ordinal).Take(MaximumArrayCount).ToHashSet(StringComparer.Ordinal);

    private static void MergeProfile(CustomerFoodProfile? profile, ISet<string> ingredients, ISet<string> excludedIngredients,
        ISet<string> allergens, ISet<string> dietary, ISet<string> tastes, ISet<string> avoidedTastes,
        ISet<string> methods, ISet<string> courses, ISet<string> purposes)
    {
        if (profile is null) return;
        foreach (var value in profile.PreferredIngredients) ingredients.Add(value.Ingredient.Code);
        foreach (var value in profile.AvoidedIngredients) excludedIngredients.Add(value.Ingredient.Code);
        foreach (var value in profile.AllergenExclusions) allergens.Add(value.Allergen.Code);
        foreach (var value in profile.DietaryRequirements) dietary.Add(value.DietaryAttribute.Code);
        foreach (var value in profile.PreferredTasteProfiles) tastes.Add(value.TasteProfile.Code);
        foreach (var value in profile.AvoidedTasteProfiles) avoidedTastes.Add(value.TasteProfile.Code);
        foreach (var value in profile.PreferredPreparationMethods) methods.Add(value.PreparationMethod.Code);
        foreach (var value in profile.PreferredCourses) courses.Add(value.Course.ToString());
        foreach (var value in profile.PreferredDiningPurposes) purposes.Add(value.Purpose.ToString());
    }

    private decimal? Price(decimal? value, string label, ISet<string> warnings)
    {
        if (!value.HasValue) return null;
        if (value <= 0) { warnings.Add($"{label}_REMOVED"); return null; }
        var maximum = Math.Clamp(_options.MaximumSupportedPrice, 1m, 1_000_000_000m);
        if (value > maximum) { warnings.Add($"{label}_CLAMPED"); return maximum; }
        return value;
    }
    private static int? PositiveDistance(int? value, ISet<string> warnings)
    { if (!value.HasValue) return null; if (value <= 0) { warnings.Add("MAXIMUM_DISTANCE_REMOVED"); return null; } return value; }
    private static string[] Ordered(IEnumerable<string> values) => values.OrderBy(value => value, StringComparer.Ordinal).Take(MaximumArrayCount).ToArray();
    private static bool HasSignal(FoodRecommendationIntent value) => value.DesiredFoodTerms.Count + value.PreferredIngredientCodes.Count
        + value.ExcludedIngredientCodes.Count + value.AllergenExclusionCodes.Count + value.DietaryRequirementCodes.Count
        + value.PreferredTasteCodes.Count + value.AvoidedTasteCodes.Count + value.PreparationMethodCodes.Count
        + value.AvoidedPreparationMethodCodes.Count + value.PreferredCourseCodes.Count + value.MealPurposeCodes.Count > 0
        || value.PreferredSpiceLevel.HasValue || value.MinimumPrice.HasValue || value.MaximumPrice.HasValue || value.PreferNearMe;
}

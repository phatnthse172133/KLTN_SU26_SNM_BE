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
        var excludedCourses = EnumCodes<FoodCourse>(raw.ExcludedCourseCodes, "COURSE", warnings);
        var purposes = EnumCodes<DiningPurpose>(raw.MealPurposeCodes, "DINING_PURPOSE", warnings);
        MergeProfile(context.CustomerProfile, ingredients, excludedIngredients, allergens, dietary, tastes, avoidedTastes, methods, avoidedMethods, courses, purposes);
        if (context.CustomerProfile is not null && dietary.Count > 0)
        {
            var explicitIngredients = RawCodes(raw.PreferredIngredientCodes);
            var profileOnlyIngredients = context.CustomerProfile.PreferredIngredients.Select(value => value.Ingredient.Code)
                .Where(value => !explicitIngredients.Contains(value)).ToArray();
            if (profileOnlyIngredients.Any(ingredients.Remove))
                warnings.Add("PROFILE_SOFT_INGREDIENTS_SUPPRESSED_BY_DIETARY");
        }
        var ambiguities = Terms(raw.Ambiguities).ToHashSet(StringComparer.Ordinal);
        var clarificationNeeded = raw.ClarificationNeeded;
        if (ingredients.Overlaps(excludedIngredients)) { clarificationNeeded = true; ambiguities.Add("INGREDIENT_PREFERENCE_CONFLICT"); }
        if (tastes.Overlaps(avoidedTastes)) { clarificationNeeded = true; ambiguities.Add("TASTE_PREFERENCE_CONFLICT"); }
        if (methods.Overlaps(avoidedMethods)) { clarificationNeeded = true; ambiguities.Add("PREPARATION_PREFERENCE_CONFLICT"); }
        if (courses.Overlaps(excludedCourses)) { clarificationNeeded = true; ambiguities.Add("COURSE_PREFERENCE_CONFLICT"); }
        ingredients.ExceptWith(excludedIngredients); tastes.ExceptWith(avoidedTastes); methods.ExceptWith(avoidedMethods); courses.ExceptWith(excludedCourses);

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
        var temperatures = (raw.PreferredServingTemperatures ?? []).Where(value => Enum.IsDefined(value) && value != ServingTemperature.UNKNOWN)
            .Distinct().Take(MaximumArrayCount).ToArray();
        var partySize = raw.PartySize is > 0 and <= 100 ? raw.PartySize : null;
        if (raw.PartySize.HasValue && !partySize.HasValue) warnings.Add("PARTY_SIZE_REMOVED");
        if (!Enum.IsDefined(raw.SortPreference)) errors.Add("AI_INVALID_SORT_PREFERENCE");
        var queryLimit = Math.Clamp(_options.MaximumQueryCharacters, 100, 4000);
        var summary = string.IsNullOrWhiteSpace(raw.Summary) ? context.Query.Trim() : raw.Summary.Trim();
        if (summary.Length > queryLimit) summary = summary[..queryLimit];
        var intent = new FoodRecommendationIntent
        {
            InputLanguageHint = raw.InputLanguageHint, DetectedLanguage = raw.DetectedLanguage,
            ResponseLanguage = raw.ResponseLanguage, LanguageConfidence = raw.LanguageConfidence,
            LanguageWarnings = raw.LanguageWarnings,
            Summary = summary,
            OriginalNormalizedQuery = DeterministicFoodIntentParser.NormalizeText(
                string.IsNullOrWhiteSpace(raw.OriginalNormalizedQuery) ? context.Query : raw.OriginalNormalizedQuery),
            DesiredFoodTerms = (raw.DesiredFoodTerms ?? []).Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(DeterministicFoodIntentParser.NormalizeText).Where(value => value.Length is > 0 and <= 100)
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).Take(MaximumArrayCount).ToArray(),
            ContextualTerms = Terms(raw.ContextualTerms), UnmappedMeaningfulTerms = Terms(raw.UnmappedMeaningfulTerms),
            PreferredIngredientCodes = Ordered(ingredients), ExcludedIngredientCodes = Ordered(excludedIngredients),
            AllergenExclusionCodes = Ordered(allergens), DietaryRequirementCodes = Ordered(dietary),
            PreferredTasteCodes = Ordered(tastes), AvoidedTasteCodes = Ordered(avoidedTastes),
            PreferredSpiceLevel = raw.PreferredSpiceLevel ?? context.CustomerProfile?.PreferredSpiceLevel,
            PreparationMethodCodes = Ordered(methods), AvoidedPreparationMethodCodes = Ordered(avoidedMethods),
            PreferredCourseCodes = Ordered(courses), ExcludedCourseCodes = Ordered(excludedCourses), MealPurposeCodes = Ordered(purposes),
            PreferredServingTemperatures = temperatures, SocialContext = Text(raw.SocialContext), DesiredFullness = Text(raw.DesiredFullness),
            MinimumPrice = minimum, MaximumPrice = maximum, PartySize = partySize, IsShareablePreferred = raw.IsShareablePreferred,
            TakeawayPreferred = raw.TakeawayPreferred, QuickServicePreferred = raw.QuickServicePreferred,
            HealthyPreference = raw.HealthyPreference, FreshPreference = raw.FreshPreference,
            PopularityPreference = raw.PopularityPreference,
            PreferNearMe = raw.PreferNearMe, MaximumDistanceMeters = distance,
            SortPreference = Enum.IsDefined(raw.SortPreference) ? raw.SortPreference : FoodRecommendationSortPreference.BEST_MATCH,
            Confidence = Math.Clamp(raw.Confidence, 0m, 1m), Ambiguities = Ordered(ambiguities), ClarificationNeeded = clarificationNeeded,
            Warnings = warnings.OrderBy(value => value, StringComparer.Ordinal).ToArray(), SignalEvidence = raw.SignalEvidence
        };
        if (context.CustomerProfile is not null)
        {
            var evidence = intent.SignalEvidence.ToList();
            static void Add(List<IntentSignalEvidence> target, string field, IEnumerable<string> values)
            {
                target.AddRange(values.Distinct(StringComparer.Ordinal).Select(value => new IntentSignalEvidence
                    { Field = field, Value = value, Source = "CUSTOMER_PROFILE", Confidence = 1m }));
            }
            Add(evidence, nameof(intent.PreferredIngredientCodes), context.CustomerProfile.PreferredIngredients.Select(value => value.Ingredient.Code));
            Add(evidence, nameof(intent.ExcludedIngredientCodes), context.CustomerProfile.AvoidedIngredients.Select(value => value.Ingredient.Code));
            Add(evidence, nameof(intent.AllergenExclusionCodes), context.CustomerProfile.AllergenExclusions.Select(value => value.Allergen.Code));
            Add(evidence, nameof(intent.DietaryRequirementCodes), context.CustomerProfile.DietaryRequirements.Select(value => value.DietaryAttribute.Code));
            intent.SignalEvidence = evidence.GroupBy(value => (value.Field, value.Value, value.Source))
                .Select(group => group.OrderByDescending(value => value.Confidence).First()).Take(100).ToArray();
        }
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
        ISet<string> methods, ISet<string> avoidedMethods, ISet<string> courses, ISet<string> purposes)
    {
        if (profile is null) return;
        foreach (var value in profile.PreferredIngredients)
            if (!excludedIngredients.Contains(value.Ingredient.Code)) ingredients.Add(value.Ingredient.Code);
        foreach (var value in profile.AvoidedIngredients)
            if (!ingredients.Contains(value.Ingredient.Code)) excludedIngredients.Add(value.Ingredient.Code);
        foreach (var value in profile.AllergenExclusions) allergens.Add(value.Allergen.Code);
        foreach (var value in profile.DietaryRequirements) dietary.Add(value.DietaryAttribute.Code);
        foreach (var value in profile.PreferredTasteProfiles)
            if (!avoidedTastes.Contains(value.TasteProfile.Code)) tastes.Add(value.TasteProfile.Code);
        foreach (var value in profile.AvoidedTasteProfiles)
            if (!tastes.Contains(value.TasteProfile.Code)) avoidedTastes.Add(value.TasteProfile.Code);
        foreach (var value in profile.PreferredPreparationMethods)
            if (!avoidedMethods.Contains(value.PreparationMethod.Code)) methods.Add(value.PreparationMethod.Code);
        foreach (var value in profile.PreferredCourses)
            if (!courses.Contains(value.Course.ToString())) courses.Add(value.Course.ToString());
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
    private static string[] Terms(IEnumerable<string>? values) => (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(DeterministicFoodIntentParser.NormalizeText).Where(value => value.Length is > 0 and <= 100)
        .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).Take(MaximumArrayCount).ToArray();
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, 100)];
    private static bool HasSignal(FoodRecommendationIntent value) => value.OriginalNormalizedQuery.Any(char.IsLetterOrDigit)
        || value.DesiredFoodTerms.Count + value.ContextualTerms.Count + value.UnmappedMeaningfulTerms.Count + value.PreferredIngredientCodes.Count
        + value.ExcludedIngredientCodes.Count + value.AllergenExclusionCodes.Count + value.DietaryRequirementCodes.Count
        + value.PreferredTasteCodes.Count + value.AvoidedTasteCodes.Count + value.PreparationMethodCodes.Count
        + value.AvoidedPreparationMethodCodes.Count + value.PreferredCourseCodes.Count + value.MealPurposeCodes.Count > 0
        || value.PreferredSpiceLevel.HasValue || value.MinimumPrice.HasValue || value.MaximumPrice.HasValue || value.PreferNearMe;
}

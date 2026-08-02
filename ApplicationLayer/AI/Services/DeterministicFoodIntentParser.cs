using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ApplicationLayer.AI.V2.Models;
using DomainLayer.Enums;

namespace ApplicationLayer.AI.V2.Services;

public sealed partial class DeterministicFoodIntentParser : IFoodRecommendationFallbackParser
{
    private const int MaximumSignals = 20;
    private static readonly (string Code, string[] Terms)[] Ingredients =
    [
        ("ING_BEEF", ["bo", "thit bo"]), ("ING_CHICKEN", ["ga", "thit ga"]),
        ("ING_PORK", ["heo", "thit heo", "thit lon"]), ("ING_SEAFOOD", ["hai san"]),
        ("ING_SHRIMP", ["tom"]), ("ING_FISH", ["ca"]), ("ING_SQUID", ["muc"]),
        ("ING_EGG", ["trung"]), ("ING_VEGETABLE", ["rau"]), ("ING_TOFU", ["dau hu", "dau phu"]),
        ("ING_PEANUT", ["dau phong", "lac"])
    ];
    private static readonly (string Code, string[] Terms)[] Methods =
    [
        ("METHOD_GRILLED", ["nuong"]), ("METHOD_FRIED", ["chien"]), ("METHOD_STEAMED", ["hap"]),
        ("METHOD_BOILED", ["luoc"]), ("METHOD_STIR_FRIED", ["xao"]), ("METHOD_ROASTED", ["quay"]),
        ("METHOD_SIMMERED", ["nau", "ham"]), ("METHOD_MIXED", ["tron"])
    ];
    private static readonly (string Code, string[] Terms)[] Tastes =
    [
        ("TASTE_SWEET", ["ngot"]), ("TASTE_SOUR", ["chua"]), ("TASTE_SALTY", ["man"]),
        ("TASTE_RICH", ["dam vi", "beo"]), ("TASTE_LIGHT", ["thanh nhe", "vi nhe", "it dau"]),
        ("TASTE_SPICY", ["cay"]), ("TASTE_MILD_SPICY", ["cay nhe"]), ("TASTE_VERY_SPICY", ["rat cay"])
    ];

    public FoodRecommendationIntentExtractionResult Parse(FoodRecommendationIntentRequest request)
    {
        var query = NormalizeText(request.Query);
        var warnings = new HashSet<string>(StringComparer.Ordinal);
        var preferredIngredients = new HashSet<string>(StringComparer.Ordinal);
        var excludedIngredients = new HashSet<string>(StringComparer.Ordinal);
        var allergens = new HashSet<string>(StringComparer.Ordinal);
        var dietary = new HashSet<string>(StringComparer.Ordinal);
        var tastes = new HashSet<string>(StringComparer.Ordinal);
        var avoidedTastes = new HashSet<string>(StringComparer.Ordinal);
        var methods = new HashSet<string>(StringComparer.Ordinal);
        var avoidedMethods = new HashSet<string>(StringComparer.Ordinal);
        var courses = new HashSet<string>(StringComparer.Ordinal);
        var purposes = new HashSet<string>(StringComparer.Ordinal);
        var desiredTerms = new HashSet<string>(StringComparer.Ordinal);

        foreach (var mapping in Ingredients)
        {
            if (!Allowed(request.AllowedTaxonomy.Ingredients, mapping.Code)) continue;
            if (mapping.Terms.Any(term => IsExcluded(query, term))) excludedIngredients.Add(mapping.Code);
            else if (mapping.Terms.Any(term => ContainsTerm(query, term)))
            { preferredIngredients.Add(mapping.Code); desiredTerms.Add(mapping.Terms[0]); }
        }
        foreach (var mapping in Methods)
        {
            if (!Allowed(request.AllowedTaxonomy.PreparationMethods, mapping.Code)) continue;
            if (mapping.Terms.Any(term => IsAvoidedMethod(query, term))) avoidedMethods.Add(mapping.Code);
            else if (mapping.Terms.Any(term => ContainsTerm(query, term))) methods.Add(mapping.Code);
        }
        if (ContainsTerm(query, "it dau"))
        { AddAllowed(avoidedMethods, request.AllowedTaxonomy.PreparationMethods, "METHOD_FRIED"); AddAllowed(avoidedMethods, request.AllowedTaxonomy.PreparationMethods, "METHOD_PAN_FRIED"); }
        foreach (var mapping in Tastes)
            if (Allowed(request.AllowedTaxonomy.TasteProfiles, mapping.Code) && mapping.Terms.Any(term => ContainsTerm(query, term))) tastes.Add(mapping.Code);

        FoodSpiceLevel? spice = null;
        if (ContainsTerm(query, "khong cay"))
        {
            spice = FoodSpiceLevel.NON_SPICY;
            tastes.Remove("TASTE_SPICY"); tastes.Remove("TASTE_MILD_SPICY"); tastes.Remove("TASTE_VERY_SPICY");
            AddAllowed(avoidedTastes, request.AllowedTaxonomy.TasteProfiles, "TASTE_SPICY");
            AddAllowed(avoidedTastes, request.AllowedTaxonomy.TasteProfiles, "TASTE_VERY_SPICY");
        }
        else if (ContainsTerm(query, "rat cay")) spice = FoodSpiceLevel.VERY_SPICY;
        else if (ContainsTerm(query, "cay nhe") || ContainsTerm(query, "hoi cay")) spice = FoodSpiceLevel.MILD;
        else if (ContainsTerm(query, "cay")) spice = FoodSpiceLevel.SPICY;

        if (ContainsTerm(query, "chay")) AddAllowed(dietary, request.AllowedTaxonomy.DietaryAttributes, "DIET_VEGETARIAN");
        AddCourseAndPurpose(query, request.AllowedTaxonomy, courses, purposes);
        ParseAllergies(query, request.AllowedTaxonomy, allergens, excludedIngredients, warnings);
        var (minimumPrice, maximumPrice) = ParseBudget(query);
        var preferNear = ContainsTerm(query, "gan toi") || ContainsTerm(query, "khong qua xa");
        var sort = ContainsTerm(query, "danh gia cao") ? FoodRecommendationSortPreference.HIGHEST_RATED_RELEVANT
            : ContainsTerm(query, "gia re") ? FoodRecommendationSortPreference.LOWEST_PRICE_RELEVANT
            : preferNear ? FoodRecommendationSortPreference.NEAREST_RELEVANT : FoodRecommendationSortPreference.BEST_MATCH;

        preferredIngredients.ExceptWith(excludedIngredients); tastes.ExceptWith(avoidedTastes); methods.ExceptWith(avoidedMethods);
        var signalCount = preferredIngredients.Count + excludedIngredients.Count + allergens.Count + dietary.Count + tastes.Count
            + avoidedTastes.Count + methods.Count + avoidedMethods.Count + courses.Count + purposes.Count
            + (spice.HasValue ? 1 : 0) + (minimumPrice.HasValue || maximumPrice.HasValue ? 1 : 0) + (preferNear ? 1 : 0);
        if (signalCount == 0)
            return new() { IsSuccess = false, UsedFallback = true, ProviderName = "DETERMINISTIC_FALLBACK",
                FailureCategory = AiProviderFailureCategory.VALIDATION_FAILED, ValidationWarnings = ["AI_FALLBACK_PARSE_INSUFFICIENT"] };

        var intent = new FoodRecommendationIntent
        {
            Summary = request.Query.Trim(), DesiredFoodTerms = Ordered(desiredTerms),
            PreferredIngredientCodes = Ordered(preferredIngredients), ExcludedIngredientCodes = Ordered(excludedIngredients),
            AllergenExclusionCodes = Ordered(allergens), DietaryRequirementCodes = Ordered(dietary),
            PreferredTasteCodes = Ordered(tastes), AvoidedTasteCodes = Ordered(avoidedTastes), PreferredSpiceLevel = spice,
            PreparationMethodCodes = Ordered(methods), AvoidedPreparationMethodCodes = Ordered(avoidedMethods),
            PreferredCourseCodes = Ordered(courses), MealPurposeCodes = Ordered(purposes), MinimumPrice = minimumPrice,
            MaximumPrice = maximumPrice, PreferNearMe = preferNear, SortPreference = sort,
            Confidence = Math.Clamp(0.30m + signalCount * 0.04m, 0.30m, 0.70m), Warnings = Ordered(warnings)
        };
        return new() { IsSuccess = true, UsedFallback = true, ProviderName = "DETERMINISTIC_FALLBACK",
            FailureCategory = AiProviderFailureCategory.NONE, ValidationWarnings = intent.Warnings, ParsedResult = intent };
    }

    public MealPlanIntentExtractionResult Parse(MealPlanIntentRequest request)
    {
        var food = Parse(new FoodRecommendationIntentRequest(request.Query, request.AllowedTaxonomy));
        var value = food.ParsedResult ?? new FoodRecommendationIntent
        {
            Summary = request.Query.Trim(),
            Confidence = .35m,
            MealPurposeCodes = request.AllowedTaxonomy.DiningPurposes
                .Where(code => string.Equals(code, request.DiningStyle, StringComparison.Ordinal)
                    || request.DiningStyle is "FAMILY" or "DATE" or "FRIEND_GROUP" or "BUDGET_FRIENDLY" or "LOCAL_SPECIALTY"
                        && code == "FULL_MEAL")
                .Take(1).ToArray(),
            Warnings = food.ValidationWarnings.Where(warning => warning != "AI_FALLBACK_PARSE_INSUFFICIENT").ToArray()
        };
        return new() { IsSuccess = true, UsedFallback = true, ProviderName = "DETERMINISTIC_FALLBACK", FailureCategory = AiProviderFailureCategory.NONE,
            ValidationWarnings = food.ValidationWarnings, ParsedResult = new MealPlanIntent
            {
                Summary = value.Summary, PreferredIngredientCodes = value.PreferredIngredientCodes, ExcludedIngredientCodes = value.ExcludedIngredientCodes,
                AllergenExclusionCodes = value.AllergenExclusionCodes, DietaryRequirementCodes = value.DietaryRequirementCodes,
                PreferredTasteCodes = value.PreferredTasteCodes, AvoidedTasteCodes = value.AvoidedTasteCodes,
                PreferredSpiceLevel = value.PreferredSpiceLevel, PreparationMethodCodes = value.PreparationMethodCodes,
                AvoidedPreparationMethodCodes = value.AvoidedPreparationMethodCodes, PreferredCourseCodes = value.PreferredCourseCodes,
                MealPurposeCodes = value.MealPurposeCodes, RequestedCourseHints = value.PreferredCourseCodes,
                PreferNearMe = value.PreferNearMe, MaximumDistanceMeters = value.MaximumDistanceMeters,
                Confidence = value.Confidence, Warnings = value.Warnings
            }};
    }

    private static void AddCourseAndPurpose(string query, AiTaxonomyCodes allowed, ISet<string> courses, ISet<string> purposes)
    {
        if (ContainsTerm(query, "trang mieng")) { AddAllowed(courses, allowed.Courses, "DESSERT"); AddAllowed(purposes, allowed.DiningPurposes, "DESSERT"); }
        if (ContainsTerm(query, "do uong") || ContainsTerm(query, "nuoc uong")) { AddAllowed(courses, allowed.Courses, "DRINK"); AddAllowed(purposes, allowed.DiningPurposes, "REFRESHMENT"); }
        if (ContainsTerm(query, "mon chinh") || ContainsTerm(query, "an no")) { AddAllowed(courses, allowed.Courses, "MAIN_COURSE"); AddAllowed(purposes, allowed.DiningPurposes, "FULL_MEAL"); }
        if (ContainsTerm(query, "khai vi")) AddAllowed(courses, allowed.Courses, "APPETIZER");
        if (ContainsTerm(query, "an nhe")) AddAllowed(purposes, allowed.DiningPurposes, "LIGHT_MEAL");
    }

    private static void ParseAllergies(string query, AiTaxonomyCodes allowed, ISet<string> allergens, ISet<string> excluded, ISet<string> warnings)
    {
        foreach (var mapping in Ingredients)
        {
            if (!mapping.Terms.Any(term => new[] { "di ung", "allergy" }.Any(prefix => ContainsTerm(query, $"{prefix} {term}")))) continue;
            var suffix = mapping.Code[4..];
            var allergen = allowed.Allergens.FirstOrDefault(code => code.Contains(suffix, StringComparison.OrdinalIgnoreCase)
                || suffix == "SHRIMP" && code.Contains("CRUSTACEAN", StringComparison.OrdinalIgnoreCase));
            if (allergen is not null) allergens.Add(allergen.Trim().ToUpperInvariant());
            else { AddAllowed(excluded, allowed.Ingredients, mapping.Code); warnings.Add($"ALLERGEN_NOT_AUTHORITATIVELY_MAPPED:{mapping.Code}"); }
        }
    }

    private static (decimal? Minimum, decimal? Maximum) ParseBudget(string query)
    {
        var range = BudgetRangeRegex().Match(query);
        if (range.Success)
        {
            var minimumUnit = string.IsNullOrWhiteSpace(range.Groups[2].Value) ? range.Groups[4].Value : range.Groups[2].Value;
            var maximumUnit = string.IsNullOrWhiteSpace(range.Groups[4].Value) ? range.Groups[2].Value : range.Groups[4].Value;
            return (Money(range.Groups[1].Value, minimumUnit), Money(range.Groups[3].Value, maximumUnit));
        }
        var upper = BudgetUpperRegex().Match(query);
        if (upper.Success) return (null, Money(upper.Groups[1].Value, upper.Groups[2].Value));
        var about = BudgetAboutRegex().Match(query);
        return about.Success ? (null, Money(about.Groups[1].Value, about.Groups[2].Value)) : (null, null);
    }

    private static decimal? Money(string amount, string unit)
    {
        var digits = amount.Replace(".", string.Empty).Replace(",", string.Empty);
        if (!decimal.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0) return null;
        if (unit is "k" or "nghin" or "ngan") value *= 1_000;
        if (unit is "tr" or "trieu") value *= 1_000_000;
        return value;
    }

    private static bool Allowed(IEnumerable<string> values, string code) => values.Any(value => string.Equals(value, code, StringComparison.OrdinalIgnoreCase));
    private static void AddAllowed(ISet<string> target, IEnumerable<string> values, string code) { if (Allowed(values, code)) target.Add(code); }
    private static string[] Ordered(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).Take(MaximumSignals).ToArray();
    private static bool ContainsTerm(string query, string term) => Regex.IsMatch(query, $@"(^|[^a-z0-9]){Regex.Escape(term)}($|[^a-z0-9])", RegexOptions.CultureInvariant);
    private static bool IsExcluded(string query, string term) => new[] { "khong an", "khong muon", "tranh", "loai bo" }.Any(prefix => ContainsTerm(query, $"{prefix} {term}"));
    private static bool IsAvoidedMethod(string query, string term) => new[] { "khong", "khong muon", "tranh", "it" }.Any(prefix => ContainsTerm(query, $"{prefix} {term}"));

    public static string NormalizeText(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character));
        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"[^a-z0-9.,]+", " ").Trim();
    }

    [GeneratedRegex(@"\btu\s+([0-9][0-9.,]*)\s*(k|nghin|ngan|tr|trieu)?\s+den\s+([0-9][0-9.,]*)\s*(k|nghin|ngan|tr|trieu)?\b", RegexOptions.CultureInvariant)] private static partial Regex BudgetRangeRegex();
    [GeneratedRegex(@"\b(?:duoi|khong qua|toi da)\s+([0-9][0-9.,]*)\s*(k|nghin|ngan|tr|trieu)?\b", RegexOptions.CultureInvariant)] private static partial Regex BudgetUpperRegex();
    [GeneratedRegex(@"\b(?:khoang|tam|gia)\s+([0-9][0-9.,]*)\s*(k|nghin|ngan|tr|trieu)?\b", RegexOptions.CultureInvariant)] private static partial Regex BudgetAboutRegex();
}

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
        ("ING_BEEF", ["bo", "thit bo", "beef"]), ("ING_CHICKEN", ["ga", "thit ga", "chicken"]),
        ("ING_PORK", ["heo", "thit heo", "thit lon", "pork"]), ("ING_SEAFOOD", ["hai san", "seafood"]),
        ("ING_SHRIMP", ["tom"]), ("ING_FISH", ["ca"]), ("ING_SQUID", ["muc"]),
        ("ING_EGG", ["trung", "egg"]), ("ING_VEGETABLE", ["rau", "vegetable"]), ("ING_TOFU", ["dau hu", "dau phu", "tofu"]),
        ("ING_PEANUT", ["dau phong", "lac", "peanut"])
    ];
    private static readonly (string Code, string[] Terms)[] Methods =
    [
        ("METHOD_GRILLED", ["nuong", "grilled", "grill"]), ("METHOD_FRIED", ["chien", "fried"]), ("METHOD_STEAMED", ["hap", "steamed"]),
        ("METHOD_BOILED", ["luoc"]), ("METHOD_STIR_FRIED", ["xao"]), ("METHOD_ROASTED", ["quay"]),
        ("METHOD_SIMMERED", ["nau", "ham"]), ("METHOD_MIXED", ["tron"])
    ];
    private static readonly (string Code, string[] Terms)[] Tastes =
    [
        ("TASTE_SWEET", ["ngot"]), ("TASTE_SOUR", ["chua"]), ("TASTE_SALTY", ["man"]),
        ("TASTE_RICH", ["dam vi", "beo"]), ("TASTE_LIGHT", ["thanh nhe", "vi nhe", "it dau"]),
        ("TASTE_SPICY", ["cay", "spicy"]), ("TASTE_MILD_SPICY", ["cay nhe", "mildly spicy", "mild spicy"]), ("TASTE_VERY_SPICY", ["rat cay", "very spicy"])
    ];
    private static readonly HashSet<string> QueryStopWords =
    [
        "toi", "minh", "muon", "an", "mon", "cai", "gi", "do", "cho", "something", "i", "want", "to", "eat", "a", "the", "please"
    ];
    private static readonly HashSet<string> ContextStopWords =
    [
        "mat", "lanh", "giai", "nhiet", "thanh", "no", "refreshing", "cool", "cold", "hap", "dan", "ngon", "tasty",
        "cung", "ban", "nhom", "friend", "friends", "share", "with", "duoc", "light", "snack", "filling", "meal"
    ];

    public FoodRecommendationIntentExtractionResult Parse(FoodRecommendationIntentRequest request)
    {
        var language = DetectLanguage(request.Query, request.InputLanguageHint);
        var query = NormalizeText(request.Query);
        if (!IsUsableQuery(query))
            return new() { IsSuccess = false, UsedFallback = true, ProviderName = "DETERMINISTIC_FALLBACK",
                FailureCategory = AiProviderFailureCategory.VALIDATION_FAILED, ValidationWarnings = ["AI_FALLBACK_PARSE_INSUFFICIENT"] };
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
        var contextualTerms = ContextualTerms(query).ToHashSet(StringComparer.Ordinal);
        var meaningfulTerms = MeaningfulTerms(query).ToHashSet(StringComparer.Ordinal);

        foreach (var mapping in Ingredients)
        {
            var code = ResolveAllowed(request.AllowedTaxonomy.Ingredients, mapping.Code);
            if (code is null) continue;
            if (mapping.Terms.Any(term => IsExcluded(query, term))) excludedIngredients.Add(code);
            else if (mapping.Terms.Any(term => ContainsTerm(query, term)))
            { preferredIngredients.Add(code); desiredTerms.Add(mapping.Terms[0]); }
        }
        foreach (var mapping in Methods)
        {
            var code = ResolveAllowed(request.AllowedTaxonomy.PreparationMethods, mapping.Code);
            if (code is null) continue;
            if (mapping.Terms.Any(term => IsAvoidedMethod(query, term))) avoidedMethods.Add(code);
            else if (mapping.Terms.Any(term => ContainsTerm(query, term))) methods.Add(code);
        }
        if (ContainsTerm(query, "it dau") || ContainsTerm(query, "khong thich do chien") || ContainsTerm(query, "do not like fried food"))
        { AddAllowed(avoidedMethods, request.AllowedTaxonomy.PreparationMethods, "METHOD_FRIED"); AddAllowed(avoidedMethods, request.AllowedTaxonomy.PreparationMethods, "METHOD_PAN_FRIED"); }
        foreach (var mapping in Tastes)
        {
            var code = ResolveAllowed(request.AllowedTaxonomy.TasteProfiles, mapping.Code);
            if (code is not null && mapping.Terms.Any(term => ContainsTerm(query, term))) tastes.Add(code);
        }
        if (new[] { "khong qua ngot", "dung qua ngot", "not too sweet" }.Any(term => ContainsTerm(query, term)))
        { AddAllowed(avoidedTastes, request.AllowedTaxonomy.TasteProfiles, "TASTE_SWEET"); tastes.Remove("TASTE_SWEET"); }

        FoodSpiceLevel? spice = null;
        if (ContainsTerm(query, "khong cay") || ContainsTerm(query, "not spicy") || ContainsTerm(query, "non spicy"))
        {
            spice = FoodSpiceLevel.NON_SPICY;
            tastes.Remove("TASTE_SPICY"); tastes.Remove("TASTE_MILD_SPICY"); tastes.Remove("TASTE_VERY_SPICY");
            AddAllowed(avoidedTastes, request.AllowedTaxonomy.TasteProfiles, "TASTE_SPICY");
            AddAllowed(avoidedTastes, request.AllowedTaxonomy.TasteProfiles, "TASTE_VERY_SPICY");
        }
        else if (ContainsTerm(query, "rat cay") || ContainsTerm(query, "very spicy")) spice = FoodSpiceLevel.VERY_SPICY;
        else if (ContainsTerm(query, "cay nhe") || ContainsTerm(query, "hoi cay") || ContainsTerm(query, "mildly spicy") || ContainsTerm(query, "mild spicy")) spice = FoodSpiceLevel.MILD;
        else if (ContainsTerm(query, "cay") || ContainsTerm(query, "spicy")) spice = FoodSpiceLevel.SPICY;

        if (ContainsTerm(query, "chay") || ContainsTerm(query, "vegetarian")) AddAllowed(dietary, request.AllowedTaxonomy.DietaryAttributes, "DIET_VEGETARIAN");
        AddCourseAndPurpose(query, request.AllowedTaxonomy, courses, purposes);
        ParseAllergies(query, request.AllowedTaxonomy, allergens, excludedIngredients, warnings);
        var (minimumPrice, maximumPrice) = ParseBudget(query);
        var partySize = ParsePartySize(query);
        var temperatures = new HashSet<ServingTemperature>();
        if (new[] { "nong", "nong nong", "hot" }.Any(term => ContainsTerm(query, term))) temperatures.Add(ServingTemperature.HOT);
        if (new[] { "lanh", "lanh lanh", "mat", "refreshing", "cold" }.Any(term => ContainsTerm(query, term))) temperatures.Add(ServingTemperature.COLD);
        var shareable = new[] { "chia se", "chia nhau", "dung chung", "share", "shareable" }.Any(term => ContainsTerm(query, term)) ? true : (bool?)null;
        var takeaway = new[] { "mang di", "takeaway", "take away", "to go" }.Any(term => ContainsTerm(query, term)) ? true : (bool?)null;
        var quick = new[] { "dang voi", "voi", "in a hurry", "quick" }.Any(term => ContainsTerm(query, term)) ? true : (bool?)null;
        var popularity = new[] { "pho bien", "popular", "danh gia cao" }.Any(term => ContainsTerm(query, term)) ? true : (bool?)null;
        var healthy = new[] { "lanh manh", "healthy", "nhieu rau" }.Any(term => ContainsTerm(query, term)) ? true : (bool?)null;
        var fresh = new[] { "tuoi", "fresh", "thanh mat" }.Any(term => ContainsTerm(query, term)) ? true : (bool?)null;
        if (shareable == true) AddAllowed(purposes, request.AllowedTaxonomy.DiningPurposes, "SHARING");
        if (takeaway == true) AddAllowed(purposes, request.AllowedTaxonomy.DiningPurposes, "TAKEAWAY");
        if (quick == true) AddAllowed(purposes, request.AllowedTaxonomy.DiningPurposes, "QUICK_MEAL");
        var social = new[] { "hen ho", "date night", "on a date" }.Any(term => ContainsTerm(query, term)) ? "DATE"
            : new[] { "voi ban", "cung ban", "friends" }.Any(term => ContainsTerm(query, term)) ? "FRIEND_GROUP" : null;
        var fullness = new[] { "an no", "cho no", "filling" }.Any(term => ContainsTerm(query, term)) ? "FULL"
            : new[] { "an nhe", "nhe nhang", "light meal" }.Any(term => ContainsTerm(query, term)) ? "LIGHT" : null;
        var ambiguities = new HashSet<string>(StringComparer.Ordinal);
        var seafoodConflict = HasCode(excludedIngredients, "SEAFOOD") && preferredIngredients.Any(value => new[] { "SHRIMP", "FISH", "SQUID" }.Contains(CodeSuffix(value)));
        var vegetarianConflict = HasCode(dietary, "VEGETARIAN") && preferredIngredients.Any(value => new[] { "BEEF", "CHICKEN", "PORK", "SEAFOOD", "SHRIMP", "FISH", "SQUID" }.Contains(CodeSuffix(value)));
        if (seafoodConflict) ambiguities.Add("SEAFOOD_EXCLUSION_CONFLICT");
        if (vegetarianConflict) ambiguities.Add("VEGETARIAN_CONFLICT");
        var preferNear = ContainsTerm(query, "gan toi") || ContainsTerm(query, "khong qua xa") || ContainsTerm(query, "near me") || ContainsTerm(query, "nearby");
        var sort = ContainsTerm(query, "danh gia cao") ? FoodRecommendationSortPreference.HIGHEST_RATED_RELEVANT
            : ContainsTerm(query, "gia re") ? FoodRecommendationSortPreference.LOWEST_PRICE_RELEVANT
            : preferNear ? FoodRecommendationSortPreference.NEAREST_RELEVANT : FoodRecommendationSortPreference.BEST_MATCH;

        preferredIngredients.ExceptWith(excludedIngredients); tastes.ExceptWith(avoidedTastes); methods.ExceptWith(avoidedMethods);
        var signalCount = preferredIngredients.Count + excludedIngredients.Count + allergens.Count + dietary.Count + tastes.Count
            + avoidedTastes.Count + methods.Count + avoidedMethods.Count + courses.Count + purposes.Count
            + (spice.HasValue ? 1 : 0) + (minimumPrice.HasValue || maximumPrice.HasValue ? 1 : 0) + (preferNear ? 1 : 0);
        foreach (var term in contextualTerms) meaningfulTerms.Remove(term);
        var residual = string.Join(' ', meaningfulTerms);
        if (residual.Length > 0) desiredTerms.Add(residual);

        var intent = new FoodRecommendationIntent
        {
            InputLanguageHint = request.InputLanguageHint, DetectedLanguage = language,
            ResponseLanguage = SupportedResponseLanguage(request.ResponseLanguage, warnings), LanguageConfidence = language == "en" ? .85m : .80m,
            LanguageWarnings = Ordered(warnings.Where(value => value.StartsWith("LANGUAGE_", StringComparison.Ordinal))),
            Summary = LocalizedSummary(request.Query, language, request.ResponseLanguage), OriginalNormalizedQuery = query,
            DesiredFoodTerms = Ordered(desiredTerms), ContextualTerms = Ordered(contextualTerms),
            UnmappedMeaningfulTerms = Ordered(meaningfulTerms),
            PreferredIngredientCodes = Ordered(preferredIngredients), ExcludedIngredientCodes = Ordered(excludedIngredients),
            AllergenExclusionCodes = Ordered(allergens), DietaryRequirementCodes = Ordered(dietary),
            PreferredTasteCodes = Ordered(tastes), AvoidedTasteCodes = Ordered(avoidedTastes), PreferredSpiceLevel = spice,
            PreparationMethodCodes = Ordered(methods), AvoidedPreparationMethodCodes = Ordered(avoidedMethods),
            PreferredCourseCodes = Ordered(courses), MealPurposeCodes = Ordered(purposes), PreferredServingTemperatures = temperatures.ToArray(),
            SocialContext = social, DesiredFullness = fullness, MinimumPrice = minimumPrice, MaximumPrice = maximumPrice,
            PartySize = partySize, IsShareablePreferred = shareable, TakeawayPreferred = takeaway, QuickServicePreferred = quick,
            HealthyPreference = healthy, FreshPreference = fresh, PopularityPreference = popularity, PreferNearMe = preferNear, SortPreference = sort,
            Ambiguities = Ordered(ambiguities), ClarificationNeeded = ambiguities.Count > 0,
            Confidence = Math.Clamp(0.30m + Math.Max(1, signalCount) * 0.04m, 0.30m, 0.70m), Warnings = Ordered(warnings)
        };
        intent.SignalEvidence = IntentSignalEvidenceFactory.Create(intent, request.Query, "EXPLICIT_QUERY", intent.Confidence);
        return new() { IsSuccess = true, UsedFallback = true, ProviderName = "DETERMINISTIC_FALLBACK",
            FailureCategory = AiProviderFailureCategory.NONE, ValidationWarnings = intent.Warnings, ParsedResult = intent };
    }

    public MealPlanIntentExtractionResult Parse(MealPlanIntentRequest request)
    {
        var food = Parse(new FoodRecommendationIntentRequest(request.Query, request.AllowedTaxonomy, request.InputLanguageHint, request.ResponseLanguage));
        if (!food.IsSuccess && food.ValidationWarnings.Contains("AI_LANGUAGE_PROVIDER_REQUIRED"))
            return new() { IsSuccess = false, UsedFallback = true, ProviderName = "DETERMINISTIC_FALLBACK",
                FailureCategory = AiProviderFailureCategory.VALIDATION_FAILED, ValidationWarnings = food.ValidationWarnings };
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
                InputLanguageHint = value.InputLanguageHint, DetectedLanguage = value.DetectedLanguage,
                ResponseLanguage = value.ResponseLanguage, LanguageConfidence = value.LanguageConfidence,
                LanguageWarnings = value.LanguageWarnings,
                Summary = value.Summary, PreferredIngredientCodes = value.PreferredIngredientCodes, ExcludedIngredientCodes = value.ExcludedIngredientCodes,
                AllergenExclusionCodes = value.AllergenExclusionCodes, DietaryRequirementCodes = value.DietaryRequirementCodes,
                PreferredTasteCodes = value.PreferredTasteCodes, AvoidedTasteCodes = value.AvoidedTasteCodes,
                PreferredSpiceLevel = value.PreferredSpiceLevel, PreparationMethodCodes = value.PreparationMethodCodes,
                AvoidedPreparationMethodCodes = value.AvoidedPreparationMethodCodes, PreferredCourseCodes = value.PreferredCourseCodes,
                MealPurposeCodes = value.MealPurposeCodes, RequestedCourseHints = value.PreferredCourseCodes,
                PreferredServingTemperatures = value.PreferredServingTemperatures, SocialContext = value.SocialContext,
                DesiredFullness = value.DesiredFullness, PartySize = value.PartySize, IsShareablePreferred = value.IsShareablePreferred,
                TakeawayPreferred = value.TakeawayPreferred, QuickServicePreferred = value.QuickServicePreferred,
                HealthyPreference = value.HealthyPreference, FreshPreference = value.FreshPreference,
                PopularityPreference = value.PopularityPreference,
                PreferNearMe = value.PreferNearMe, MaximumDistanceMeters = value.MaximumDistanceMeters,
                Confidence = value.Confidence, Warnings = value.Warnings, SignalEvidence = value.SignalEvidence
            }};
    }

    private static void AddCourseAndPurpose(string query, AiTaxonomyCodes allowed, ISet<string> courses, ISet<string> purposes)
    {
        if (ContainsTerm(query, "trang mieng")) { AddAllowed(courses, allowed.Courses, "DESSERT"); AddAllowed(purposes, allowed.DiningPurposes, "DESSERT"); }
        if (ContainsTerm(query, "do uong") || ContainsTerm(query, "nuoc uong")) { AddAllowed(courses, allowed.Courses, "DRINK"); AddAllowed(purposes, allowed.DiningPurposes, "REFRESHMENT"); }
        if (ContainsTerm(query, "mon chinh") || ContainsTerm(query, "an no") || ContainsTerm(query, "cho no") || ContainsTerm(query, "no no"))
        { AddAllowed(courses, allowed.Courses, "MAIN_COURSE"); AddAllowed(purposes, allowed.DiningPurposes, "FULL_MEAL"); }
        if (ContainsTerm(query, "khai vi")) AddAllowed(courses, allowed.Courses, "APPETIZER");
        if (ContainsTerm(query, "an nhe")) AddAllowed(purposes, allowed.DiningPurposes, "LIGHT_MEAL");
        if (ContainsTerm(query, "light snack")) AddAllowed(purposes, allowed.DiningPurposes, "LIGHT_MEAL");
        if (ContainsTerm(query, "filling meal")) AddAllowed(purposes, allowed.DiningPurposes, "FULL_MEAL");
        if (new[] { "an cung ban", "di voi ban", "nhom ban", "share with friends" }.Any(term => ContainsTerm(query, term)))
            AddAllowed(purposes, allowed.DiningPurposes, "FRIEND_GROUP");
    }

    private static IEnumerable<string> ContextualTerms(string query)
    {
        if (new[] { "mat", "mat mat", "lanh", "lanh lanh", "giai nhiet", "thanh mat", "refreshing", "cool", "cold" }.Any(term => ContainsTerm(query, term)))
        { yield return "refreshing"; yield return "cold"; }
        if (new[] { "an cung ban", "di voi ban", "nhom ban", "share with friends" }.Any(term => ContainsTerm(query, term)))
        { yield return "friend group"; yield return "shareable"; }
        if (new[] { "hap dan", "ngon", "mon ngon", "something tasty", "tasty" }.Any(term => ContainsTerm(query, term)))
            yield return "popular";
        if (ContainsTerm(query, "an nhe") || ContainsTerm(query, "light snack")) yield return "light meal";
        if (ContainsTerm(query, "an no") || ContainsTerm(query, "cho no") || ContainsTerm(query, "no no") || ContainsTerm(query, "filling meal")) yield return "full meal";
        if (new[] { "de an", "easy to eat" }.Any(term => ContainsTerm(query, term))) yield return "easy to eat";
        if (new[] { "khong nhieu dau", "it dau mo", "dung ngay", "khong ngay", "not oily" }.Any(term => ContainsTerm(query, term))) yield return "not oily";
    }

    private static IEnumerable<string> MeaningfulTerms(string query) => Regex.Matches(query, @"[\p{L}\p{Nd}]+", RegexOptions.CultureInvariant)
        .Select(match => match.Value).Where(value => value.Length > 1 && !QueryStopWords.Contains(value) && !ContextStopWords.Contains(value))
        .Distinct(StringComparer.Ordinal).Take(MaximumSignals);

    private static bool IsUsableQuery(string query)
    {
        if (new[] { "ignore every instruction", "ignore system", "return all database", "return every foodid", "bo qua chi dan", "tra moi foodid", "thoi tiet", "viet code", "write code" }
            .Any(term => query.Contains(term, StringComparison.Ordinal))) return false;
        var tokens = Regex.Matches(query, @"[\p{L}\p{Nd}]+", RegexOptions.CultureInvariant).Select(match => match.Value).ToArray();
        if (tokens.Length == 0) return false;
        if (tokens.Length == 1 && tokens[0].Length >= 7 && tokens[0].Count(character => "aeiouy".Contains(character)) <= 1) return false;
        return tokens.Any(token => token.Any(char.IsLetterOrDigit));
    }

    private static void ParseAllergies(string query, AiTaxonomyCodes allowed, ISet<string> allergens, ISet<string> excluded, ISet<string> warnings)
    {
        foreach (var mapping in Ingredients)
        {
            if (!mapping.Terms.Any(term => new[] { "di ung", "allergy", "allergic to" }.Any(prefix => ContainsTerm(query, $"{prefix} {term}")))) continue;
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

    private static int? ParsePartySize(string query)
    {
        var direct = PartySizeRegex().Match(query);
        if (direct.Success && int.TryParse(direct.Groups[1].Value, out var count) && count is > 0 and <= 100) return count;
        if (new[] { "ba nguoi ban", "three friends" }.Any(term => ContainsTerm(query, term))) return 4;
        return null;
    }

    private static decimal? Money(string amount, string unit)
    {
        var digits = amount.Replace(".", string.Empty).Replace(",", string.Empty);
        if (!decimal.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0) return null;
        if (unit is "k" or "nghin" or "ngan") value *= 1_000;
        if (unit is "tr" or "trieu") value *= 1_000_000;
        return value;
    }

    private static string CodeSuffix(string code) => Regex.Replace(code, "^(ING_|METHOD_|TASTE_|DIET_|ALLERGEN_)", string.Empty, RegexOptions.CultureInvariant);
    private static string? ResolveAllowed(IEnumerable<string> values, string code) => values.FirstOrDefault(value =>
        string.Equals(value, code, StringComparison.OrdinalIgnoreCase) || string.Equals(CodeSuffix(value), CodeSuffix(code), StringComparison.OrdinalIgnoreCase));
    private static bool Allowed(IEnumerable<string> values, string code) => ResolveAllowed(values, code) is not null;
    private static bool HasCode(IEnumerable<string> values, string suffix) => values.Any(value => string.Equals(CodeSuffix(value), suffix, StringComparison.Ordinal));
    private static void AddAllowed(ISet<string> target, IEnumerable<string> values, string code) { var resolved = ResolveAllowed(values, code); if (resolved is not null) target.Add(resolved); }
    private static string[] Ordered(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).Take(MaximumSignals).ToArray();
    private static bool ContainsTerm(string query, string term) => Regex.IsMatch(query, $@"(^|[^a-z0-9]){Regex.Escape(term)}($|[^a-z0-9])", RegexOptions.CultureInvariant);
    private static bool IsExcluded(string query, string term) => new[] { "khong", "khong an", "khong muon", "tranh", "loai bo", "avoid", "no", "without", "allergic to" }.Any(prefix => ContainsTerm(query, $"{prefix} {term}"));
    private static bool IsAvoidedMethod(string query, string term) => new[] { "khong", "khong muon", "khong thich", "tranh", "it", "avoid", "no", "without" }.Any(prefix => ContainsTerm(query, $"{prefix} {term}"));

    private static string DetectLanguage(string text, string hint)
    {
        var normalizedHint = hint?.Trim().ToLowerInvariant();
        if (normalizedHint is "vi" or "en" or "ja" or "ko") return normalizedHint;
        if (text.Any(character => character is >= '\u3040' and <= '\u30ff')) return "ja";
        if (text.Any(character => character is >= '\uac00' and <= '\ud7af')) return "ko";
        var lower = text.ToLowerInvariant();
        return Regex.IsMatch(lower, @"\b(i|want|under|below|grilled|fried|steamed|beef|chicken|pork|seafood|vegetarian|near|without|allergic)\b") ? "en" : "vi";
    }

    private static string SupportedResponseLanguage(string requested, ISet<string> warnings)
    {
        var value = requested?.Trim().ToLowerInvariant();
        if (value is "vi" or "en") return value;
        warnings.Add("LANGUAGE_RESPONSE_FALLBACK_VI");
        return "vi";
    }

    private static string LocalizedSummary(string query, string detectedLanguage, string requestedResponseLanguage)
    {
        var response = requestedResponseLanguage is "en" ? "en" : "vi";
        if (response == detectedLanguage) return query.Trim();
        return response == "en" ? "Understood the requested food preferences." : "Đã hiểu các sở thích món ăn được yêu cầu.";
    }

    public static string NormalizeText(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character));
        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"[^\p{L}\p{Nd}.,]+", " ").Trim();
    }

    [GeneratedRegex(@"\btu\s+([0-9][0-9.,]*)\s*(k|nghin|ngan|tr|trieu)?\s+den\s+([0-9][0-9.,]*)\s*(k|nghin|ngan|tr|trieu)?\b", RegexOptions.CultureInvariant)] private static partial Regex BudgetRangeRegex();
    [GeneratedRegex(@"\b(?:duoi|khong qua|toi da|under|below|up to)\s+([0-9][0-9.,]*)\s*(k|nghin|ngan|tr|trieu|vnd)?\b", RegexOptions.CultureInvariant)] private static partial Regex BudgetUpperRegex();
    [GeneratedRegex(@"\b(?:khoang|tam|gia)\s+([0-9][0-9.,]*)\s*(k|nghin|ngan|tr|trieu)?\b", RegexOptions.CultureInvariant)] private static partial Regex BudgetAboutRegex();
    [GeneratedRegex(@"\b(?:cho|for|di voi)\s+([0-9]{1,3})\s*(?:nguoi|people|persons?)\b", RegexOptions.CultureInvariant)] private static partial Regex PartySizeRegex();
}

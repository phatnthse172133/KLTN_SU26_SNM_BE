using System.Text.Json;
using ApplicationLayer.Exceptions;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services.Assistant;

public sealed class AssistantIntentInterpreter(
    ILanguageModelClient languageModel,
    IOptions<OpenAiOptions> openAiOptions)
{
    private readonly OpenAiOptions _openAi = openAiOptions.Value;

    public async Task<ParsedAssistantIntent> InterpretAsync(
        string message,
        IReadOnlyList<AssistantMessage> history,
        CustomerFoodProfile? profile,
        FoodSemanticCatalogSet catalogs,
        AssistantStageAContext context,
        CancellationToken cancellationToken)
    {
        var userPrompt = BuildUserPrompt(message, history, profile, catalogs, context);
        string raw;
        try
        {
            raw = await languageModel.CompleteJsonAsync(
                AssistantPromptCatalog.IntentSystem + "\nJSON schema:\n" + AssistantPromptCatalog.IntentSchema,
                userPrompt,
                _openAi.MaxOutputTokensIntent,
                cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw AssistantErrors.ProviderUnavailable(exception);
        }
        catch (Exception exception)
        {
            throw AssistantErrors.ProviderUnavailable(exception);
        }

        ParsedAssistantIntent parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ParsedAssistantIntent>(raw, AssistantJson.Options)
                ?? throw new JsonException("Intent payload was null.");
        }
        catch (JsonException exception)
        {
            throw AssistantErrors.ProviderUnavailable(exception);
        }

        return Sanitize(parsed, catalogs);
    }

    public static ParsedAssistantIntent Sanitize(ParsedAssistantIntent parsed, FoodSemanticCatalogSet catalogs)
    {
        var allergenCodes = CatalogCodes(catalogs.Allergens);
        var ingredientCodes = CatalogCodes(catalogs.Ingredients);
        var dietaryCodes = CatalogCodes(catalogs.DietaryAttributes);
        var tasteCodes = CatalogCodes(catalogs.TasteProfiles);
        var prepCodes = CatalogCodes(catalogs.PreparationMethods);
        var courseCodes = Enum.GetNames<FoodCourse>().ToHashSet(StringComparer.OrdinalIgnoreCase);

        var semanticPreferences = new List<string>(Normalize(parsed.SemanticPreferences));
        var semanticAvoidances = new List<string>(Normalize(parsed.SemanticAvoidances));
        var hard = parsed.HardConstraints ?? new AssistantHardConstraints();
        var prefs = parsed.StructuredPreferences ?? new AssistantStructuredPreferences();

        return new ParsedAssistantIntent
        {
            Intent = parsed.Intent,
            BudgetMin = parsed.BudgetMin is < 0 ? null : parsed.BudgetMin,
            BudgetMax = parsed.BudgetMax is < 0 ? null : parsed.BudgetMax,
            PartySize = parsed.PartySize is < 1 ? null : parsed.PartySize,
            NeedsLocation = parsed.NeedsLocation,
            DiningContext = string.IsNullOrWhiteSpace(parsed.DiningContext) ? null : parsed.DiningContext.Trim(),
            UserGoal = string.IsNullOrWhiteSpace(parsed.UserGoal) ? null : parsed.UserGoal.Trim(),
            AdditionalMeaning = string.IsNullOrWhiteSpace(parsed.AdditionalMeaning) ? null : parsed.AdditionalMeaning.Trim(),
            AssistantReply = string.IsNullOrWhiteSpace(parsed.AssistantReply) ? null : parsed.AssistantReply.Trim(),
            HardConstraints = new AssistantHardConstraints
            {
                AllergenCodes = KeepKnown(hard.AllergenCodes, allergenCodes, semanticAvoidances),
                AvoidedIngredientCodes = KeepKnown(hard.AvoidedIngredientCodes, ingredientCodes, semanticAvoidances),
                DietaryCodes = KeepKnown(hard.DietaryCodes, dietaryCodes, semanticPreferences),
                MaxSpiceLevel = hard.MaxSpiceLevel is FoodSpiceLevel.UNKNOWN ? null : hard.MaxSpiceLevel,
                AvoidedTasteCodes = KeepKnown(hard.AvoidedTasteCodes, tasteCodes, semanticAvoidances)
            },
            StructuredPreferences = new AssistantStructuredPreferences
            {
                PreferredIngredientCodes = KeepKnown(prefs.PreferredIngredientCodes, ingredientCodes, semanticPreferences),
                PreferredTasteCodes = KeepKnown(prefs.PreferredTasteCodes, tasteCodes, semanticPreferences),
                PreferredPreparationCodes = KeepKnown(prefs.PreferredPreparationCodes, prepCodes, semanticPreferences),
                PreferredCourseCodes = KeepKnown(prefs.PreferredCourseCodes, courseCodes, semanticPreferences)
            },
            SemanticPreferences = semanticPreferences.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            SemanticAvoidances = semanticAvoidances.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static IReadOnlyList<string> KeepKnown(
        IReadOnlyList<string>? values,
        HashSet<string> known,
        List<string> leftover)
    {
        var kept = new List<string>();
        foreach (var value in Normalize(values))
        {
            var match = known.FirstOrDefault(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
            if (match is not null) kept.Add(match);
            else leftover.Add(value);
        }
        return kept.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static HashSet<string> CatalogCodes(IReadOnlyCollection<SemanticCatalogEntity> items)
        => items.Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> Normalize(IReadOnlyList<string>? values)
        => (values ?? []).Select(value => value.Trim()).Where(value => value.Length > 0);

    private static string BuildUserPrompt(
        string message,
        IReadOnlyList<AssistantMessage> history,
        CustomerFoodProfile? profile,
        FoodSemanticCatalogSet catalogs,
        AssistantStageAContext context)
    {
        var hasLocation = context.Latitude is >= -90 and <= 90 && context.Longitude is >= -180 and <= 180;
        var payload = new
        {
            originalMessage = message,
            explicitContext = new
            {
                marketId = context.MarketId,
                locationProvided = hasLocation,
                latitude = hasLocation ? context.Latitude : null,
                longitude = hasLocation ? context.Longitude : null,
                maxDistanceMeters = context.MaxDistanceMeters
            },
            conversationHistory = history.Select(item => new { role = item.Role.ToString(), content = item.Content }).ToArray(),
            customerFoodProfile = profile is null ? null : new
            {
                preferredSpiceLevel = profile.PreferredSpiceLevel?.ToString(),
                preferredPriceMin = profile.PreferredPriceMin,
                preferredPriceMax = profile.PreferredPriceMax,
                defaultMaxDistanceMeters = profile.DefaultMaxDistanceMeters,
                preferredIngredientCodes = profile.PreferredIngredients.Select(item => item.Ingredient.Code).ToArray(),
                avoidedIngredientCodes = profile.AvoidedIngredients.Select(item => item.Ingredient.Code).ToArray(),
                allergenExclusionCodes = profile.AllergenExclusions.Select(item => item.Allergen.Code).ToArray(),
                dietaryRequirementCodes = profile.DietaryRequirements.Select(item => item.DietaryAttribute.Code).ToArray(),
                preferredTasteCodes = profile.PreferredTasteProfiles.Select(item => item.TasteProfile.Code).ToArray(),
                avoidedTasteCodes = profile.AvoidedTasteProfiles.Select(item => item.TasteProfile.Code).ToArray(),
                preferredPreparationCodes = profile.PreferredPreparationMethods.Select(item => item.PreparationMethod.Code).ToArray(),
                preferredCourses = profile.PreferredCourses.Select(item => item.Course.ToString()).ToArray()
            },
            activeCatalogs = new
            {
                ingredients = catalogs.Ingredients.Select(Compact).ToArray(),
                allergens = catalogs.Allergens.Select(Compact).ToArray(),
                dietaryAttributes = catalogs.DietaryAttributes.Select(Compact).ToArray(),
                tasteProfiles = catalogs.TasteProfiles.Select(Compact).ToArray(),
                preparationMethods = catalogs.PreparationMethods.Select(Compact).ToArray()
            }
        };
        return JsonSerializer.Serialize(payload, AssistantJson.Options);
    }

    private static object Compact(SemanticCatalogEntity item) => new { item.Code, item.Name };
}

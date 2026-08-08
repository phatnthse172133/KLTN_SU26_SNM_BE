using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DomainLayer.Entities;
using DomainLayer.Enums;

namespace ApplicationLayer.Services.Menus;

public sealed class FoodAiProfileGenerator : IFoodAiProfileGenerator
{
    public void Rebuild(FoodItem food, DateTime utcNow)
    {
        var searchText = BuildSearchText(food);
        var sourceHash = ComputeSourceHash(food);
        var profile = food.AiProfile;
        if (profile is null)
        {
            food.AiProfile = new FoodAiProfile
            {
                FoodItemId = food.Id,
                SearchText = searchText,
                ContentHash = sourceHash,
                Status = FoodAiProfileStatus.PENDING,
                Version = 1,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };
        }
        else if (string.Equals(profile.ContentHash, sourceHash, StringComparison.Ordinal))
        {
            // Source unchanged: READY/DISABLED skip regeneration. Still refresh local SearchText
            // only when not READY (READY SearchText may include merged AI terms).
            if (profile.Status is FoodAiProfileStatus.READY or FoodAiProfileStatus.DISABLED)
            {
                // no-op for enrichment invalidation
            }
            else if (!string.Equals(profile.SearchText, searchText, StringComparison.Ordinal))
            {
                profile.SearchText = searchText;
                profile.UpdatedAt = utcNow;
            }
        }
        else
        {
            profile.SearchText = searchText;
            profile.ContentHash = sourceHash;
            ClearEnrichmentFields(profile);
            if (profile.Status != FoodAiProfileStatus.DISABLED)
                profile.Status = FoodAiProfileStatus.PENDING;
            profile.Version++;
            profile.UpdatedAt = utcNow;
        }

        food.SemanticProfileVersion = food.AiProfile?.Version ?? 1;
        food.SemanticProfileUpdatedAt = utcNow;
    }

    public static string ComputeSourceHash(FoodItem food)
    {
        var parts = new List<string?> { food.Name, food.Description };
        parts.AddRange(food.Ingredients.OrderBy(x => x.Ingredient.Code, StringComparer.Ordinal)
            .Select(x => x.Ingredient.Code));
        var fingerprint = string.Join("\u001f", parts
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => Normalize(x!)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
    }

    public static string BuildSearchText(FoodItem food)
    {
        var values = new List<string?> { food.Name, food.Description, food.Category?.Code, food.Category?.Name };
        values.AddRange(food.Courses.OrderBy(x => x.Course).Select(x => x.Course.ToString()));
        values.AddRange(food.Ingredients.OrderBy(x => x.Ingredient.Code).Select(x => x.Ingredient.Code));
        values.AddRange(food.PreparationMethods.OrderBy(x => x.PreparationMethod.Code).Select(x => x.PreparationMethod.Code));
        values.AddRange(food.TasteProfiles.OrderBy(x => x.TasteProfile.Code).Select(x => x.TasteProfile.Code));
        values.AddRange(food.DietaryAttributes.Where(x => x.Source != MetadataSource.UNKNOWN).OrderBy(x => x.DietaryAttribute.Code).Select(x => x.DietaryAttribute.Code));
        if (food.SpiceLevel != FoodSpiceLevel.UNKNOWN) values.Add(food.SpiceLevel.ToString());
        if (food.ServingTemperature is not null and not ServingTemperature.UNKNOWN) values.Add(food.ServingTemperature.ToString());
        if (food.EstimatedServingCount.HasValue) values.Add($"serves {food.EstimatedServingCount.Value}");
        values.Add(food.ServingSizeDescription);
        if (food.IsShareable == true) values.Add("shareable");
        return JoinSearchTerms(values);
    }

    public static string MergeSearchText(string localSearchText, IEnumerable<string?> aiTerms)
        => JoinSearchTerms(
            localSearchText.Split(" | ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Concat(aiTerms));

    public static void ClearEnrichmentFields(FoodAiProfile profile)
    {
        profile.AiDescription = null;
        profile.GeneratedByModel = null;
        profile.Confidence = null;
        profile.StructuredProfileJson = null;
        profile.Embedding = null;
        profile.EmbeddingModel = null;
        profile.EmbeddedAt = null;
        profile.LastError = null;
    }

    private static string JoinSearchTerms(IEnumerable<string?> values)
        => string.Join(" | ", values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Normalize(x!))
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal));

    private static string Normalize(string value)
        => Regex.Replace(value.Normalize(NormalizationForm.FormKC).Trim(), @"\s+", " ").ToLower(CultureInfo.InvariantCulture);
}

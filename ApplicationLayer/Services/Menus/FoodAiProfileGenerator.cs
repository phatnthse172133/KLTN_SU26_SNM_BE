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
        var text = BuildSearchText(food);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        var profile = food.AiProfile;
        if (profile is null)
        {
            food.AiProfile = new FoodAiProfile { FoodItemId = food.Id, SearchText = text, ContentHash = hash, Status = FoodAiProfileStatus.PENDING, Version = 1, CreatedAt = utcNow, UpdatedAt = utcNow };
        }
        else if (!string.Equals(profile.ContentHash, hash, StringComparison.Ordinal) || !string.Equals(profile.SearchText, text, StringComparison.Ordinal))
        {
            profile.SearchText = text;
            profile.ContentHash = hash;
            profile.Status = profile.Status == FoodAiProfileStatus.DISABLED ? FoodAiProfileStatus.DISABLED : FoodAiProfileStatus.STALE;
            profile.Embedding = null;
            profile.EmbeddingModel = null;
            profile.EmbeddedAt = null;
            profile.Version++;
            profile.UpdatedAt = utcNow;
        }
        food.SemanticProfileVersion = profile?.Version ?? 1;
        food.SemanticProfileUpdatedAt = utcNow;
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
        return string.Join(" | ", values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Normalize(x!)).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal));
    }

    private static string Normalize(string value)
        => Regex.Replace(value.Normalize(NormalizationForm.FormKC).Trim(), @"\s+", " ").ToLower(CultureInfo.InvariantCulture);
}

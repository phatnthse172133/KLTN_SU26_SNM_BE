using ApplicationLayer.Exceptions;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.FoodTags;

public static class FoodTagSelectionPolicy
{
    private static readonly HashSet<string> SpicyLevels =
        ["TASTE_MILD_SPICY", "TASTE_SPICY", "TASTE_VERY_SPICY"];

    public static void Validate(IReadOnlyCollection<FoodTag> tags)
    {
        if (tags.Any(tag => !tag.IsSelectable || tag.IsAutoAssigned))
            throw AppException.BadRequest("Budget and system-assigned food tags cannot be selected manually.");

        if (tags.Count(tag => tag.TagGroup == FoodTagGroup.Taste) > 3)
            throw AppException.BadRequest("A food item can have at most three taste tags.");

        if (tags.Count(tag => SpicyLevels.Contains(tag.Code)) > 1)
            throw AppException.BadRequest("Mild spicy, spicy, and very spicy tags are mutually exclusive.");

        if (tags.Count(tag => tag.TagGroup == FoodTagGroup.Temperature) > 1)
            throw AppException.BadRequest("A food item can have at most one temperature tag.");

        if (tags.Count(tag => tag.TagGroup == FoodTagGroup.Ingredient) > 5)
            throw AppException.BadRequest("A food item can have at most five main ingredient tags.");
    }
}

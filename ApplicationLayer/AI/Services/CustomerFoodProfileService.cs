using ApplicationLayer.AI.DTOs;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.AI.Services;

public sealed class CustomerFoodProfileService(IFoodSemanticMetadataRepository repository) : ICustomerFoodProfileService
{
    public async Task<ApiResponse<CustomerFoodProfileResponse>> GetMineAsync(Guid customerId, CancellationToken ct = default)
    {
        var profile = await repository.GetCustomerProfileAsync(customerId, false, ct);
        return ApiResponse<CustomerFoodProfileResponse>.SuccessResponse(profile is null ? new() : Map(profile));
    }

    public async Task<ApiResponse<CustomerFoodProfileResponse>> UpdateMineAsync(Guid customerId, UpdateCustomerFoodProfileRequest request, CancellationToken ct = default)
    {
        Validate(request);
        var ingredients = await repository.GetActiveIngredientsAsync(request.PreferredIngredientIds.Concat(request.AvoidedIngredientIds).Distinct().ToArray(), ct);
        var allergens = await repository.GetActiveAllergensAsync(request.AllergenExclusionIds, ct);
        var dietary = await repository.GetActiveDietaryAttributesAsync(request.DietaryRequirementIds, ct);
        var preparations = await repository.GetActivePreparationMethodsAsync(request.PreferredPreparationMethodIds, ct);
        var tastes = await repository.GetActiveTasteProfilesAsync(request.PreferredTasteProfileIds.Concat(request.AvoidedTasteProfileIds).Distinct().ToArray(), ct);
        if (ingredients.Count != request.PreferredIngredientIds.Concat(request.AvoidedIngredientIds).Distinct().Count()
            || allergens.Count != request.AllergenExclusionIds.Count || dietary.Count != request.DietaryRequirementIds.Count
            || preparations.Count != request.PreferredPreparationMethodIds.Count
            || tastes.Count != request.PreferredTasteProfileIds.Concat(request.AvoidedTasteProfileIds).Distinct().Count())
            throw AppException.BadRequest("One or more food profile catalog IDs are invalid or inactive.", "AI_INVALID_REQUEST");

        var now = DateTime.UtcNow;
        var profile = await repository.GetCustomerProfileAsync(customerId, true, ct);
        if (profile is null)
        {
            profile = new CustomerFoodProfile { CustomerId = customerId, CreatedAt = now };
            repository.AddCustomerProfile(profile);
        }
        profile.PreferredSpiceLevel = request.PreferredSpiceLevel; profile.PreferredPriceMin = request.PreferredPriceMin;
        profile.PreferredPriceMax = request.PreferredPriceMax; profile.DefaultMaxDistanceMeters = request.DefaultMaxDistanceMeters; profile.UpdatedAt = now;
        Sync(profile.PreferredIngredients, request.PreferredIngredientIds, x => x.IngredientId, id => new() { CustomerId = customerId, IngredientId = id, Ingredient = ingredients.Single(x => x.Id == id), CreatedAt = now });
        Sync(profile.AvoidedIngredients, request.AvoidedIngredientIds, x => x.IngredientId, id => new() { CustomerId = customerId, IngredientId = id, Ingredient = ingredients.Single(x => x.Id == id), CreatedAt = now });
        Sync(profile.DietaryRequirements, request.DietaryRequirementIds, x => x.DietaryAttributeId, id => new() { CustomerId = customerId, DietaryAttributeId = id, DietaryAttribute = dietary.Single(x => x.Id == id), CreatedAt = now });
        Sync(profile.AllergenExclusions, request.AllergenExclusionIds, x => x.AllergenId, id => new() { CustomerId = customerId, AllergenId = id, Allergen = allergens.Single(x => x.Id == id), CreatedAt = now });
        Sync(profile.PreferredPreparationMethods, request.PreferredPreparationMethodIds, x => x.PreparationMethodId, id => new() { CustomerId = customerId, PreparationMethodId = id, PreparationMethod = preparations.Single(x => x.Id == id), CreatedAt = now });
        Sync(profile.PreferredTasteProfiles, request.PreferredTasteProfileIds, x => x.TasteProfileId, id => new() { CustomerId = customerId, TasteProfileId = id, TasteProfile = tastes.Single(x => x.Id == id), CreatedAt = now });
        Sync(profile.AvoidedTasteProfiles, request.AvoidedTasteProfileIds, x => x.TasteProfileId, id => new() { CustomerId = customerId, TasteProfileId = id, TasteProfile = tastes.Single(x => x.Id == id), CreatedAt = now });
        Sync(profile.PreferredCourses, request.PreferredCourses, x => x.Course, course => new() { CustomerId = customerId, Course = course, CreatedAt = now });
        await repository.SaveChangesAsync(ct);
        return ApiResponse<CustomerFoodProfileResponse>.SuccessResponse(Map(profile), "Customer food profile updated successfully.");
    }

    private static void Validate(UpdateCustomerFoodProfileRequest request)
    {
        static void Unique<T>(IReadOnlyCollection<T> values, string label) where T : notnull
        { if (values.Count != values.Distinct().Count()) throw AppException.BadRequest($"{label} must not contain duplicates.", "AI_INVALID_REQUEST"); }
        Unique(request.PreferredIngredientIds, "Preferred ingredients"); Unique(request.AvoidedIngredientIds, "Avoided ingredients");
        Unique(request.DietaryRequirementIds, "Dietary requirements"); Unique(request.AllergenExclusionIds, "Allergen exclusions");
        Unique(request.PreferredPreparationMethodIds, "Preparation methods"); Unique(request.PreferredTasteProfileIds, "Preferred tastes");
        Unique(request.AvoidedTasteProfileIds, "Avoided tastes"); Unique(request.PreferredCourses, "Preferred courses");
        if (request.PreferredIngredientIds.Intersect(request.AvoidedIngredientIds).Any()) throw AppException.BadRequest("An avoided ingredient cannot also be preferred.", "AI_PROFILE_INGREDIENT_CONFLICT");
        if (request.PreferredTasteProfileIds.Intersect(request.AvoidedTasteProfileIds).Any()) throw AppException.BadRequest("An avoided taste cannot also be preferred.", "AI_PROFILE_TASTE_CONFLICT");
        if (request.PreferredPriceMin > request.PreferredPriceMax) throw AppException.BadRequest("Preferred minimum price cannot exceed maximum price.", "AI_INVALID_REQUEST");
        if (request.PreferredCourses.Any(x => !Enum.IsDefined(x))) throw AppException.BadRequest("Preferred course is invalid.", "AI_INVALID_REQUEST");
        if (request.PreferredSpiceLevel.HasValue && !Enum.IsDefined(request.PreferredSpiceLevel.Value)) throw AppException.BadRequest("Preferred spice level is invalid.", "AI_INVALID_REQUEST");
        if (request.DefaultMaxDistanceMeters <= 0) throw AppException.BadRequest("Default maximum distance must be greater than zero.", "AI_INVALID_REQUEST");
    }

    private static CustomerFoodProfileResponse Map(CustomerFoodProfile profile) => new()
    {
        PreferredIngredients = profile.PreferredIngredients.OrderBy(x => x.Ingredient.Code).Select(x => Catalog(x.Ingredient)).ToArray(),
        AvoidedIngredients = profile.AvoidedIngredients.OrderBy(x => x.Ingredient.Code).Select(x => Catalog(x.Ingredient)).ToArray(),
        DietaryRequirements = profile.DietaryRequirements.OrderBy(x => x.DietaryAttribute.Code).Select(x => Catalog(x.DietaryAttribute)).ToArray(),
        AllergenExclusions = profile.AllergenExclusions.OrderBy(x => x.Allergen.Code).Select(x => Catalog(x.Allergen)).ToArray(),
        PreferredPreparationMethods = profile.PreferredPreparationMethods.OrderBy(x => x.PreparationMethod.Code).Select(x => Catalog(x.PreparationMethod)).ToArray(),
        PreferredTasteProfiles = profile.PreferredTasteProfiles.OrderBy(x => x.TasteProfile.Code).Select(x => Catalog(x.TasteProfile)).ToArray(),
        AvoidedTasteProfiles = profile.AvoidedTasteProfiles.OrderBy(x => x.TasteProfile.Code).Select(x => Catalog(x.TasteProfile)).ToArray(),
        PreferredCourses = profile.PreferredCourses.OrderBy(x => x.Course).Select(x => x.Course).ToArray(), PreferredSpiceLevel = profile.PreferredSpiceLevel,
        PreferredPriceMin = profile.PreferredPriceMin, PreferredPriceMax = profile.PreferredPriceMax, DefaultMaxDistanceMeters = profile.DefaultMaxDistanceMeters
    };
    private static SemanticCatalogResponse Catalog(SemanticCatalogEntity value) => new(value.Id, value.Code, value.Name);

    private static void Sync<TItem, TKey>(ICollection<TItem> current, IReadOnlyCollection<TKey> requested, Func<TItem, TKey> key, Func<TKey, TItem> create)
        where TKey : notnull
    {
        var desired = requested.ToHashSet();
        foreach (var item in current.Where(item => !desired.Contains(key(item))).ToArray()) current.Remove(item);
        var existing = current.Select(key).ToHashSet();
        foreach (var value in requested.Where(value => !existing.Contains(value))) current.Add(create(value));
    }
}

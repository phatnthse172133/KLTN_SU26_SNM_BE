using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IFoodSemanticMetadataRepository
{
    Task<FoodSemanticCatalogSet> GetActiveCatalogsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Ingredient>> GetActiveIngredientsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Allergen>> GetActiveAllergensAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DietaryAttribute>> GetActiveDietaryAttributesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PreparationMethod>> GetActivePreparationMethodsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TasteProfile>> GetActiveTasteProfilesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<CustomerFoodProfile?> GetCustomerProfileAsync(Guid customerId, bool tracking, CancellationToken cancellationToken = default);
    void AddCustomerProfile(CustomerFoodProfile profile);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record FoodSemanticCatalogSet(
    IReadOnlyCollection<Ingredient> Ingredients,
    IReadOnlyCollection<Allergen> Allergens,
    IReadOnlyCollection<DietaryAttribute> DietaryAttributes,
    IReadOnlyCollection<PreparationMethod> PreparationMethods,
    IReadOnlyCollection<TasteProfile> TasteProfiles);

using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public sealed class FoodSemanticMetadataRepository(SNMDbContext db) : IFoodSemanticMetadataRepository
{
    public async Task<FoodSemanticCatalogSet> GetActiveCatalogsAsync(CancellationToken ct = default)
        => new(await All(db.Ingredients, ct), await All(db.Allergens, ct), await All(db.DietaryAttributes, ct), await All(db.PreparationMethods, ct), await All(db.TasteProfiles, ct));
    public Task<IReadOnlyCollection<Ingredient>> GetActiveIngredientsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => Active(db.Ingredients, ids, ct);
    public Task<IReadOnlyCollection<Allergen>> GetActiveAllergensAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => Active(db.Allergens, ids, ct);
    public Task<IReadOnlyCollection<DietaryAttribute>> GetActiveDietaryAttributesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => Active(db.DietaryAttributes, ids, ct);
    public Task<IReadOnlyCollection<PreparationMethod>> GetActivePreparationMethodsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => Active(db.PreparationMethods, ids, ct);
    public Task<IReadOnlyCollection<TasteProfile>> GetActiveTasteProfilesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => Active(db.TasteProfiles, ids, ct);

    private static async Task<IReadOnlyCollection<T>> Active<T>(DbSet<T> set, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        where T : SemanticCatalogEntity
        => await set.Where(x => ids.Contains(x.Id) && x.IsActive).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code).ToListAsync(ct);

    private static async Task<IReadOnlyCollection<T>> All<T>(DbSet<T> set, CancellationToken ct) where T : SemanticCatalogEntity
        => await set.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code).ToListAsync(ct);
}

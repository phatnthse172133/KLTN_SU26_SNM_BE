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

    public async Task<CustomerFoodProfile?> GetCustomerProfileAsync(Guid customerId, bool tracking, CancellationToken ct = default)
    {
        IQueryable<CustomerFoodProfile> query = db.CustomerFoodProfiles
            .Include(x => x.PreferredIngredients).ThenInclude(x => x.Ingredient)
            .Include(x => x.AvoidedIngredients).ThenInclude(x => x.Ingredient)
            .Include(x => x.DietaryRequirements).ThenInclude(x => x.DietaryAttribute)
            .Include(x => x.AllergenExclusions).ThenInclude(x => x.Allergen)
            .Include(x => x.PreferredPreparationMethods).ThenInclude(x => x.PreparationMethod)
            .Include(x => x.PreferredTasteProfiles).ThenInclude(x => x.TasteProfile)
            .Include(x => x.AvoidedTasteProfiles).ThenInclude(x => x.TasteProfile)
            .Include(x => x.PreferredCourses);
        if (!tracking) query = query.AsNoTracking();
        return await query.AsSplitQuery().SingleOrDefaultAsync(x => x.CustomerId == customerId, ct);
    }

    public void AddCustomerProfile(CustomerFoodProfile profile) => db.CustomerFoodProfiles.Add(profile);
    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    private static async Task<IReadOnlyCollection<T>> Active<T>(DbSet<T> set, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        where T : SemanticCatalogEntity
        => await set.Where(x => ids.Contains(x.Id) && x.IsActive).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code).ToListAsync(ct);

    private static async Task<IReadOnlyCollection<T>> All<T>(DbSet<T> set, CancellationToken ct) where T : SemanticCatalogEntity
        => await set.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code).ToListAsync(ct);
}

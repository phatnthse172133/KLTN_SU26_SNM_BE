using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class FoodCategoryRepository : GenericRepository<FoodCategory>, IFoodCategoryRepository
{
    public FoodCategoryRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<FoodCategory>> GetActivePagedByBoothAsync(
        Guid boothId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Where(category => category.BoothId == boothId && !category.IsDeleted)
            .OrderBy(category => category.Name);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FoodCategory>(items, total);
    }

    public async Task<FoodCategory?> GetActiveByBoothAsync(Guid boothId, Guid categoryId)
        => await _dbSet.FirstOrDefaultAsync(category => category.Id == categoryId && category.BoothId == boothId && !category.IsDeleted);

    public async Task<bool> ActiveNameExistsAsync(Guid boothId, string name, Guid? excludeId = null)
        => await _dbSet.AnyAsync(category =>
            category.BoothId == boothId &&
            !category.IsDeleted &&
            category.Name.ToLower() == name.ToLower() &&
            (!excludeId.HasValue || category.Id != excludeId.Value));

    public async Task<IReadOnlyCollection<FoodCategory>> GetByIdsAsync(IReadOnlyCollection<Guid> ids)
        => await _dbSet.Where(category => ids.Contains(category.Id)).ToListAsync();

    public async Task<IReadOnlyCollection<FoodCategory>> GetActiveByIdsAndBoothAsync(
        Guid boothId,
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken = default)
        => await ActiveQuery()
            .Where(category => category.BoothId == boothId && categoryIds.Contains(category.Id))
            .ToListAsync(cancellationToken);
}

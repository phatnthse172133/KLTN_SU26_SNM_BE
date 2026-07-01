using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class FoodItemRepository : GenericRepository<FoodItem>, IFoodItemRepository
{
    public FoodItemRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<FoodItem>> GetMenuByBoothPagedAsync(
        Guid boothId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(item => item.Category)
            .Where(item => item.BoothId == boothId && !item.IsDeleted);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.IsFeatured)
            .ThenByDescending(item => item.IsAvailable)
            .ThenBy(item => item.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FoodItem>(items, totalCount);
    }

    public async Task<FoodItem?> GetByBoothAsync(Guid boothId, Guid foodItemId)
        => await _dbSet
            .Include(item => item.Category)
            .FirstOrDefaultAsync(item => item.Id == foodItemId && item.BoothId == boothId && !item.IsDeleted);

    public Task<FoodItem?> GetForCartAsync(
        Guid foodItemId,
        CancellationToken cancellationToken = default)
        => ActiveQuery()
            .Include(item => item.Booth)
            .Include(item => item.Category)
            .Include(item => item.FoodPrices)
            .FirstOrDefaultAsync(item => item.Id == foodItemId, cancellationToken);
}

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

    public async Task<IReadOnlyCollection<FoodItem>> GetMenuByBoothAsync(Guid boothId)
        => await _dbSet
            .Include(item => item.Category)
            .Where(item => item.BoothId == boothId && !item.IsDeleted)
            .OrderByDescending(item => item.IsFeatured)
            .ThenByDescending(item => item.IsAvailable)
            .ThenBy(item => item.Name)
            .ToListAsync();

    public async Task<FoodItem?> GetByBoothAsync(Guid boothId, Guid foodItemId)
        => await _dbSet
            .Include(item => item.Category)
            .FirstOrDefaultAsync(item => item.Id == foodItemId && item.BoothId == boothId && !item.IsDeleted);
}

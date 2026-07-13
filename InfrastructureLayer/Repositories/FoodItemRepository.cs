using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

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

    public Task<FoodItem?> GetWithTagsAsync(
        Guid foodItemId,
        CancellationToken cancellationToken = default)
        => ActiveQuery()
            .Include(item => item.Booth)
            .Include(item => item.FoodItemTags)
            .FirstOrDefaultAsync(item => item.Id == foodItemId, cancellationToken);

    public async Task<IReadOnlyCollection<FoodItem>> GetActiveByIdsAndBoothAsync(
        Guid boothId,
        IReadOnlyCollection<Guid> foodItemIds,
        CancellationToken cancellationToken = default)
        => await ActiveQuery()
            .Where(item => item.BoothId == boothId && foodItemIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<FoodItem>> GetAiCandidatesAsync(
        Guid? nightMarketId,
        CancellationToken cancellationToken = default)
    {
        var query = ActiveQuery()
            .Include(item => item.Category)
            .Include(item => item.FoodItemTags)
                .ThenInclude(foodItemTag => foodItemTag.FoodTag)
            .Include(item => item.Booth)
                .ThenInclude(booth => booth.NightMarket)
            .Include(item => item.Booth)
                .ThenInclude(booth => booth.Zone)
            .Where(item =>
                item.IsAvailable
                && item.Booth.Status == BoothStatus.Active
                && item.Booth.NightMarket.Status == NightMarketStatus.Open
                && !item.Booth.NightMarket.IsDeleted);

        if (nightMarketId.HasValue)
        {
            query = query.Where(item => item.Booth.NightMarketId == nightMarketId.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }
}

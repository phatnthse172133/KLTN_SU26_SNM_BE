using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class FoodPriceRepository : GenericRepository<FoodPrice>, IFoodPriceRepository
{
    public FoodPriceRepository(SNMDbContext context) : base(context) { }

    public async Task<PagedResult<FoodPrice>> GetByFoodItemPagedAsync(
        Guid foodItemId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = ActiveQuery()
            .AsNoTracking()
            .Where(price => price.FoodItemId == foodItemId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(price => price.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FoodPrice>(items, totalCount);
    }
}

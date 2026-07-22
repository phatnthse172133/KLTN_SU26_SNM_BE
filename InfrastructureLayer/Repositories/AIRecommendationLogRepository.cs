using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class AIRecommendationLogRepository : GenericRepository<AIRecommendationLog>, IAIRecommendationLogRepository
{
    public AIRecommendationLogRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<AIRecommendationLog>> GetPagedLogsAsync(
        string? search,
        AIRecommendationType? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(x => x.Customer)
            .Include(x => x.NightMarket)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(x =>
                (x.Customer != null && x.Customer.FullName.ToLower().Contains(searchLower)) ||
                (x.Customer != null && x.Customer.Email.ToLower().Contains(searchLower)) ||
                (x.NightMarket != null && x.NightMarket.Name.ToLower().Contains(searchLower)));
        }

        if (type.HasValue)
        {
            query = query.Where(x => x.RecommendationType == type.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AIRecommendationLog>(items, total);
    }
}

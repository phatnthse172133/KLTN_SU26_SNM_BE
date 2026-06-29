using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class NightMarketRepository : GenericRepository<NightMarket>, INightMarketRepository
{
    public NightMarketRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<NightMarket>> GetActivePagedAsync(
        string? keyword,
        NightMarketStatus? status,
        int page,
        int pageSize,
        string sortBy,
        bool ascending,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(market => !market.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim().ToLower();
            query = query.Where(market =>
                market.Name.ToLower().Contains(normalizedKeyword) ||
                market.Address.ToLower().Contains(normalizedKeyword));
        }

        if (status.HasValue)
            query = query.Where(market => market.Status == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        query = ApplySorting(query, sortBy, ascending);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NightMarket>(items, totalCount);
    }

    public async Task<NightMarket?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbSet.FirstOrDefaultAsync(market => market.Id == id && !market.IsDeleted, cancellationToken);

    public async Task<bool> ActiveNameExistsAsync(
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim().ToLower();
        return await _dbSet.AnyAsync(market =>
            !market.IsDeleted &&
            market.Name.ToLower() == normalizedName &&
            (!excludeId.HasValue || market.Id != excludeId.Value),
            cancellationToken);
    }

    private static IQueryable<NightMarket> ApplySorting(
        IQueryable<NightMarket> query,
        string sortBy,
        bool ascending)
        => (sortBy.ToLowerInvariant(), ascending) switch
        {
            ("name", true) => query.OrderBy(market => market.Name),
            ("name", false) => query.OrderByDescending(market => market.Name),
            ("status", true) => query.OrderBy(market => market.Status),
            ("status", false) => query.OrderByDescending(market => market.Status),
            ("updatedat", true) => query.OrderBy(market => market.UpdatedAt),
            ("updatedat", false) => query.OrderByDescending(market => market.UpdatedAt),
            ("createdat", true) => query.OrderBy(market => market.CreatedAt),
            _ => query.OrderByDescending(market => market.CreatedAt)
        };
}

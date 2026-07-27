using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class NightMarketRepository : GenericRepository<NightMarket>, INightMarketRepository
{
    public NightMarketRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<NightMarketCustomerReadModel>> GetCustomerPagedAsync(
        string? keyword,
        bool? openNow,
        TimeOnly localTime,
        int page,
        int pageSize,
        string sortBy,
        bool ascending,
        CancellationToken cancellationToken = default)
    {
        var query = CustomerVisibleQuery();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim().ToLower();
            query = query.Where(market =>
                market.Name.ToLower().Contains(normalizedKeyword) ||
                market.Address.ToLower().Contains(normalizedKeyword));
        }

        if (openNow.HasValue)
        {
            var isOpen = IsCustomerOpenAt(localTime);
            query = query.Where(openNow.Value ? isOpen : Negate(isOpen));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        query = ApplyCustomerSorting(query, sortBy, ascending);

        var items = await ProjectCustomer(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NightMarketCustomerReadModel>(items, totalCount);
    }

    public Task<NightMarketCustomerReadModel?> GetCustomerByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => ProjectCustomer(CustomerVisibleQuery().Where(market => market.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> CustomerVisibleExistsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => CustomerVisibleQuery().AnyAsync(market => market.Id == id, cancellationToken);

    public async Task<PagedResult<NightMarket>> GetActivePagedAsync(
        string? keyword,
        NightMarketStatus? status,
        int page,
        int pageSize,
        string sortBy,
        bool ascending,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(market => !market.IsDeleted && market.ModerationStatus != ModerationStatus.Suspended);

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
        => await _dbSet.FirstOrDefaultAsync(market => market.Id == id && !market.IsDeleted && market.ModerationStatus != ModerationStatus.Suspended, cancellationToken);

    public async Task<bool> ActiveNameExistsAsync(
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim().ToLower();
        return await _dbSet.AnyAsync(market =>
            !market.IsDeleted &&
            market.ModerationStatus != ModerationStatus.Suspended &&
            market.Name.ToLower() == normalizedName &&
            (!excludeId.HasValue || market.Id != excludeId.Value),
            cancellationToken);
    }

    public async Task<List<NightMarket>> GetByOwnerIdAsync(Guid marketOwnerId, CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking()
            .Where(market => market.MarketOwnerId == marketOwnerId && !market.IsDeleted && market.ModerationStatus != ModerationStatus.Suspended)
            .OrderByDescending(market => market.CreatedAt)
            .ToListAsync(cancellationToken);

    private IQueryable<NightMarket> CustomerVisibleQuery()
        => _dbSet.AsNoTracking().Where(market =>
            !market.IsDeleted &&
            market.ModerationStatus == ModerationStatus.Active &&
            market.Status == NightMarketStatus.Active);

    private static Expression<Func<NightMarket, bool>> IsCustomerOpenAt(TimeOnly localTime)
        => market =>
            market.Status == NightMarketStatus.Active &&
            market.OpeningHours.HasValue &&
            market.ClosingHours.HasValue &&
            market.OpeningHours.Value != market.ClosingHours.Value &&
            (market.OpeningHours.Value < market.ClosingHours.Value
                ? market.OpeningHours.Value <= localTime && localTime < market.ClosingHours.Value
                : localTime >= market.OpeningHours.Value || localTime < market.ClosingHours.Value);

    private static Expression<Func<NightMarket, bool>> Negate(Expression<Func<NightMarket, bool>> predicate)
        => Expression.Lambda<Func<NightMarket, bool>>(Expression.Not(predicate.Body), predicate.Parameters);

    private static IQueryable<NightMarketCustomerReadModel> ProjectCustomer(IQueryable<NightMarket> query)
        => query.Select(market => new NightMarketCustomerReadModel(
            market.Id,
            market.Name,
            market.Description,
            market.Address,
            market.Latitude,
            market.Longitude,
            market.OpeningHours,
            market.ClosingHours,
            market.ThumbnailUrl,
            market.Status,
            market.Booths.Count(booth => booth.Status == BoothStatus.Active),
            market.MarketLayouts.Any(layout =>
                !layout.IsDeleted && layout.Status == MarketLayoutStatus.Active),
            market.BoundaryWidthMeters,
            market.BoundaryHeightMeters));

    private static IQueryable<NightMarket> ApplyCustomerSorting(
        IQueryable<NightMarket> query,
        string sortBy,
        bool ascending)
        => (sortBy.ToLowerInvariant(), ascending) switch
        {
            ("name", true) => query.OrderBy(market => market.Name).ThenBy(market => market.Id),
            ("name", false) => query.OrderByDescending(market => market.Name).ThenBy(market => market.Id),
            ("boothcount", true) => query.OrderBy(market => market.Booths.Count(booth => booth.Status == BoothStatus.Active)).ThenBy(market => market.Id),
            ("boothcount", false) => query.OrderByDescending(market => market.Booths.Count(booth => booth.Status == BoothStatus.Active)).ThenBy(market => market.Id),
            ("createdat", true) => query.OrderBy(market => market.CreatedAt).ThenBy(market => market.Id),
            _ => query.OrderByDescending(market => market.CreatedAt).ThenBy(market => market.Id)
        };

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

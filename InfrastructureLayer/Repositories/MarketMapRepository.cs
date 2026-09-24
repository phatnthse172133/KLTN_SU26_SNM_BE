using DomainLayer.Entities;
using DomainLayer.InterfaceRepositories;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public sealed class MarketMapRepository : GenericRepository<MarketMap>, IMarketMapRepository
{
    public MarketMapRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyCollection<MarketMap>> GetByMarketIdAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking()
            .Include(map => map.MarketLayouts.Where(layout => !layout.IsDeleted))
            .Where(map => map.NightMarketId == nightMarketId)
            .OrderByDescending(map => map.Version)
            .ToListAsync(cancellationToken);

    public Task<MarketMap?> GetDetailAsync(
        Guid nightMarketId, Guid marketMapId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .AsSplitQuery()
            .Include(map => map.MarketLayouts.Where(layout => !layout.IsDeleted))
                .ThenInclude(layout => layout.LayoutNodes.Where(node => !node.IsDeleted))
            .FirstOrDefaultAsync(map =>
                map.Id == marketMapId && map.NightMarketId == nightMarketId,
                cancellationToken);

    public Task<MarketMap?> GetManagementDetailAsync(
        Guid marketMapId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .AsSplitQuery()
            .Include(map => map.MarketLayouts)
                .ThenInclude(layout => layout.LayoutNodes.Where(node => !node.IsDeleted))
            .FirstOrDefaultAsync(map => map.Id == marketMapId, cancellationToken);

    public Task<MarketMap?> GetManagementDetailForUpdateAsync(
        Guid marketMapId, CancellationToken cancellationToken = default)
        => _dbSet
            .AsSplitQuery()
            .Include(map => map.MarketLayouts)
                .ThenInclude(layout => layout.LayoutNodes.Where(node => !node.IsDeleted))
            .FirstOrDefaultAsync(map => map.Id == marketMapId, cancellationToken);

    public Task<MarketMap?> GetActiveByMarketIdAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().SingleOrDefaultAsync(
            map => map.NightMarketId == nightMarketId && map.Status == MarketMapStatus.Active,
            cancellationToken);

    public Task<MarketMap?> GetCustomerActiveDetailAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .AsSplitQuery()
            .Include(map => map.MarketLayouts)
                .ThenInclude(layout => layout.LayoutNodes.Where(node => !node.IsDeleted))
            .Include(map => map.MarketLayouts)
                .ThenInclude(layout => layout.LayoutEdges.Where(edge => !edge.IsDeleted))
            .Include(map => map.MarketLayouts)
                .ThenInclude(layout => layout.BoothLocations.Where(location =>
                    !location.IsDeleted &&
                    location.ReleasedAt == null &&
                    location.Booth.Status == BoothStatus.Active &&
                    location.Booth.NightMarketId == nightMarketId &&
                    !location.Booth.NightMarket.IsDeleted &&
                    location.Booth.NightMarket.Status == NightMarketStatus.Active &&
                    location.Booth.NightMarket.ModerationStatus == ModerationStatus.Active))
                    .ThenInclude(location => location.Booth)
            .Include(map => map.MarketLayouts)
                .ThenInclude(layout => layout.NavigationAnchors.Where(anchor =>
                    !anchor.IsDeleted && anchor.IsActive && anchor.IsCustomerAccessible))
            .SingleOrDefaultAsync(map =>
                map.NightMarketId == nightMarketId && map.Status == MarketMapStatus.Active,
                cancellationToken);

    public Task<MarketMap?> GetActiveForUpdateAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(
            map => map.NightMarketId == nightMarketId && map.Status == MarketMapStatus.Active,
            cancellationToken);

    public async Task<int> GetLatestVersionAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default)
        => await _dbSet
            .Where(map => map.NightMarketId == nightMarketId)
            .MaxAsync(map => (int?)map.Version, cancellationToken) ?? 0;

    /// <summary>
    /// Compatibility bridge for legacy layout create/clone endpoints. All new
    /// legacy draft sections for a market reuse one draft composition until the
    /// explicit MarketMap workflow exists beside this bridge.
    /// The caller must hold the existing per-market advisory lock.
    /// </summary>
    public async Task<MarketMap> GetOrCreateLegacyDraftAsync(
        Guid nightMarketId, DateTime now, CancellationToken cancellationToken = default)
    {
        var existing = await _dbSet.FirstOrDefaultAsync(map =>
            map.NightMarketId == nightMarketId &&
            map.Status == MarketMapStatus.Draft &&
            map.Name == MarketMap.LegacyDraftName,
            cancellationToken);
        if (existing is not null) return existing;

        var map = new MarketMap
        {
            Id = Guid.NewGuid(),
            NightMarketId = nightMarketId,
            Name = MarketMap.LegacyDraftName,
            Version = checked(await GetLatestVersionAsync(nightMarketId, cancellationToken) + 1),
            Status = MarketMapStatus.Draft,
            PublishedAt = null,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _dbSet.AddAsync(map, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return map;
    }
}

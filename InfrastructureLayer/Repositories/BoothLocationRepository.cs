using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class BoothLocationRepository : GenericRepository<BoothLocation>, IBoothLocationRepository
{
    public BoothLocationRepository(SNMDbContext context) : base(context) { }

    public async Task<PagedResult<BoothLocation>> GetPagedByLayoutAsync(
        Guid layoutId, Guid? zoneId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(x => x.LayoutId == layoutId && !x.IsDeleted);
        if (zoneId.HasValue) query = query.Where(x => x.ZoneId == zoneId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Include(x => x.Booth).Include(x => x.LayoutNode)
            .OrderBy(x => x.SlotNumber).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<BoothLocation>(items, total);
    }

    public Task<BoothLocation?> GetCurrentByBoothAsync(Guid boothId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .Where(x => x.BoothId == boothId && !x.IsDeleted)
            .OrderByDescending(x => x.Layout.Status == DomainLayer.Enums.GeneralEnum.MarketLayoutStatus.Active)
            .ThenByDescending(x => x.Layout.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<BoothLocation?> GetCurrentByLayoutAndBoothAsync(Guid layoutId, Guid boothId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().FirstOrDefaultAsync(
            x => x.LayoutId == layoutId && x.BoothId == boothId && !x.IsDeleted, cancellationToken);

    public Task<BoothLocation?> GetCurrentByNodeAsync(Guid nodeId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().FirstOrDefaultAsync(x => x.LayoutNodeId == nodeId && !x.IsDeleted, cancellationToken);

    public async Task<IReadOnlyCollection<BoothLocation>> GetCurrentByLayoutAsync(Guid layoutId, CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking().Include(x => x.Booth)
            .Where(x => x.LayoutId == layoutId && !x.IsDeleted)
            .OrderBy(x => x.SlotNumber).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<BoothLocation>> GetCustomerCurrentByLayoutAsync(
        Guid layoutId,
        CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking()
            .Include(location => location.Booth)
            .Where(location =>
                location.LayoutId == layoutId &&
                !location.IsDeleted &&
                location.Booth.NightMarketId == location.Layout.NightMarketId &&
                location.Booth.Status == DomainLayer.Enums.GeneralEnum.BoothStatus.Active &&
                !location.Booth.NightMarket.IsDeleted &&
                location.Booth.NightMarket.ModerationStatus == DomainLayer.Enums.GeneralEnum.ModerationStatus.Active &&
                (location.Booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Upcoming ||
                 location.Booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Open ||
                 location.Booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Closed))
            .OrderBy(location => location.SlotNumber)
            .ToListAsync(cancellationToken);

    public async Task AssignOrMoveAsync(BoothLocation location, DateTime now, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var current = await _dbSet.FirstOrDefaultAsync(
            x => x.LayoutId == location.LayoutId && x.BoothId == location.BoothId && !x.IsDeleted, cancellationToken);
        if (current is not null)
        {
            current.IsDeleted = true;
            current.ReleasedAt = now;
            current.UpdatedAt = now;
        }
        await _dbSet.AddAsync(location, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReleaseAsync(Guid layoutId, Guid boothId, DateTime now, CancellationToken cancellationToken = default)
    {
        var current = await _dbSet.FirstOrDefaultAsync(
            x => x.LayoutId == layoutId && x.BoothId == boothId && !x.IsDeleted, cancellationToken);
        if (current is null) return;
        current.IsDeleted = true;
        current.ReleasedAt = now;
        current.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseDraftLocationsAsync(Guid boothId, DateTime now, CancellationToken cancellationToken = default)
    {
        var current = await _dbSet.Include(x => x.Layout)
            .Where(x => x.BoothId == boothId && !x.IsDeleted &&
                        x.Layout.Status == DomainLayer.Enums.GeneralEnum.MarketLayoutStatus.Draft)
            .ToListAsync(cancellationToken);
        foreach (var location in current)
        {
            location.IsDeleted = true;
            location.ReleasedAt = now;
            location.UpdatedAt = now;
            location.Layout.GraphRevision = checked(location.Layout.GraphRevision + 1);
            location.Layout.UpdatedAt = now;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }
}

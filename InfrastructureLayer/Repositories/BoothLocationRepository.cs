using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

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
        => _dbSet.AsNoTracking().FirstOrDefaultAsync(x => x.BoothId == boothId && !x.IsDeleted, cancellationToken);

    public Task<BoothLocation?> GetCurrentByLayoutAndBoothAsync(Guid layoutId, Guid boothId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().FirstOrDefaultAsync(
            x => x.LayoutId == layoutId && x.BoothId == boothId && !x.IsDeleted,
            cancellationToken);

    public Task<BoothLocation?> GetCurrentByNodeAsync(Guid nodeId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().FirstOrDefaultAsync(x => x.LayoutNodeId == nodeId && !x.IsDeleted, cancellationToken);

    public async Task<IReadOnlyCollection<BoothLocation>> GetCurrentByLayoutAsync(Guid layoutId, bool activeBoothsOnly = false, CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking().Include(x => x.Booth)
            .Where(x => x.LayoutId == layoutId && !x.IsDeleted
                && (!activeBoothsOnly || x.Booth.Status == BoothStatus.Active))
            .OrderBy(x => x.SlotNumber).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<BoothLocation>> GetCustomerCurrentByLayoutAsync(
        Guid layoutId,
        CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking()
            .Include(location => location.Booth)
                .ThenInclude(booth => booth.NightMarket)
            .Where(location =>
                location.LayoutId == layoutId &&
                !location.IsDeleted &&
                location.Booth.Status == BoothStatus.Active &&
                !location.Booth.NightMarket.IsDeleted &&
                location.Booth.NightMarket.Status == NightMarketStatus.Active &&
                location.Booth.NightMarket.ModerationStatus == ModerationStatus.Active)
            .OrderBy(location => location.SlotNumber)
            .ToListAsync(cancellationToken);

    public Task<int> CountActiveByNightMarketAsync(Guid nightMarketId, CancellationToken cancellationToken = default)
        => (from location in _dbSet
            join layout in _context.MarketLayouts on location.LayoutId equals layout.Id
            where !location.IsDeleted && layout.NightMarketId == nightMarketId
            select location).CountAsync(cancellationToken);

    public async Task AssignOrMoveAsync(BoothLocation location, DateTime now, CancellationToken cancellationToken = default)
    {
        var current = await _dbSet.FirstOrDefaultAsync(x => x.BoothId == location.BoothId && !x.IsDeleted, cancellationToken);
        if (current is not null)
        {
            current.IsDeleted = true;
            current.ReleasedAt = now;
            current.UpdatedAt = now;
        }
        await _dbSet.AddAsync(location, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(Guid boothId, DateTime now, CancellationToken cancellationToken = default)
    {
        var current = await _dbSet.FirstOrDefaultAsync(x => x.BoothId == boothId && !x.IsDeleted, cancellationToken);
        if (current is null) return;
        current.IsDeleted = true;
        current.ReleasedAt = now;
        current.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(Guid layoutId, Guid boothId, DateTime now, CancellationToken cancellationToken = default)
    {
        var current = await _dbSet.FirstOrDefaultAsync(
            x => x.LayoutId == layoutId && x.BoothId == boothId && !x.IsDeleted,
            cancellationToken);
        if (current is null) return;
        current.IsDeleted = true;
        current.ReleasedAt = now;
        current.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AcquireMarketAssignmentLockAsync(Guid nightMarketId, CancellationToken cancellationToken = default)
    {
        var marketIdString = nightMarketId.ToString();
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({marketIdString}))",
            cancellationToken);
    }
}

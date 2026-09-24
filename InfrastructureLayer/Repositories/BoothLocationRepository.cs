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
        => _dbSet.AsNoTracking().FirstOrDefaultAsync(x =>
            x.BoothId == boothId &&
            !x.IsDeleted &&
            x.ReleasedAt == null &&
            !x.Layout.IsDeleted &&
            x.Layout.Status == MarketLayoutStatus.Active,
            cancellationToken);

    public Task<BoothLocation?> GetCurrentByLayoutAndBoothAsync(Guid layoutId, Guid boothId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().FirstOrDefaultAsync(
            x => x.LayoutId == layoutId && x.BoothId == boothId && !x.IsDeleted && x.ReleasedAt == null,
            cancellationToken);

    public Task<BoothLocation?> GetCurrentByNodeAsync(Guid nodeId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().FirstOrDefaultAsync(
            x => x.LayoutNodeId == nodeId && !x.IsDeleted && x.ReleasedAt == null,
            cancellationToken);

    public async Task<IReadOnlyCollection<BoothLocation>> GetCurrentByLayoutAsync(Guid layoutId, bool activeBoothsOnly = false, CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking().Include(x => x.Booth)
            .Where(x => x.LayoutId == layoutId && !x.IsDeleted && x.ReleasedAt == null
                && (!activeBoothsOnly || x.Booth.Status == BoothStatus.Active))
            .OrderBy(x => x.SlotNumber).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<BoothLocation>> GetCustomerCurrentByLayoutAsync(
        Guid layoutId,
        CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking()
            .Include(location => location.Booth)
                .ThenInclude(booth => booth.NightMarket)
            .Include(location => location.LayoutNode)
            .Include(location => location.Zone)
            .Where(location =>
                location.LayoutId == layoutId &&
                !location.IsDeleted &&
                location.ReleasedAt == null &&
                location.Booth.Status == BoothStatus.Active &&
                !location.Booth.NightMarket.IsDeleted &&
                location.Booth.NightMarket.Status == NightMarketStatus.Active &&
                location.Booth.NightMarket.ModerationStatus == ModerationStatus.Active)
            .OrderBy(location => location.SlotNumber)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetDuplicateBoothIdsByMarketMapAsync(
        Guid marketMapId, CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking()
            .Where(location =>
                location.Layout.MarketMapId == marketMapId &&
                !location.Layout.IsDeleted &&
                !location.IsDeleted &&
                location.ReleasedAt == null)
            .GroupBy(location => location.BoothId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToListAsync(cancellationToken);

    public async Task AssignOrMoveAsync(BoothLocation location, DateTime now, CancellationToken cancellationToken = default)
    {
        var targetIsPublished = await _context.MarketLayouts.AnyAsync(layout =>
            layout.Id == location.LayoutId && !layout.IsDeleted && layout.Status == MarketLayoutStatus.Active,
            cancellationToken);

        var currentLocations = await _dbSet
            .Where(existing =>
                existing.BoothId == location.BoothId &&
                !existing.IsDeleted &&
                existing.ReleasedAt == null &&
                (targetIsPublished
                    ? !existing.Layout.IsDeleted && existing.Layout.Status == MarketLayoutStatus.Active
                    : existing.LayoutId == location.LayoutId))
            .ToListAsync(cancellationToken);

        foreach (var current in currentLocations)
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
        var current = await _dbSet.FirstOrDefaultAsync(x =>
            x.BoothId == boothId &&
            !x.IsDeleted &&
            x.ReleasedAt == null &&
            !x.Layout.IsDeleted &&
            x.Layout.Status == MarketLayoutStatus.Active,
            cancellationToken);
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

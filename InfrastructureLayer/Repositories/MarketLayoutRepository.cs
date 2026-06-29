using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class MarketLayoutRepository : GenericRepository<MarketLayout>, IMarketLayoutRepository
{
    public MarketLayoutRepository(SNMDbContext context) : base(context) { }

    public async Task<(IReadOnlyCollection<MarketLayout> Items, int TotalCount)> GetActivePagedAsync(
        Guid nightMarketId, string? keyword, MarketLayoutStatus? status, int page, int pageSize,
        string sortBy, bool ascending, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Where(layout => layout.NightMarketId == nightMarketId && !layout.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim().ToLower();
            query = query.Where(layout => layout.LayoutName.ToLower().Contains(normalized));
        }

        if (status.HasValue)
            query = query.Where(layout => layout.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);
        query = (sortBy.ToLowerInvariant(), ascending) switch
        {
            ("name", true) => query.OrderBy(layout => layout.LayoutName),
            ("name", false) => query.OrderByDescending(layout => layout.LayoutName),
            ("version", true) => query.OrderBy(layout => layout.Version),
            ("version", false) => query.OrderByDescending(layout => layout.Version),
            ("status", true) => query.OrderBy(layout => layout.Status),
            ("status", false) => query.OrderByDescending(layout => layout.Status),
            ("updatedat", true) => query.OrderBy(layout => layout.UpdatedAt),
            ("updatedat", false) => query.OrderByDescending(layout => layout.UpdatedAt),
            ("createdat", true) => query.OrderBy(layout => layout.CreatedAt),
            _ => query.OrderByDescending(layout => layout.CreatedAt)
        };

        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<MarketLayout?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(
            layout => layout.Id == id && !layout.IsDeleted && !layout.NightMarket.IsDeleted,
            cancellationToken);

    public Task<MarketLayout?> GetEditorLayoutAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .Include(layout => layout.LayoutNodes.Where(node => !node.IsDeleted))
            .Include(layout => layout.BoothLocations.Where(location => !location.IsDeleted))
            .FirstOrDefaultAsync(
                layout => layout.Id == id && !layout.IsDeleted && !layout.NightMarket.IsDeleted,
                cancellationToken);

    public Task<MarketLayout?> GetActiveMapAsync(Guid nightMarketId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .FirstOrDefaultAsync(x => x.NightMarketId == nightMarketId && !x.IsDeleted &&
                x.Status == MarketLayoutStatus.Active && !x.NightMarket.IsDeleted, cancellationToken);

    public async Task<IReadOnlyCollection<LayoutEdge>> GetEdgesByLayoutIdAsync(
        Guid layoutId, CancellationToken cancellationToken = default)
        => await _context.LayoutEdges.AsNoTracking()
            .Where(edge => edge.LayoutId == layoutId && !edge.IsDeleted)
            .OrderBy(edge => edge.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> ActiveNameOrVersionExistsAsync(
        Guid nightMarketId, string name, int version, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLower();
        return _dbSet.AnyAsync(layout =>
            layout.NightMarketId == nightMarketId &&
            !layout.IsDeleted &&
            (layout.LayoutName.ToLower() == normalized || layout.Version == version) &&
            (!excludeId.HasValue || layout.Id != excludeId.Value), cancellationToken);
    }

    public async Task ActivateExclusiveAsync(
        Guid nightMarketId, Guid activeLayoutId, DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended(CAST({nightMarketId} AS text), 0))",
            cancellationToken);

        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "MarketLayouts"
            SET "Status" = 'Inactive',
                "UpdatedAt" = {updatedAt}
            WHERE "NightMarketId" = {nightMarketId}
              AND "IsDeleted" = false
              AND "Id" <> {activeLayoutId}
              AND "Status" = 'Active'
            """, cancellationToken);

        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "MarketLayouts"
            SET "Status" = 'Active',
                "UpdatedAt" = {updatedAt}
            WHERE "Id" = {activeLayoutId}
              AND "NightMarketId" = {nightMarketId}
              AND "IsDeleted" = false
            """, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}

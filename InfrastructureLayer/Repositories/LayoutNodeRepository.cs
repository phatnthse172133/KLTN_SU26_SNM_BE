using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class LayoutNodeRepository : GenericRepository<LayoutNode>, ILayoutNodeRepository
{
    public LayoutNodeRepository(SNMDbContext context) : base(context) { }

    public async Task<PagedResult<LayoutNode>> GetPagedAsync(
        Guid layoutId, string? keyword, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(x => x.LayoutId == layoutId && !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.Trim().ToLower();
            query = query.Where(x => x.NodeName != null && x.NodeName.ToLower().Contains(value));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<LayoutNode>(items, total);
    }

    public async Task<IReadOnlyCollection<LayoutNode>> GetByLayoutAsync(
        Guid layoutId, bool accessibleOnly = false, CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking()
            .Where(x => x.LayoutId == layoutId && !x.IsDeleted && (!accessibleOnly || x.IsAccessible))
            .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);

    public async Task<PagedResult<LayoutNode>> GetAvailableBoothAccessPagedAsync(
        Guid layoutId, Guid? zoneId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(node =>
            node.LayoutId == layoutId && !node.IsDeleted && node.IsAccessible &&
            node.NodeType == LayoutNodeType.BoothSlot &&
            (!zoneId.HasValue || node.ZoneId == zoneId) &&
            !_context.BoothLocations.Any(location => location.LayoutNodeId == node.Id && !location.IsDeleted));
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.NodeName).ThenBy(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<LayoutNode>(items, total);
    }

    public async Task<PagedResult<LayoutNode>> GetStartingPointsPagedAsync(
        Guid layoutId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(node =>
            node.LayoutId == layoutId &&
            !node.IsDeleted &&
            node.IsAccessible &&
            (node.IsStartingPoint ||
             node.NodeType == LayoutNodeType.Entrance ||
             node.NodeType == LayoutNodeType.Exit ||
             node.NodeType == LayoutNodeType.Landmark));
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(node => node.NodeName)
            .ThenBy(node => node.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LayoutNode>(items, totalCount);
    }

    public Task<LayoutNode?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && !x.Layout.IsDeleted, cancellationToken);
}

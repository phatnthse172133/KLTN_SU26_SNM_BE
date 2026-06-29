using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class LayoutNodeRepository : GenericRepository<LayoutNode>, ILayoutNodeRepository
{
    public LayoutNodeRepository(SNMDbContext context) : base(context) { }

    public async Task<(IReadOnlyCollection<LayoutNode> Items, int TotalCount)> GetPagedAsync(
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
        return (items, total);
    }

    public async Task<IReadOnlyCollection<LayoutNode>> GetByLayoutAsync(
        Guid layoutId, bool accessibleOnly = false, CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking()
            .Where(x => x.LayoutId == layoutId && !x.IsDeleted && (!accessibleOnly || x.IsAccessible))
            .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyCollection<LayoutNode> Items, int TotalCount)> GetAvailableBoothAccessPagedAsync(
        Guid layoutId, Guid? zoneId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(node =>
            node.LayoutId == layoutId && !node.IsDeleted && node.IsAccessible &&
            node.NodeType == LayoutNodeType.BoothAccess &&
            (!zoneId.HasValue || node.ZoneId == zoneId) &&
            !_context.BoothLocations.Any(location => location.LayoutNodeId == node.Id && !location.IsDeleted));
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.NodeName).ThenBy(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<LayoutNode?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && !x.Layout.IsDeleted, cancellationToken);
}

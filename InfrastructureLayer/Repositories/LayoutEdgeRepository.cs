using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class LayoutEdgeRepository : GenericRepository<LayoutEdge>, ILayoutEdgeRepository
{
    public LayoutEdgeRepository(SNMDbContext context) : base(context) { }

    public async Task<PagedResult<LayoutEdge>> GetPagedAsync(
        Guid layoutId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = ActiveGraphEdges(layoutId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<LayoutEdge>(items, total);
    }

    public async Task<IReadOnlyCollection<LayoutEdge>> GetByLayoutAsync(
        Guid layoutId, bool accessibleOnly = false, CancellationToken cancellationToken = default)
        => await ActiveGraphEdges(layoutId)
            .Where(x => !accessibleOnly || x.IsAccessible)
            .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);

    public Task<LayoutEdge?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && !x.Layout.IsDeleted, cancellationToken);

    public Task<bool> ExistsAsync(Guid layoutId, Guid fromNodeId, Guid toNodeId, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => ActiveGraphEdges(layoutId).AnyAsync(x =>
            (!excludeId.HasValue || x.Id != excludeId.Value) &&
            ((x.FromNodeId == fromNodeId && x.ToNodeId == toNodeId) ||
             (x.IsBidirectional && x.FromNodeId == toNodeId && x.ToNodeId == fromNodeId)), cancellationToken);

    private IQueryable<LayoutEdge> ActiveGraphEdges(Guid layoutId)
        => _dbSet.AsNoTracking().Where(edge =>
            edge.LayoutId == layoutId &&
            !edge.IsDeleted &&
            !edge.FromNode.IsDeleted &&
            !edge.ToNode.IsDeleted &&
            edge.FromNode.LayoutId == layoutId &&
            edge.ToNode.LayoutId == layoutId);
}

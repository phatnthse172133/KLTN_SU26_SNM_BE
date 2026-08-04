using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class LayoutNavigationAnchorRepository : GenericRepository<LayoutNavigationAnchor>, ILayoutNavigationAnchorRepository
{
    public LayoutNavigationAnchorRepository(SNMDbContext context) : base(context) { }

    public async Task<IReadOnlyCollection<LayoutNavigationAnchor>> GetByLayoutAsync(Guid layoutId, CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking().Include(x => x.LayoutNode)
            .Where(x => x.LayoutId == layoutId && !x.IsDeleted)
            .OrderBy(x => x.DisplayName).ToListAsync(cancellationToken);

    public Task<LayoutNavigationAnchor?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbSet.Include(x => x.Layout).Include(x => x.LayoutNode)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid layoutId, string anchorCode, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(x => x.LayoutId == layoutId && !x.IsDeleted && x.AnchorCode == anchorCode &&
                               (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken);

}

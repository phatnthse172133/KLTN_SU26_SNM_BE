using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface ILayoutEdgeRepository : IGenericRepository<LayoutEdge>
{
    Task<PagedResult<LayoutEdge>> GetPagedAsync(
        Guid layoutId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LayoutEdge>> GetByLayoutAsync(Guid layoutId, bool accessibleOnly = false, CancellationToken cancellationToken = default);
    Task<LayoutEdge?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid layoutId, Guid fromNodeId, Guid toNodeId, Guid? excludeId = null, CancellationToken cancellationToken = default);
}

using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface ILayoutNodeRepository : IGenericRepository<LayoutNode>
{
    Task<PagedResult<LayoutNode>> GetPagedAsync(
        Guid layoutId, string? keyword, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LayoutNode>> GetByLayoutAsync(Guid layoutId, bool accessibleOnly = false, CancellationToken cancellationToken = default);
    Task<PagedResult<LayoutNode>> GetAvailableBoothAccessPagedAsync(
        Guid layoutId, Guid? zoneId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<LayoutNode>> GetStartingPointsPagedAsync(
        Guid layoutId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<LayoutNode?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

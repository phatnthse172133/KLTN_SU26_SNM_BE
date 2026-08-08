using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface ILayoutNavigationAnchorRepository : IGenericRepository<LayoutNavigationAnchor>
{
    Task<IReadOnlyCollection<LayoutNavigationAnchor>> GetByLayoutAsync(Guid layoutId, CancellationToken cancellationToken = default);
    Task<LayoutNavigationAnchor?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(Guid layoutId, string anchorCode, Guid? excludeId = null, CancellationToken cancellationToken = default);
}

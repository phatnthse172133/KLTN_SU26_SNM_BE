using DomainLayer.Common;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface IMarketLayoutRepository : IGenericRepository<MarketLayout>
{
    Task<PagedResult<MarketLayout>> GetActivePagedAsync(
        Guid nightMarketId,
        string? keyword,
        MarketLayoutStatus? status,
        int page,
        int pageSize,
        string sortBy,
        bool ascending,
        CancellationToken cancellationToken = default);

    Task<MarketLayout?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MarketLayout?> GetEditorLayoutAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MarketLayout?> GetActiveMapAsync(Guid nightMarketId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LayoutNode>> GetNodesByLayoutIdAsync(Guid layoutId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LayoutEdge>> GetEdgesByLayoutIdAsync(Guid layoutId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LayoutBlock>> GetBlocksByLayoutIdAsync(Guid layoutId, CancellationToken cancellationToken = default);
    Task AcquireMarketLockAsync(Guid nightMarketId, CancellationToken cancellationToken = default);
    Task<int> GetNextVersionAsync(Guid nightMarketId, CancellationToken cancellationToken = default);
    Task<bool> ActiveNameOrVersionExistsAsync(Guid nightMarketId, string name, int version, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task ActivateExclusiveAsync(Guid nightMarketId, Guid activeLayoutId, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task<MarketLayout> CloneToDraftAsync(Guid sourceLayoutId, string? layoutName, DateTime createdAt, CancellationToken cancellationToken = default);
    Task SaveGraphTransactionalAsync(Guid layoutId, IEnumerable<LayoutBlock> blocks, IEnumerable<LayoutNode> nodes, IEnumerable<LayoutEdge> edges, IEnumerable<Zone>? zoneUpserts = null, CancellationToken cancellationToken = default);
}

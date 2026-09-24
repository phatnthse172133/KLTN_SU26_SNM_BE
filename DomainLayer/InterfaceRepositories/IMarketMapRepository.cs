using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace DomainLayer.InterfaceRepositories;

public interface IMarketMapRepository : IGenericRepository<MarketMap>
{
    Task<IReadOnlyCollection<MarketMap>> GetByMarketIdAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default);

    Task<MarketMap?> GetDetailAsync(
        Guid nightMarketId, Guid marketMapId, CancellationToken cancellationToken = default);

    Task<MarketMap?> GetManagementDetailAsync(
        Guid marketMapId, CancellationToken cancellationToken = default);

    Task<MarketMap?> GetManagementDetailForUpdateAsync(
        Guid marketMapId, CancellationToken cancellationToken = default);

    Task<MarketMap?> GetActiveByMarketIdAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default);

    Task<MarketMap?> GetCustomerActiveDetailAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default);

    Task<MarketMap?> GetActiveForUpdateAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default);

    Task<int> GetLatestVersionAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default);

    Task<MarketMap> GetOrCreateLegacyDraftAsync(
        Guid nightMarketId, DateTime now, CancellationToken cancellationToken = default);
}

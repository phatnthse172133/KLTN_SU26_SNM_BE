using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IBoothLocationRepository : IGenericRepository<BoothLocation>
{
    Task<PagedResult<BoothLocation>> GetPagedByLayoutAsync(
        Guid layoutId, Guid? zoneId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<BoothLocation?> GetCurrentByBoothAsync(Guid boothId, CancellationToken cancellationToken = default);
    Task<BoothLocation?> GetCurrentByNodeAsync(Guid nodeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<BoothLocation>> GetCurrentByLayoutAsync(Guid layoutId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<BoothLocation>> GetCustomerCurrentByLayoutAsync(Guid layoutId, CancellationToken cancellationToken = default);
    Task AssignOrMoveAsync(BoothLocation location, DateTime now, CancellationToken cancellationToken = default);
    Task ReleaseAsync(Guid boothId, DateTime now, CancellationToken cancellationToken = default);
}

using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IBoothRegistrationRepository : IGenericRepository<BoothRegistration>
{
    Task<PagedResult<BoothRegistration>> GetByOwnerPagedAsync(
        Guid ownerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<BoothRegistration>> GetPendingPagedAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);
    Task<bool> HasPendingAsync(Guid ownerId, CancellationToken cancellationToken = default);
}

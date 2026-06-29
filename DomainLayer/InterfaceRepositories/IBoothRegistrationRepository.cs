using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IBoothRegistrationRepository : IGenericRepository<BoothRegistration>
{
    Task<IReadOnlyCollection<BoothRegistration>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyCollection<BoothRegistration> Items, int TotalCount)> GetPendingPagedAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);
    Task<bool> HasPendingAsync(Guid ownerId, CancellationToken cancellationToken = default);
}

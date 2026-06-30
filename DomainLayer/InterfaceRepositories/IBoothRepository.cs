using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IBoothRepository : IGenericRepository<Booth>
{
    Task<Booth?> GetOwnedBoothAsync(Guid ownerId, Guid boothId);
    Task<PagedResult<Booth>> GetOwnedPagedAsync(
        Guid ownerId, int page, int pageSize, CancellationToken cancellationToken = default);
}

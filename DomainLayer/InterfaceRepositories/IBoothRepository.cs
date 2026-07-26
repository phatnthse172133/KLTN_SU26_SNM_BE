using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IBoothRepository : IGenericRepository<Booth>
{
    Task<PagedResult<NightMarketBoothCustomerReadModel>> GetCustomerByNightMarketPagedAsync(
        Guid nightMarketId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Booth?> GetByOwnerIdAsync(
        Guid ownerId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByOwnerIdAsync(
        Guid ownerId, CancellationToken cancellationToken = default);
    Task<Booth?> GetOwnedBoothAsync(Guid ownerId, Guid boothId);
}

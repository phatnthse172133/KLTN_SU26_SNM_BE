using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IBoothRepository : IGenericRepository<Booth>
{
    Task<Booth?> GetByOwnerIdAsync(
        Guid ownerId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByOwnerIdAsync(
        Guid ownerId, CancellationToken cancellationToken = default);
    Task<Booth?> GetOwnedBoothAsync(Guid ownerId, Guid boothId);
}

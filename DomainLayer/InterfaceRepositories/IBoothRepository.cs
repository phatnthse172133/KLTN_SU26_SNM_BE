using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IBoothRepository : IGenericRepository<Booth>
{
    Task<Booth?> GetOwnedBoothAsync(Guid ownerId, Guid boothId);
}

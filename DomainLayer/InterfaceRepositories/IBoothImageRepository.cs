using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IBoothImageRepository : IGenericRepository<BoothImage>
{
    Task AcquireGalleryLockAsync(Guid boothId, CancellationToken cancellationToken = default);
}

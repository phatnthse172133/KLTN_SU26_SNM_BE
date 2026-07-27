using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface INightMarketImageRepository : IGenericRepository<NightMarketImage>
{
    Task AcquireGalleryLockAsync(Guid nightMarketId, CancellationToken cancellationToken = default);
}

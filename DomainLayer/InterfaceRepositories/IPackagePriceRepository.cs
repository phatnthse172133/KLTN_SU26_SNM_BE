using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IPackagePriceRepository : IGenericRepository<PackagePrice>
{
    Task<PagedResult<PackagePrice>> GetByPackagePagedAsync(
        Guid packageId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

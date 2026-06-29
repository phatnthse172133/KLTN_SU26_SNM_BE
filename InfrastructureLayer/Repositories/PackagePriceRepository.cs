using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class PackagePriceRepository : GenericRepository<PackagePrice>, IPackagePriceRepository
{
    public PackagePriceRepository(SNMDbContext context) : base(context) { }

    public async Task<PagedResult<PackagePrice>> GetByPackagePagedAsync(
        Guid packageId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = ActiveQuery()
            .AsNoTracking()
            .Where(price => price.PackageId == packageId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(price => price.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PackagePrice>(items, totalCount);
    }
}

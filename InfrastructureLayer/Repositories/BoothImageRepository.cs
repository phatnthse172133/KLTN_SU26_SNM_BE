using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class BoothImageRepository : GenericRepository<BoothImage>, IBoothImageRepository
{
    public BoothImageRepository(SNMDbContext context) : base(context) { }

    public async Task AcquireGalleryLockAsync(Guid boothId, CancellationToken cancellationToken = default)
    {
        var lockKey = $"booth-gallery:{boothId}";
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);
    }
}

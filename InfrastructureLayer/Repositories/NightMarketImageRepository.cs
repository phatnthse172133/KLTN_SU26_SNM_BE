using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class NightMarketImageRepository : GenericRepository<NightMarketImage>, INightMarketImageRepository
{
    public NightMarketImageRepository(SNMDbContext context) : base(context) { }

    public async Task AcquireGalleryLockAsync(Guid nightMarketId, CancellationToken cancellationToken = default)
    {
        var lockKey = $"nm-gallery:{nightMarketId}";
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);
    }
}

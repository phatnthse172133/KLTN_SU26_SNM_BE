using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class BoothRepository : GenericRepository<Booth>, IBoothRepository
{
    public BoothRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<Booth?> GetOwnedBoothAsync(Guid ownerId, Guid boothId)
        => await _dbSet.FirstOrDefaultAsync(booth => booth.Id == boothId && booth.BoothOwnerId == ownerId);

    public async Task<PagedResult<Booth>> GetOwnedPagedAsync(
        Guid ownerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(booth => booth.BoothOwnerId == ownerId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(booth => booth.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Booth>(items, totalCount);
    }
}

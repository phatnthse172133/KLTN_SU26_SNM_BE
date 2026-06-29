using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class BoothRegistrationRepository : GenericRepository<BoothRegistration>, IBoothRegistrationRepository
{
    public BoothRegistrationRepository(SNMDbContext context) : base(context) { }

    public async Task<PagedResult<BoothRegistration>> GetByOwnerPagedAsync(
        Guid ownerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(registration => registration.OwnerId == ownerId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(registration => registration.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<BoothRegistration>(items, totalCount);
    }

    public async Task<PagedResult<BoothRegistration>> GetPendingPagedAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(x => x.Status == BoothRegistrationStatus.PendingReview);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<BoothRegistration>(items, total);
    }

    public Task<bool> HasPendingAsync(Guid ownerId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(x => x.OwnerId == ownerId && x.Status == BoothRegistrationStatus.PendingReview, cancellationToken);
}

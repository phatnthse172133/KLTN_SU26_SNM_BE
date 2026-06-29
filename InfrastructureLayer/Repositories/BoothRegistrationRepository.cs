using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class BoothRegistrationRepository : GenericRepository<BoothRegistration>, IBoothRegistrationRepository
{
    public BoothRegistrationRepository(SNMDbContext context) : base(context) { }

    public async Task<IReadOnlyCollection<BoothRegistration>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking().Where(x => x.OwnerId == ownerId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyCollection<BoothRegistration> Items, int TotalCount)> GetPendingPagedAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(x => x.Status == BoothRegistrationStatus.PendingReview);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<bool> HasPendingAsync(Guid ownerId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(x => x.OwnerId == ownerId && x.Status == BoothRegistrationStatus.PendingReview, cancellationToken);
}

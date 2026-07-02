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

    public Task<Booth?> GetByOwnerIdAsync(
        Guid ownerId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().FirstOrDefaultAsync(
            booth => booth.BoothOwnerId == ownerId, cancellationToken);

    public Task<bool> ExistsByOwnerIdAsync(
        Guid ownerId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(
            booth => booth.BoothOwnerId == ownerId, cancellationToken);

    public async Task<Booth?> GetOwnedBoothAsync(Guid ownerId, Guid boothId)
        => await _dbSet.FirstOrDefaultAsync(booth => booth.Id == boothId && booth.BoothOwnerId == ownerId);
}

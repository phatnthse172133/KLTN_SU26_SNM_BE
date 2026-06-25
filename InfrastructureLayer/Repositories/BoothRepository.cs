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
}

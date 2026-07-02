using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyCollection<Guid>> GetActiveRecipientIdsAsync(
        Guid? userId,
        string? role,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Where(user => user.Status == UserStatus.Active);

        if (userId.HasValue)
            query = query.Where(user => user.Id == userId.Value);

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(user => user.Role.RoleName == role);

        return await query.Select(user => user.Id).ToListAsync(cancellationToken);
    }
}

using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class UserDeviceTokenRepository : GenericRepository<UserDeviceToken>, IUserDeviceTokenRepository
{
    public UserDeviceTokenRepository(SNMDbContext context) : base(context)
    {
    }

    public Task<UserDeviceToken?> GetByTokenIncludingDeletedAsync(
        string token,
        CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(item => item.Token == token, cancellationToken);

    public async Task<IReadOnlyCollection<UserDeviceToken>> GetActiveByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => await ActiveQuery()
            .Where(item => item.UserId == userId && item.IsActive)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<UserDeviceToken>> GetActiveByUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
        => await ActiveQuery()
            .Where(item => userIds.Contains(item.UserId) && item.IsActive)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<UserDeviceToken>> GetByUserAndSelectionAsync(
        Guid userId,
        string? token,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var query = ActiveQuery().Where(item => item.UserId == userId);
        if (!string.IsNullOrWhiteSpace(token))
            query = query.Where(item => item.Token == token);
        else if (!string.IsNullOrWhiteSpace(deviceId))
            query = query.Where(item => item.DeviceId == deviceId);
        else
            return [];

        return await query.ToListAsync(cancellationToken);
    }
}

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

    public async Task<bool> UserExistsAsync(Guid userId)
        => await _dbSet.AnyAsync(u => u.Id == userId);

    public async Task BeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction == null)
        {
            await _context.Database.BeginTransactionAsync();
        }
    }

    public async Task CommitTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            await _context.Database.CurrentTransaction.CommitAsync();
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            await _context.Database.CurrentTransaction.RollbackAsync();
        }
    }

    public async Task<int> UpdateStatusWithConcurrencyAsync(Guid userId, UserStatus expectedPreviousStatus, UserStatus newStatus, DateTime updatedAt)
    {
        var query = _dbSet.Where(u => u.Id == userId && u.Status == expectedPreviousStatus);

        if (newStatus == UserStatus.Inactive)
        {
            return await query.ExecuteUpdateAsync(update => update
                .SetProperty(u => u.Status, newStatus)
                .SetProperty(u => u.UpdatedAt, updatedAt)
                .SetProperty(u => u.RefreshTokenHash, (string?)null)
                .SetProperty(u => u.RefreshTokenExpiresAt, (DateTime?)null));
        }

        return await query.ExecuteUpdateAsync(update => update
            .SetProperty(u => u.Status, newStatus)
            .SetProperty(u => u.UpdatedAt, updatedAt));
    }

    public async Task ReloadAsync(User entity)
    {
        await _context.Entry(entity).ReloadAsync();
    }
}

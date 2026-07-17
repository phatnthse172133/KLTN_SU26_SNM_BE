using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface IUserRepository : IGenericRepository<User>
{
    Task<IReadOnlyCollection<Guid>> GetActiveRecipientIdsAsync(
        Guid? userId,
        string? role,
        CancellationToken cancellationToken = default);

    Task<bool> UserExistsAsync(Guid userId);

    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
    Task<int> UpdateStatusWithConcurrencyAsync(Guid userId, UserStatus expectedPreviousStatus, UserStatus newStatus, DateTime updatedAt);
    Task ReloadAsync(User entity);
}

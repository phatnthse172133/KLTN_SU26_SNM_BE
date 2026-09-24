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

    Task<bool> TryLinkGoogleIdentityAsync(
        Guid userId,
        string googleId,
        DateTime updatedAt,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddGoogleUserAsync(User user, CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, string>> GetUserNamesByIdsAsync(
        List<Guid> userIds,
        CancellationToken cancellationToken = default);
}

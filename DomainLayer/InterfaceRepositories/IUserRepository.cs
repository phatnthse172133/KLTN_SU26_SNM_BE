using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IUserRepository : IGenericRepository<User>
{
    Task<IReadOnlyCollection<Guid>> GetActiveRecipientIdsAsync(
        Guid? userId,
        string? role,
        CancellationToken cancellationToken = default);

    Task<bool> UserExistsAsync(Guid userId);
}

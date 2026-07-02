using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IUserDeviceTokenRepository : IGenericRepository<UserDeviceToken>
{
    Task<UserDeviceToken?> GetByTokenIncludingDeletedAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserDeviceToken>> GetActiveByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserDeviceToken>> GetActiveByUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserDeviceToken>> GetByUserAndSelectionAsync(
        Guid userId,
        string? token,
        string? deviceId,
        CancellationToken cancellationToken = default);
}

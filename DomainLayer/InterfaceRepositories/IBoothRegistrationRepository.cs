using DomainLayer.Common;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface IBoothRegistrationRepository : IGenericRepository<BoothRegistration>
{
    Task<PagedResult<BoothRegistration>> GetByOwnerPagedAsync(
        Guid ownerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<BoothRegistration>> GetPendingPagedAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);
    Task<bool> HasPendingAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<PagedResult<BoothRegistration>> GetByMarketOwnerPagedAsync(
        Guid marketOwnerId, Guid? marketId, BoothRegistrationStatus? status,
        string? keyword,
        int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Dictionary<BoothRegistrationStatus, int>> GetCountsByMarketOwnerAsync(
        Guid marketOwnerId, Guid? marketId, CancellationToken cancellationToken = default);
    Task<int> UpdateStatusWithConcurrencyAsync(
        Guid registrationId, BoothRegistrationStatus expectedStatus, BoothRegistrationStatus newStatus,
        string? rejectReason, DateTime updatedAt, CancellationToken cancellationToken = default);

}

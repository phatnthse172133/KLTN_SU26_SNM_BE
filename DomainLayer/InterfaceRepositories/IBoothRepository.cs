using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IBoothRepository : IGenericRepository<Booth>
{
    Task<PagedResult<CustomerBoothReadModel>> GetCustomerPagedAsync(
        Guid? marketId,
        string? search,
        bool? openNow,
        TimeOnly localTime,
        decimal? minimumRating,
        decimal? maximumRating,
        int page,
        int pageSize,
        string sort,
        CancellationToken cancellationToken = default);

    Task<CustomerBoothReadModel?> GetCustomerByIdAsync(
        Guid boothId,
        CancellationToken cancellationToken = default);

    Task<bool> CustomerVisibleExistsAsync(
        Guid boothId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<NightMarketBoothCustomerReadModel>> GetCustomerByNightMarketPagedAsync(
        Guid nightMarketId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Booth?> GetByOwnerIdAsync(
        Guid ownerId, CancellationToken cancellationToken = default);
    Task<Booth?> GetByOwnerIdWithAdminDetailsAsync(
        Guid ownerId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByOwnerIdAsync(
        Guid ownerId, CancellationToken cancellationToken = default);
    Task<Booth?> GetOwnedBoothAsync(Guid ownerId, Guid boothId);
}

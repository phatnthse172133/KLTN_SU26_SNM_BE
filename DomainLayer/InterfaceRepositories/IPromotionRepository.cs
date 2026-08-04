using DomainLayer.Common;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface IPromotionRepository : IGenericRepository<Promotion>
{
    Task<PagedResult<Promotion>> GetPagedAsync(
        Guid? boothId,
        string? keyword,
        PromotionStatus? status,
        PromotionScope? scope,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Promotion?> GetDetailsAsync(Guid promotionId, CancellationToken cancellationToken = default);

    Task<Promotion?> GetByCodeAsync(
        Guid boothId,
        string promotionCode,
        CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(
        Guid boothId,
        string promotionCode,
        Guid? excludePromotionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes promotion reservations for one promotion for the lifetime of
    /// the caller's current PostgreSQL transaction.
    /// </summary>
    Task AcquireReservationLockAsync(
        Guid promotionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads the promotion and its scope targets after the reservation lock
    /// has been acquired, avoiding validation against a stale tracked entity.
    /// </summary>
    Task<Promotion?> GetReservationDetailsAsync(
        Guid promotionId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Promotion>> GetAvailablePagedAsync(
        IReadOnlyCollection<Guid> boothIds,
        DateTime now,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Promotion>> GetAvailableAsync(
        IReadOnlyCollection<Guid> boothIds,
        DateTime now,
        CancellationToken cancellationToken = default);

    Task ReplaceScopeTargetsAsync(
        Promotion promotion,
        IReadOnlyCollection<Guid> foodItemIds,
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken = default);
}

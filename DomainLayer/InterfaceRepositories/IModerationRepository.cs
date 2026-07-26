using DomainLayer.Common;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface IModerationRepository : IGenericRepository<ModerationActionHistory>
{
    // Night Market queries
    Task<PagedResult<NightMarket>> GetMarketsPagedAsync(
        string? keyword,
        NightMarketStatus? lifecycleStatus,
        ModerationStatus? moderationStatus,
        Guid? marketOwnerId,
        int page,
        int pageSize,
        string sortBy,
        bool ascending,
        CancellationToken cancellationToken = default);

    Task<NightMarket?> GetMarketDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> UpdateMarketModerationStatusAsync(
        Guid marketId,
        ModerationStatus expectedPreviousStatus,
        ModerationStatus newStatus,
        DateTime updatedAt,
        CancellationToken cancellationToken = default);

    // Booth queries
    Task<PagedResult<Booth>> GetBoothsPagedAsync(
        string? keyword,
        BoothStatus? status,
        Guid? nightMarketId,
        Guid? boothOwnerId,
        int page,
        int pageSize,
        string sortBy,
        bool ascending,
        CancellationToken cancellationToken = default);

    Task<Booth?> GetBoothDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> UpdateBoothStatusAsync(
        Guid boothId,
        BoothStatus expectedPreviousStatus,
        BoothStatus newStatus,
        DateTime updatedAt,
        CancellationToken cancellationToken = default);

    // History
    Task<PagedResult<ModerationActionHistory>> GetHistoryByBoothAsync(
        Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PagedResult<ModerationActionHistory>> GetHistoryByMarketAsync(
        Guid marketId, int page, int pageSize, CancellationToken cancellationToken = default);

    // Complaint counts
    Task<int> CountSeriousComplaintsByMarketAsync(Guid marketId, CancellationToken cancellationToken = default);
    Task<int> CountComplaintsByMarketAsync(Guid marketId, CancellationToken cancellationToken = default);
    Task<int> CountComplaintsByBoothAsync(Guid boothId, CancellationToken cancellationToken = default);
    Task<int> CountBoothsByMarketAsync(Guid marketId, CancellationToken cancellationToken = default);
    Task<int> CountActiveBoothsByMarketAsync(Guid marketId, CancellationToken cancellationToken = default);

    Task<List<Complaint>> GetRecentComplaintsByBoothAsync(Guid boothId, int count, CancellationToken cancellationToken = default);
    Task<List<Complaint>> GetRecentComplaintsByMarketAsync(Guid marketId, int count, CancellationToken cancellationToken = default);

    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();

    Task<Dictionary<Guid, int>> CountComplaintsByBoothIdsAsync(IEnumerable<Guid> boothIds, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, int>> CountBoothsByMarketIdsAsync(IEnumerable<Guid> marketIds, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, int>> CountActiveBoothsByMarketIdsAsync(IEnumerable<Guid> marketIds, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, int>> CountSeriousComplaintsByMarketIdsAsync(IEnumerable<Guid> marketIds, CancellationToken cancellationToken = default);
}

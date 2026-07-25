using DomainLayer.Common;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface INightMarketRepository : IGenericRepository<NightMarket>
{
    Task<PagedResult<NightMarket>> GetActivePagedAsync(
        string? keyword,
        NightMarketStatus? status,
        int page,
        int pageSize,
        string sortBy,
        bool ascending,
        CancellationToken cancellationToken = default);

    Task<NightMarket?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ActiveNameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<List<NightMarket>> GetByOwnerIdAsync(Guid marketOwnerId, CancellationToken cancellationToken = default);
}

using DomainLayer.Common;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface IZoneRepository : IGenericRepository<Zone>
{
    Task<PagedResult<Zone>> GetActivePagedAsync(
        Guid nightMarketId,
        string? keyword,
        ZoneStatus? status,
        int page,
        int pageSize,
        string sortBy,
        bool ascending,
        CancellationToken cancellationToken = default);

    Task<Zone?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Zone>> GetActiveByNightMarketIdAsync(Guid nightMarketId, bool activeStatusOnly = false, CancellationToken cancellationToken = default);
    Task<bool> ActiveNameExistsAsync(Guid nightMarketId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> ActiveZoneCodeExistsAsync(Guid nightMarketId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, int>> GetAssignedSlotCountsByMarketAsync(Guid nightMarketId, CancellationToken cancellationToken = default);
}

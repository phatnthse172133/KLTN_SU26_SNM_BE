using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IFoodCategoryRepository : IGenericRepository<FoodCategory>
{
    Task<PagedResult<FoodCategory>> GetActivePagedByBoothAsync(Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<FoodCategory?> GetActiveByBoothAsync(Guid boothId, Guid categoryId);
    Task<bool> ActiveNameExistsAsync(Guid boothId, string name, Guid? excludeId = null);
    Task<IReadOnlyCollection<FoodCategory>> GetByIdsAsync(IReadOnlyCollection<Guid> ids);
    Task<IReadOnlyCollection<FoodCategory>> GetActiveByIdsAndBoothAsync(
        Guid boothId,
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken = default);
}

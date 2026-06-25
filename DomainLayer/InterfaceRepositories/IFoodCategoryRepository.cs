using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IFoodCategoryRepository : IGenericRepository<FoodCategory>
{
    Task<(IEnumerable<FoodCategory> Items, int TotalCount)> GetActivePagedByBoothAsync(Guid boothId, int page, int pageSize);
    Task<FoodCategory?> GetActiveByBoothAsync(Guid boothId, Guid categoryId);
    Task<bool> ActiveNameExistsAsync(Guid boothId, string name, Guid? excludeId = null);
    Task<IReadOnlyCollection<FoodCategory>> GetByIdsAsync(IReadOnlyCollection<Guid> ids);
}

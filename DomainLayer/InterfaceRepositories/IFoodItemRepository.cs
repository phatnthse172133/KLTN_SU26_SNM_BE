using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IFoodItemRepository : IGenericRepository<FoodItem>
{
    Task<PagedResult<FoodItem>> GetMenuByBoothPagedAsync(
        Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<FoodItem?> GetByBoothAsync(Guid boothId, Guid foodItemId);
    Task<FoodItem?> GetForCartAsync(
        Guid foodItemId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<FoodItem>> GetActiveByIdsAndBoothAsync(
        Guid boothId,
        IReadOnlyCollection<Guid> foodItemIds,
        CancellationToken cancellationToken = default);
}

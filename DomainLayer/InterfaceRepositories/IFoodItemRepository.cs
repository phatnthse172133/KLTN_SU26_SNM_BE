using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IFoodItemRepository : IGenericRepository<FoodItem>
{
    Task<IReadOnlyCollection<FoodItem>> GetMenuByBoothAsync(Guid boothId);
    Task<FoodItem?> GetByBoothAsync(Guid boothId, Guid foodItemId);
}

using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IFoodPriceRepository : IGenericRepository<FoodPrice>
{
    Task<PagedResult<FoodPrice>> GetByFoodItemPagedAsync(
        Guid foodItemId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

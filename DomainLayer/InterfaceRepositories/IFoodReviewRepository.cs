using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IFoodReviewRepository : IGenericRepository<FoodReview>
{
    Task<FoodReview?> GetByIdWithNavAsync(Guid foodReviewId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByOrderDetailAsync(Guid orderDetailId, CancellationToken cancellationToken = default);
    Task<bool> TrySaveNewFoodReviewAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<FoodReview>> GetPagedVisibleByFoodItemAsync(Guid foodItemId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<FoodReview>> GetPagedByCustomerAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task RefreshFoodItemAverageRatingAsync(Guid foodItemId, CancellationToken cancellationToken = default);
}

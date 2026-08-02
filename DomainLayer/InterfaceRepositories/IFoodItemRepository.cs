using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IFoodItemRepository : IGenericRepository<FoodItem>
{
    Task<PagedResult<CustomerFoodReadModel>> GetCustomerPagedAsync(
        Guid? marketId,
        Guid? boothId,
        Guid? categoryId,
        string? search,
        decimal? minPrice,
        decimal? maxPrice,
        bool availableOnly,
        DateTime utcNow,
        TimeOnly localTime,
        int page,
        int pageSize,
        string sort,
        CancellationToken cancellationToken = default);

    Task<CustomerFoodReadModel?> GetCustomerByIdAsync(
        Guid foodItemId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<PagedResult<NightMarketFoodCustomerReadModel>> GetCustomerByNightMarketPagedAsync(
        Guid nightMarketId,
        DateTime utcNow,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<FoodItem>> GetMenuByBoothPagedAsync(
        Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<FoodItem?> GetByBoothAsync(Guid boothId, Guid foodItemId);
    Task<FoodItem?> GetForCartAsync(
        Guid foodItemId,
        CancellationToken cancellationToken = default);
    Task<FoodItem?> GetWithTagsAsync(
        Guid foodItemId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<FoodItem>> GetActiveByIdsAndBoothAsync(
        Guid boothId,
        IReadOnlyCollection<Guid> foodItemIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FoodItem>> GetAiCandidatesAsync(
        Guid? nightMarketId,
        int maxCandidates,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FoodItem>> GetAiOrderableCandidatesAsync(
        Guid? nightMarketId,
        TimeOnly localTime,
        int maxCandidates,
        CancellationToken cancellationToken = default);

    Task<List<FoodItem>> GetAllFoodItemsByIdsAsync(List<Guid> foodItemIds);
    Task<IReadOnlyCollection<FoodItem>> GetSemanticProfileBatchAsync(Guid? foodItemId, int batchSize, CancellationToken cancellationToken = default);
}

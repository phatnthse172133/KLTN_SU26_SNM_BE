using DomainLayer.Common;

namespace DomainLayer.InterfaceRepository;

public interface IAssistantFoodQueryRepository
{
    Task<AssistantFoodQueryResult> GetEligibleFoodsAsync(
        AssistantFoodQueryCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<int> CountNotDeletedFoodItemsAsync(CancellationToken cancellationToken = default);

    Task<AssistantEligibleFood?> GetCurrentByIdAsync(
        Guid foodItemId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

using DomainLayer.Common;

namespace DomainLayer.InterfaceRepository;

public interface IAssistantFoodQueryRepository
{
    Task<IReadOnlyList<AssistantEligibleFood>> GetEligibleFoodsAsync(
        AssistantFoodQueryCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<int> CountNotDeletedFoodItemsAsync(CancellationToken cancellationToken = default);

    Task<AssistantEligibleFood?> GetCurrentByIdAsync(
        Guid foodItemId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

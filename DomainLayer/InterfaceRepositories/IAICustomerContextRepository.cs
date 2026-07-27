using DomainLayer.Common;

namespace DomainLayer.InterfaceRepository;

public interface IAICustomerContextRepository
{
    Task<CustomerRecommendationContext> GetAsync(
        Guid customerId,
        int maxCompletedOrders,
        int maxReviews,
        CancellationToken cancellationToken = default);
}

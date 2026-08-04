using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IPromotionUsageRepository : IGenericRepository<PromotionUsage>
{
    Task<int> CountActiveAsync(Guid promotionId, CancellationToken cancellationToken = default);

    Task<int> CountActiveByCustomerAsync(
        Guid promotionId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<(int Reserved, int Consumed, int Released, decimal TotalDiscount)> GetStatisticsAsync(
        Guid promotionId,
        CancellationToken cancellationToken = default);

    Task<int> ConsumeReservedByOrderAsync(
        Guid orderId,
        DateTime updatedAt,
        CancellationToken cancellationToken = default);

    Task<int> ReleaseReservedByOrderAsync(
        Guid orderId,
        DateTime updatedAt,
        CancellationToken cancellationToken = default);
}

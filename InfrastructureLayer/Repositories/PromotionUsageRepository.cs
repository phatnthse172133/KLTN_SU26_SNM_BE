using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class PromotionUsageRepository : GenericRepository<PromotionUsage>, IPromotionUsageRepository
{
    public PromotionUsageRepository(SNMDbContext context) : base(context)
    {
    }

    public Task<int> CountActiveAsync(
        Guid promotionId,
        CancellationToken cancellationToken = default)
        => _dbSet.CountAsync(
            usage => usage.PromotionId == promotionId
                && usage.Status != PromotionUsageStatus.Released,
            cancellationToken);

    public Task<int> CountActiveByCustomerAsync(
        Guid promotionId,
        Guid customerId,
        CancellationToken cancellationToken = default)
        => _dbSet.CountAsync(
            usage => usage.PromotionId == promotionId
                && usage.CustomerId == customerId
                && usage.Status != PromotionUsageStatus.Released,
            cancellationToken);

    public async Task<(int Reserved, int Consumed, int Released, decimal TotalDiscount)> GetStatisticsAsync(
        Guid promotionId,
        CancellationToken cancellationToken = default)
    {
        var usages = await _dbSet
            .Where(usage => usage.PromotionId == promotionId)
            .Select(usage => new { usage.Status, usage.DiscountAmount })
            .ToListAsync(cancellationToken);

        return (
            usages.Count(usage => usage.Status == PromotionUsageStatus.Reserved),
            usages.Count(usage => usage.Status == PromotionUsageStatus.Consumed),
            usages.Count(usage => usage.Status == PromotionUsageStatus.Released),
            usages
                .Where(usage => usage.Status == PromotionUsageStatus.Consumed)
                .Sum(usage => usage.DiscountAmount));
    }
}

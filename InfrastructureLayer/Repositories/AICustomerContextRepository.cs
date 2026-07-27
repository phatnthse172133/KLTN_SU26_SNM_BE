using DomainLayer.Common;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public sealed class AICustomerContextRepository : IAICustomerContextRepository
{
    private readonly SNMDbContext _context;

    public AICustomerContextRepository(SNMDbContext context) => _context = context;

    public async Task<CustomerRecommendationContext> GetAsync(
        Guid customerId,
        int maxCompletedOrders,
        int maxReviews,
        CancellationToken cancellationToken = default)
    {
        maxCompletedOrders = Math.Clamp(maxCompletedOrders, 1, 50);
        maxReviews = Math.Clamp(maxReviews, 1, 50);

        var orderIds = await _context.Orders
            .AsNoTracking()
            .Where(order =>
                order.CustomerId == customerId &&
                order.Status == OrderStatus.Completed &&
                order.Payments.Any(payment => payment.Status == PaymentStatus.Paid))
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Select(order => order.Id)
            .Take(maxCompletedOrders)
            .ToListAsync(cancellationToken);

        if (orderIds.Count == 0)
            return await WithReviewSignalsAsync(customerId, maxReviews, CustomerRecommendationContext.Empty, cancellationToken);

        var details = _context.OrderDetails
            .AsNoTracking()
            .Where(detail => orderIds.Contains(detail.OrderId));

        var categoryQuantities = await details
            .GroupBy(detail => detail.FoodItem.CategoryId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(detail => detail.Quantity) })
            .ToDictionaryAsync(row => row.Id, row => row.Quantity, cancellationToken);

        var tagQuantities = await details
            .SelectMany(detail => detail.FoodItem.FoodItemTags.Select(tag => new
            {
                tag.FoodTagId,
                detail.Quantity
            }))
            .GroupBy(row => row.FoodTagId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(row => row.Quantity) })
            .ToDictionaryAsync(row => row.Id, row => row.Quantity, cancellationToken);

        var boothQuantities = await details
            .GroupBy(detail => detail.FoodItem.BoothId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(detail => detail.Quantity) })
            .ToDictionaryAsync(row => row.Id, row => row.Quantity, cancellationToken);

        var recentFoodIds = (await details
                .OrderByDescending(detail => detail.Order.CreatedAt)
                .ThenByDescending(detail => detail.CreatedAt)
                .Select(detail => detail.FoodItemId)
                .Take(maxCompletedOrders * 5)
                .ToListAsync(cancellationToken))
            .Distinct()
            .ToHashSet();

        var typicalUnitPrice = await details
            .Select(detail => (decimal?)detail.UnitPrice)
            .AverageAsync(cancellationToken);

        var context = new CustomerRecommendationContext(
            tagQuantities,
            categoryQuantities,
            boothQuantities,
            recentFoodIds,
            new HashSet<Guid>(),
            new HashSet<Guid>(),
            typicalUnitPrice);

        return await WithReviewSignalsAsync(customerId, maxReviews, context, cancellationToken);
    }

    private async Task<CustomerRecommendationContext> WithReviewSignalsAsync(
        Guid customerId,
        int maxReviews,
        CustomerRecommendationContext context,
        CancellationToken cancellationToken)
    {
        var reviews = await _context.Reviews
            .AsNoTracking()
            .Where(review =>
                review.CustomerId == customerId &&
                review.Order.Status == OrderStatus.Completed &&
                review.Order.Payments.Any(payment => payment.Status == PaymentStatus.Paid))
            .OrderByDescending(review => review.CreatedAt)
            .ThenByDescending(review => review.Id)
            .Select(review => new { review.BoothId, review.Rating })
            .Take(maxReviews)
            .ToListAsync(cancellationToken);

        var positive = reviews.Where(review => review.Rating >= 4).Select(review => review.BoothId).ToHashSet();
        var negative = reviews.Where(review => review.Rating <= 2).Select(review => review.BoothId).ToHashSet();
        positive.ExceptWith(negative);

        return context with { PositiveBoothIds = positive, NegativeBoothIds = negative };
    }
}

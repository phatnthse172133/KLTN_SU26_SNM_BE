using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InfrastructureLayer.Repositories;

public class FoodReviewRepository : GenericRepository<FoodReview>, IFoodReviewRepository
{
    public FoodReviewRepository(SNMDbContext context) : base(context)
    {
    }

    public Task<FoodReview?> GetByIdWithNavAsync(Guid foodReviewId, CancellationToken cancellationToken = default)
        => QueryWithNav().FirstOrDefaultAsync(review => review.Id == foodReviewId, cancellationToken);

    public Task<bool> ExistsByOrderDetailAsync(Guid orderDetailId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(review => review.OrderDetailId == orderDetailId, cancellationToken);

    public Task<List<FoodReview>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        => QueryWithNav()
            .Where(review => review.OrderId == orderId)
            .OrderBy(review => review.CreatedAt)
            .ThenBy(review => review.Id)
            .ToListAsync(cancellationToken);

    public async Task<Dictionary<Guid, List<FoodReview>>> GetByOrderIdsAsync(IEnumerable<Guid> orderIds, CancellationToken cancellationToken = default)
    {
        var ids = orderIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, List<FoodReview>>();

        var reviews = await QueryWithNav()
            .Where(review => ids.Contains(review.OrderId))
            .OrderBy(review => review.CreatedAt)
            .ThenBy(review => review.Id)
            .ToListAsync(cancellationToken);

        return reviews
            .GroupBy(review => review.OrderId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    public async Task<bool> TrySaveNewFoodReviewAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "uq_foodreview_orderdetail"
            })
        {
            return false;
        }
    }

    public async Task<PagedResult<FoodReview>> GetPagedVisibleByFoodItemAsync(Guid foodItemId, int page, int pageSize, CancellationToken cancellationToken = default)
        => await ToPagedAsync(QueryWithNav().Where(review => review.FoodItemId == foodItemId && review.IsVisible), page, pageSize, cancellationToken);

    public async Task<PagedResult<FoodReview>> GetPagedByCustomerAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default)
        => await ToPagedAsync(QueryWithNav().Where(review => review.CustomerId == customerId), page, pageSize, cancellationToken);

    public async Task RefreshFoodItemAverageRatingAsync(Guid foodItemId, CancellationToken cancellationToken = default)
    {
        var foodItem = await _context.FoodItems.FirstOrDefaultAsync(item => item.Id == foodItemId, cancellationToken);
        if (foodItem is null)
            return;

        var ratings = await _dbSet
            .Where(review => review.FoodItemId == foodItemId && review.IsVisible)
            .Select(review => review.Rating)
            .ToListAsync(cancellationToken);

        foodItem.AverageRating = ratings.Count == 0 ? 0 : Math.Round(ratings.Average(rating => (decimal)rating), 2);
        foodItem.ReviewCount = ratings.Count;
        foodItem.UpdatedAt = DateTime.UtcNow;

        _context.FoodItems.Update(foodItem);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<FoodReview> QueryWithNav()
        => _dbSet
            .Include(review => review.Customer)
            .Include(review => review.Booth)
            .Include(review => review.FoodItem)
            .Include(review => review.Order)
            .AsSplitQuery();

    private static async Task<PagedResult<FoodReview>> ToPagedAsync(
        IQueryable<FoodReview> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(review => review.CreatedAt)
            .ThenByDescending(review => review.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FoodReview>(items, totalCount);
    }
}

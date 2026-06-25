using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class ReviewRepository : GenericRepository<Review>, IReviewRepository
{
    public ReviewRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<Review?> GetWithReplyByIdAsync(Guid reviewId)
        => await QueryWithReply()
            .FirstOrDefaultAsync(review => review.Id == reviewId);

    public async Task<ReviewReply> UpsertReplyAsync(Guid reviewId, Guid boothOwnerId, string content)
    {
        var now = DateTime.UtcNow;
        var reply = await _context.ReviewReplies.FirstOrDefaultAsync(reply => reply.ReviewId == reviewId);
        if (reply is null)
        {
            reply = new ReviewReply
            {
                Id = Guid.NewGuid(),
                ReviewId = reviewId,
                BoothOwnerId = boothOwnerId,
                Content = content,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _context.ReviewReplies.AddAsync(reply);
            return reply;
        }

        reply.Content = content;
        reply.UpdatedAt = now;
        _context.ReviewReplies.Update(reply);
        return reply;
    }

    public async Task<bool> ExistsByOrderAsync(Guid orderId)
        => await _dbSet.AnyAsync(review => review.OrderId == orderId);

    public async Task<(IEnumerable<Review> Items, int TotalCount)> GetPagedWithReplyAsync(int page, int pageSize)
        => await ToPagedAsync(QueryWithReply(), page, pageSize);

    public async Task<(IEnumerable<Review> Items, int TotalCount)> GetPagedByCustomerWithReplyAsync(Guid customerId, int page, int pageSize)
        => await ToPagedAsync(QueryWithReply().Where(review => review.CustomerId == customerId), page, pageSize);

    public async Task<(IEnumerable<Review> Items, int TotalCount)> GetPagedVisibleByBoothWithReplyAsync(Guid boothId, int page, int pageSize)
        => await ToPagedAsync(QueryWithReply().Where(review => review.BoothId == boothId && review.IsVisible), page, pageSize);

    public async Task RefreshBoothAverageRatingAsync(Guid boothId)
    {
        var booth = await _context.Booths.FirstOrDefaultAsync(booth => booth.Id == boothId);
        if (booth is null)
        {
            return;
        }

        var ratings = await _dbSet
            .Where(review => review.BoothId == boothId)
            .Select(review => review.Rating)
            .ToListAsync();

        booth.AverageRating = ratings.Count == 0 ? 0 : Math.Round(ratings.Average(rating => (decimal)rating), 2);
        booth.UpdatedAt = DateTime.UtcNow;

        _context.Booths.Update(booth);
        await _context.SaveChangesAsync();
    }

    private IQueryable<Review> QueryWithReply()
        => _dbSet
            .Include(review => review.ReviewReply)
            .AsSplitQuery();

    private static async Task<(IEnumerable<Review> Items, int TotalCount)> ToPagedAsync(IQueryable<Review> query, int page, int pageSize)
    {
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(review => review.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}

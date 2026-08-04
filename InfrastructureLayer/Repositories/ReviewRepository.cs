using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

    public async Task<bool> TrySaveNewReviewAsync(CancellationToken cancellationToken = default)
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
                ConstraintName: "uq_review_order"
            })
        {
            return false;
        }
    }

    public async Task<PagedResult<Review>> GetPagedWithReplyAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        => await ToPagedAsync(QueryWithReply(), page, pageSize, cancellationToken);

    public async Task<PagedResult<Review>> GetPagedWithReplyFilteredAsync(int page, int pageSize, short? rating, bool? isVisible, Guid? boothId, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = QueryWithReply();
        if (rating.HasValue)
            query = query.Where(review => review.Rating == rating.Value);
        if (isVisible.HasValue)
            query = query.Where(review => review.IsVisible == isVisible.Value);
        if (boothId.HasValue)
            query = query.Where(review => review.BoothId == boothId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(review =>
                EF.Functions.ILike(review.Content ?? string.Empty, pattern) ||
                EF.Functions.ILike(review.Customer.FullName, pattern) ||
                EF.Functions.ILike(review.Customer.Email, pattern) ||
                EF.Functions.ILike(review.Booth.BoothName, pattern));
        }
        return await ToPagedAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<Review>> GetPagedByCustomerWithReplyAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default)
        => await ToPagedAsync(QueryWithReply().Where(review => review.CustomerId == customerId), page, pageSize, cancellationToken);

    public async Task<PagedResult<Review>> GetPagedVisibleByBoothWithReplyAsync(Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default)
        => await ToPagedAsync(QueryWithReply().Where(review => review.BoothId == boothId && review.IsVisible), page, pageSize, cancellationToken);

    public async Task RefreshBoothAverageRatingAsync(Guid boothId)
    {
        var booth = await _context.Booths.FirstOrDefaultAsync(booth => booth.Id == boothId);
        if (booth is null)
        {
            return;
        }

        var ratings = await _dbSet
            .Where(review => review.BoothId == boothId && review.IsVisible)
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
            .Include(review => review.Customer)
            .Include(review => review.Booth)
            .Include(review => review.Order)
            .AsSplitQuery();

    private static async Task<PagedResult<Review>> ToPagedAsync(
        IQueryable<Review> query,
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

        return new PagedResult<Review>(items, totalCount);
    }
}

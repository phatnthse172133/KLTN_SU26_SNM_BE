using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IReviewRepository : IGenericRepository<Review>
{
    Task<Review?> GetWithReplyByIdAsync(Guid reviewId);
    Task<ReviewReply> UpsertReplyAsync(Guid reviewId, Guid boothOwnerId, string content);
    Task<bool> ExistsByOrderAsync(Guid orderId);
    Task<(IEnumerable<Review> Items, int TotalCount)> GetPagedWithReplyAsync(int page, int pageSize);
    Task<(IEnumerable<Review> Items, int TotalCount)> GetPagedByCustomerWithReplyAsync(Guid customerId, int page, int pageSize);
    Task<(IEnumerable<Review> Items, int TotalCount)> GetPagedVisibleByBoothWithReplyAsync(Guid boothId, int page, int pageSize);
    Task RefreshBoothAverageRatingAsync(Guid boothId);
}

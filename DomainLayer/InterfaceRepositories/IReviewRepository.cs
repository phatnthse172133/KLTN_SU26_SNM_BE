using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IReviewRepository : IGenericRepository<Review>
{
    Task<Review?> GetWithReplyByIdAsync(Guid reviewId);
    Task<ReviewReply> UpsertReplyAsync(Guid reviewId, Guid boothOwnerId, string content);
    Task<bool> ExistsByOrderAsync(Guid orderId);
    Task<PagedResult<Review>> GetPagedWithReplyAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<Review>> GetPagedByCustomerWithReplyAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<Review>> GetPagedVisibleByBoothWithReplyAsync(Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task RefreshBoothAverageRatingAsync(Guid boothId);
}

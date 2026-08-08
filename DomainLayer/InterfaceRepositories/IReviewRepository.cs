using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IReviewRepository : IGenericRepository<Review>
{
    Task<Review?> GetWithReplyByIdAsync(Guid reviewId);
    Task<Review?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, Review>> GetByOrderIdsAsync(IEnumerable<Guid> orderIds, CancellationToken cancellationToken = default);
    Task<ReviewReply> UpsertReplyAsync(Guid reviewId, Guid boothOwnerId, string content);
    Task<bool> ExistsByOrderAsync(Guid orderId);
    Task<bool> TrySaveNewReviewAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<Review>> GetPagedWithReplyAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<Review>> GetPagedWithReplyFilteredAsync(int page, int pageSize, short? rating, bool? isVisible, Guid? boothId, string? keyword, CancellationToken cancellationToken = default);
    Task<PagedResult<Review>> GetPagedByCustomerWithReplyAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<Review>> GetPagedVisibleByBoothWithReplyAsync(Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task RefreshBoothAverageRatingAsync(Guid boothId);
}

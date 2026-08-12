using DomainLayer.Common;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface IComplaintRepository : IGenericRepository<Complaint>
{
    Task AddImagesAsync(IEnumerable<ComplaintImage> images);
    Task AddStatusHistoryAsync(ComplaintStatusHistory history, CancellationToken cancellationToken = default);
    Task<bool> HasActiveComplaintAsync(Guid customerId, Guid boothId, Guid orderId);
    Task<Guid?> GetActiveComplaintIdAsync(Guid customerId, Guid boothId, Guid orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, Guid>> GetActiveComplaintIdsByOrderIdsAsync(Guid customerId, Guid boothId, IEnumerable<Guid> orderIds, CancellationToken cancellationToken = default);
    Task<Complaint?> GetWithImagesByIdAsync(Guid complaintId);
    Task<Complaint?> GetCustomerWithImagesByIdAsync(Guid customerId, Guid complaintId, CancellationToken cancellationToken = default);
    Task<bool> TrySaveNewComplaintAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<Complaint>> GetPagedWithImagesAsync(int page, int pageSize, ComplaintStatus? status = null, CancellationToken cancellationToken = default);
    Task<PagedResult<Complaint>> GetPagedWithImagesFilteredAsync(int page, int pageSize, ComplaintStatus? status, string? keyword, Guid? boothId, CancellationToken cancellationToken = default);
    Task<PagedResult<Complaint>> GetPagedByCustomerWithImagesAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<Complaint>> GetPagedByBoothWithImagesAsync(Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<Complaint>> GetPagedByMarketOwnerWithImagesAsync(Guid marketOwnerId, ComplaintStatus? status, Guid? marketId, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Dictionary<ComplaintStatus, int>> CountByStatusByMarketOwnerAsync(Guid marketOwnerId, CancellationToken cancellationToken = default);
    Task<int> UpdateStatusWithConcurrencyAsync(Guid complaintId, ComplaintStatus expectedPreviousStatus, ComplaintStatus newStatus, string? adminResponse, ComplaintResolutionAction? resolutionAction, string? policyViolation, DateTime updatedAt, string? customerEvidenceRequestNote = null);
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
    Task<Dictionary<ComplaintStatus, int>> CountByStatusAsync(CancellationToken cancellationToken = default);
}

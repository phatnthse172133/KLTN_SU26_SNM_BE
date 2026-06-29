using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IComplaintRepository : IGenericRepository<Complaint>
{
    Task AddImagesAsync(IEnumerable<ComplaintImage> images);
    Task<bool> HasActiveComplaintAsync(Guid customerId, Guid boothId, Guid orderId);
    Task<Complaint?> GetWithImagesByIdAsync(Guid complaintId);
    Task<PagedResult<Complaint>> GetPagedWithImagesAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<Complaint>> GetPagedByCustomerWithImagesAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<Complaint>> GetPagedByBoothWithImagesAsync(Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default);
}

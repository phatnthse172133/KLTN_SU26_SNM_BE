using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IComplaintRepository : IGenericRepository<Complaint>
{
    Task AddImagesAsync(IEnumerable<ComplaintImage> images);
    Task<bool> HasActiveComplaintAsync(Guid customerId, Guid boothId, Guid orderId);
    Task<Complaint?> GetWithImagesByIdAsync(Guid complaintId);
    Task<(IEnumerable<Complaint> Items, int TotalCount)> GetPagedWithImagesAsync(int page, int pageSize);
    Task<(IEnumerable<Complaint> Items, int TotalCount)> GetPagedByCustomerWithImagesAsync(Guid customerId, int page, int pageSize);
    Task<(IEnumerable<Complaint> Items, int TotalCount)> GetPagedByBoothWithImagesAsync(Guid boothId, int page, int pageSize);
}

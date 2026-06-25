using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class ComplaintRepository : GenericRepository<Complaint>, IComplaintRepository
{
    public ComplaintRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task AddImagesAsync(IEnumerable<ComplaintImage> images)
        => await _context.ComplaintImages.AddRangeAsync(images);

    public async Task<bool> HasActiveComplaintAsync(Guid customerId, Guid boothId, Guid orderId)
        => await _dbSet.AnyAsync(complaint =>
            complaint.CustomerId == customerId &&
            complaint.BoothId == boothId &&
            complaint.OrderId == orderId &&
            (complaint.Status == DomainLayer.Enums.GeneralEnum.ComplaintStatus.Submitted ||
             complaint.Status == DomainLayer.Enums.GeneralEnum.ComplaintStatus.UnderInvestigation));

    public async Task<Complaint?> GetWithImagesByIdAsync(Guid complaintId)
        => await QueryWithImages()
            .FirstOrDefaultAsync(complaint => complaint.Id == complaintId);

    public async Task<(IEnumerable<Complaint> Items, int TotalCount)> GetPagedWithImagesAsync(int page, int pageSize)
        => await ToPagedAsync(QueryWithImages(), page, pageSize);

    public async Task<(IEnumerable<Complaint> Items, int TotalCount)> GetPagedByCustomerWithImagesAsync(Guid customerId, int page, int pageSize)
        => await ToPagedAsync(QueryWithImages().Where(complaint => complaint.CustomerId == customerId), page, pageSize);

    public async Task<(IEnumerable<Complaint> Items, int TotalCount)> GetPagedByBoothWithImagesAsync(Guid boothId, int page, int pageSize)
        => await ToPagedAsync(QueryWithImages().Where(complaint => complaint.BoothId == boothId), page, pageSize);

    private IQueryable<Complaint> QueryWithImages()
        => _dbSet
            .Include(complaint => complaint.ComplaintImages)
            .AsSplitQuery();

    private static async Task<(IEnumerable<Complaint> Items, int TotalCount)> ToPagedAsync(IQueryable<Complaint> query, int page, int pageSize)
    {
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(complaint => complaint.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}

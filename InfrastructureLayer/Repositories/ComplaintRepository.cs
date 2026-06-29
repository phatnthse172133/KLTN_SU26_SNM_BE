using DomainLayer.Common;
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

    public async Task<PagedResult<Complaint>> GetPagedWithImagesAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        => await ToPagedAsync(QueryWithImages(), page, pageSize, cancellationToken);

    public async Task<PagedResult<Complaint>> GetPagedByCustomerWithImagesAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default)
        => await ToPagedAsync(QueryWithImages().Where(complaint => complaint.CustomerId == customerId), page, pageSize, cancellationToken);

    public async Task<PagedResult<Complaint>> GetPagedByBoothWithImagesAsync(Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default)
        => await ToPagedAsync(QueryWithImages().Where(complaint => complaint.BoothId == boothId), page, pageSize, cancellationToken);

    private IQueryable<Complaint> QueryWithImages()
        => _dbSet
            .Include(complaint => complaint.ComplaintImages)
            .AsSplitQuery();

    private static async Task<PagedResult<Complaint>> ToPagedAsync(
        IQueryable<Complaint> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(complaint => complaint.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Complaint>(items, totalCount);
    }
}

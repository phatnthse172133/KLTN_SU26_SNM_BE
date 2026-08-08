using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class ComplaintRepository : GenericRepository<Complaint>, IComplaintRepository
{
    public ComplaintRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task AddImagesAsync(IEnumerable<ComplaintImage> images)
        => await _context.ComplaintImages.AddRangeAsync(images);

    public async Task AddStatusHistoryAsync(ComplaintStatusHistory history, CancellationToken cancellationToken = default)
        => await _context.Set<ComplaintStatusHistory>().AddAsync(history, cancellationToken);

    public async Task<bool> HasActiveComplaintAsync(Guid customerId, Guid boothId, Guid orderId)
        => await _dbSet.AnyAsync(complaint =>
            complaint.CustomerId == customerId &&
            complaint.BoothId == boothId &&
            complaint.OrderId == orderId &&
            (complaint.Status == ComplaintStatus.Pending
             || complaint.Status == ComplaintStatus.UnderReview
             || complaint.Status == ComplaintStatus.WaitingForCustomer));

    public Task<Guid?> GetActiveComplaintIdAsync(Guid customerId, Guid boothId, Guid orderId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .Where(complaint =>
                complaint.CustomerId == customerId &&
                complaint.BoothId == boothId &&
                complaint.OrderId == orderId &&
                (complaint.Status == ComplaintStatus.Pending
                 || complaint.Status == ComplaintStatus.UnderReview
                 || complaint.Status == ComplaintStatus.WaitingForCustomer))
            .Select(complaint => (Guid?)complaint.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetActiveComplaintIdsByOrderIdsAsync(
        Guid customerId, Guid boothId, IEnumerable<Guid> orderIds, CancellationToken cancellationToken = default)
    {
        var ids = orderIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, Guid>();

        var items = await _dbSet.AsNoTracking()
            .Where(complaint =>
                complaint.CustomerId == customerId &&
                complaint.BoothId == boothId &&
                ids.Contains(complaint.OrderId) &&
                (complaint.Status == ComplaintStatus.Pending
                 || complaint.Status == ComplaintStatus.UnderReview
                 || complaint.Status == ComplaintStatus.WaitingForCustomer))
            .Select(complaint => new { complaint.OrderId, complaint.Id })
            .ToListAsync(cancellationToken);

        return items
            .GroupBy(item => item.OrderId)
            .ToDictionary(group => group.Key, group => group.First().Id);
    }

    public async Task<Complaint?> GetWithImagesByIdAsync(Guid complaintId)
        => await QueryWithImages()
            .FirstOrDefaultAsync(complaint => complaint.Id == complaintId);

    public Task<Complaint?> GetCustomerWithImagesByIdAsync(Guid customerId, Guid complaintId, CancellationToken cancellationToken = default)
        => QueryWithImages().AsNoTracking()
            .FirstOrDefaultAsync(complaint => complaint.Id == complaintId && complaint.CustomerId == customerId, cancellationToken);

    public async Task<bool> TrySaveNewComplaintAsync(CancellationToken cancellationToken = default)
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
                ConstraintName: "uq_complaint_active_customer_order_booth"
            })
        {
            return false;
        }
    }

    public async Task<PagedResult<Complaint>> GetPagedWithImagesAsync(int page, int pageSize, ComplaintStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = QueryWithImages();
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);
        return await ToPagedAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<Complaint>> GetPagedWithImagesFilteredAsync(int page, int pageSize, ComplaintStatus? status, string? keyword, Guid? boothId, CancellationToken cancellationToken = default)
    {
        var query = QueryWithImages();
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);
        if (boothId.HasValue)
            query = query.Where(c => c.BoothId == boothId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(c => c.Title.ToLower().Contains(kw) || c.Description.ToLower().Contains(kw));
        }
        return await ToPagedAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<int> UpdateStatusWithConcurrencyAsync(
        Guid complaintId,
        ComplaintStatus expectedPreviousStatus,
        ComplaintStatus newStatus,
        string? adminResponse,
        ComplaintResolutionAction? resolutionAction,
        string? policyViolation,
        DateTime updatedAt,
        string? customerEvidenceRequestNote = null)
    {
        var query = _dbSet.Where(c => c.Id == complaintId && c.Status == expectedPreviousStatus);
        return await query.ExecuteUpdateAsync(s => s
            .SetProperty(c => c.Status, newStatus)
            .SetProperty(c => c.AdminResponse, adminResponse)
            .SetProperty(c => c.ResolutionAction, resolutionAction)
            .SetProperty(c => c.PolicyViolation, policyViolation)
            .SetProperty(c => c.CustomerEvidenceRequestNote, customerEvidenceRequestNote)
            .SetProperty(c => c.UpdatedAt, updatedAt));
    }

    public async Task BeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction == null)
            await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
            await _context.Database.CurrentTransaction.CommitAsync();
    }

    public async Task RollbackTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
            await _context.Database.CurrentTransaction.RollbackAsync();
    }

    public async Task<Dictionary<ComplaintStatus, int>> CountByStatusAsync(CancellationToken cancellationToken = default)
    {
        var counts = await _dbSet
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.Status, c => c.Count);
    }

    public async Task<PagedResult<Complaint>> GetPagedByCustomerWithImagesAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default)
        => await ToPagedAsync(QueryWithImages().Where(complaint => complaint.CustomerId == customerId), page, pageSize, cancellationToken);

    public async Task<PagedResult<Complaint>> GetPagedByBoothWithImagesAsync(Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default)
        => await ToPagedAsync(QueryWithImages().Where(complaint => complaint.BoothId == boothId), page, pageSize, cancellationToken);

    public async Task<PagedResult<Complaint>> GetPagedByMarketOwnerWithImagesAsync(Guid marketOwnerId, ComplaintStatus? status, Guid? marketId, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = from complaint in QueryWithImages()
                    join booth in _context.Booths on complaint.BoothId equals booth.Id
                    join market in _context.NightMarkets on booth.NightMarketId equals market.Id
                    where market.MarketOwnerId == marketOwnerId && !market.IsDeleted
                    select new { complaint, booth, market };

        if (status.HasValue)
            query = query.Where(x => x.complaint.Status == status.Value);

        if (marketId.HasValue)
            query = query.Where(x => x.market.Id == marketId.Value);

        if (fromDate.HasValue)
            query = query.Where(x => x.complaint.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
        {
            var endExclusive = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.complaint.CreatedAt < endExclusive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.complaint.CreatedAt)
            .ThenByDescending(x => x.complaint.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.complaint)
            .ToListAsync(cancellationToken);

        return new PagedResult<Complaint>(items, totalCount);
    }

    public async Task<Dictionary<ComplaintStatus, int>> CountByStatusByMarketOwnerAsync(Guid marketOwnerId, CancellationToken cancellationToken = default)
    {
        var query = from complaint in _dbSet
                    join booth in _context.Booths on complaint.BoothId equals booth.Id
                    join market in _context.NightMarkets on booth.NightMarketId equals market.Id
                    where market.MarketOwnerId == marketOwnerId && !market.IsDeleted
                    select new { complaint, market };

        var counts = await query
            .GroupBy(x => x.complaint.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.Status, c => c.Count);
    }

    private IQueryable<Complaint> QueryWithImages()
        => _dbSet
            .Include(complaint => complaint.ComplaintImages)
            .Include(complaint => complaint.StatusHistories)
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
            .ThenByDescending(complaint => complaint.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Complaint>(items, totalCount);
    }
}

using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class ModerationRepository : GenericRepository<ModerationActionHistory>, IModerationRepository
{
    public ModerationRepository(SNMDbContext context) : base(context) { }

    // â”€â”€â”€ Night Market â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<PagedResult<NightMarket>> GetMarketsPagedAsync(
        string? keyword,
        NightMarketStatus? lifecycleStatus,
        ModerationStatus? moderationStatus,
        Guid? marketOwnerId,
        int page,
        int pageSize,
        string sortBy,
        bool ascending,
        CancellationToken cancellationToken = default)
    {
        var query = _context.NightMarkets
            .AsNoTracking()
            .Include(m => m.MarketOwner)
            .Where(m => !m.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(m =>
                EF.Functions.ILike(m.Name, pattern) ||
                EF.Functions.ILike(m.Address, pattern));
        }

        if (lifecycleStatus.HasValue)
            query = query.Where(m => m.Status == lifecycleStatus.Value);

        if (moderationStatus.HasValue)
            query = query.Where(m => m.ModerationStatus == moderationStatus.Value);

        if (marketOwnerId.HasValue)
            query = query.Where(m => m.MarketOwnerId == marketOwnerId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        query = sortBy.ToLower() switch
        {
            "name" => ascending ? query.OrderBy(m => m.Name) : query.OrderByDescending(m => m.Name),
            "status" => ascending ? query.OrderBy(m => m.Status) : query.OrderByDescending(m => m.Status),
            "updatedat" => ascending ? query.OrderBy(m => m.UpdatedAt) : query.OrderByDescending(m => m.UpdatedAt),
            _ => ascending ? query.OrderBy(m => m.CreatedAt) : query.OrderByDescending(m => m.CreatedAt)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NightMarket>(items, totalCount);
    }

    public async Task<NightMarket?> GetMarketDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.NightMarkets
            .AsNoTracking()
            .Include(m => m.MarketOwner)
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, cancellationToken);
    }

    public async Task<int> UpdateMarketModerationStatusAsync(
        Guid marketId,
        ModerationStatus expectedPreviousStatus,
        ModerationStatus newStatus,
        DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        return await _context.NightMarkets
            .Where(m => m.Id == marketId && !m.IsDeleted && m.ModerationStatus == expectedPreviousStatus)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.ModerationStatus, newStatus)
                .SetProperty(m => m.UpdatedAt, updatedAt), cancellationToken);
    }

    // â”€â”€â”€ Booth â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<PagedResult<Booth>> GetBoothsPagedAsync(
        string? keyword,
        BoothStatus? status,
        Guid? nightMarketId,
        Guid? boothOwnerId,
        int page,
        int pageSize,
        string sortBy,
        bool ascending,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Booths
            .AsNoTracking()
            .Include(b => b.BoothOwner)
            .Include(b => b.NightMarket)
            .Include(b => b.Zone)
            .Where(b => true);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(b =>
                EF.Functions.ILike(b.BoothName, pattern) ||
                (b.BoothCode != null && EF.Functions.ILike(b.BoothCode, pattern)) ||
                EF.Functions.ILike(b.BoothOwner.FullName, pattern) ||
                EF.Functions.ILike(b.BoothOwner.Email, pattern));
        }

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        if (nightMarketId.HasValue)
            query = query.Where(b => b.NightMarketId == nightMarketId.Value);

        if (boothOwnerId.HasValue)
            query = query.Where(b => b.BoothOwnerId == boothOwnerId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        query = sortBy.ToLower() switch
        {
            "boothname" => ascending ? query.OrderBy(b => b.BoothName) : query.OrderByDescending(b => b.BoothName),
            "status" => ascending ? query.OrderBy(b => b.Status) : query.OrderByDescending(b => b.Status),
            "updatedat" => ascending ? query.OrderBy(b => b.UpdatedAt) : query.OrderByDescending(b => b.UpdatedAt),
            _ => ascending ? query.OrderBy(b => b.CreatedAt) : query.OrderByDescending(b => b.CreatedAt)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Booth>(items, totalCount);
    }

    public async Task<Booth?> GetBoothDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Booths
            .AsNoTracking()
            .Include(b => b.BoothOwner)
            .Include(b => b.NightMarket)
            .Include(b => b.Zone)
            .Include(b => b.Registration)
                .ThenInclude(r => r.BoothDocuments)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<int> UpdateBoothStatusAsync(
        Guid boothId,
        BoothStatus expectedPreviousStatus,
        BoothStatus newStatus,
        DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        return await _context.Booths
            .Where(b => b.Id == boothId && b.Status == expectedPreviousStatus)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, newStatus)
                .SetProperty(b => b.UpdatedAt, updatedAt), cancellationToken);
    }

    // â”€â”€â”€ History â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<PagedResult<ModerationActionHistory>> GetHistoryByBoothAsync(
        Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.ModerationActionHistories
            .Where(h => h.BoothId == boothId)
            .OrderByDescending(h => h.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ModerationActionHistory>(items, totalCount);
    }

    public async Task<PagedResult<ModerationActionHistory>> GetHistoryByMarketAsync(
        Guid marketId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.ModerationActionHistories
            .Where(h => h.NightMarketId == marketId)
            .OrderByDescending(h => h.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ModerationActionHistory>(items, totalCount);
    }

    // â”€â”€â”€ Complaint counts â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<int> CountComplaintsByMarketAsync(Guid marketId, CancellationToken cancellationToken = default)
    {
        return await (from complaint in _context.Complaints
                      join booth in _context.Booths on complaint.BoothId equals booth.Id
                      where booth.NightMarketId == marketId
                      select complaint)
                      .CountAsync(cancellationToken);
    }

    public async Task<int> CountSeriousComplaintsByMarketAsync(Guid marketId, CancellationToken cancellationToken = default)
    {
        return await (from complaint in _context.Complaints
                      join booth in _context.Booths on complaint.BoothId equals booth.Id
                      where booth.NightMarketId == marketId
                            && complaint.Status == ComplaintStatus.Pending
                      select complaint)
                      .CountAsync(cancellationToken);
    }

    public async Task<int> CountComplaintsByBoothAsync(Guid boothId, CancellationToken cancellationToken = default)
    {
        return await _context.Complaints
            .Where(c => c.BoothId == boothId && c.Status == ComplaintStatus.Pending)
            .CountAsync(cancellationToken);
    }

    public async Task<int> CountActiveBoothsByMarketAsync(Guid marketId, CancellationToken cancellationToken = default)
    {
        return await _context.Booths
            .Where(b => b.NightMarketId == marketId && b.Status == BoothStatus.Active)
            .CountAsync(cancellationToken);
    }

    public async Task<int> CountBoothsByMarketAsync(Guid marketId, CancellationToken cancellationToken = default)
    {
        return await _context.Booths
            .Where(b => b.NightMarketId == marketId)
            .CountAsync(cancellationToken);
    }

    public async Task<List<Complaint>> GetRecentComplaintsByBoothAsync(Guid boothId, int count, CancellationToken cancellationToken = default)
    {
        return await _context.Complaints
            .Where(c => c.BoothId == boothId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Complaint>> GetRecentComplaintsByMarketAsync(Guid marketId, int count, CancellationToken cancellationToken = default)
    {
        return await (from complaint in _context.Complaints
                      join booth in _context.Booths on complaint.BoothId equals booth.Id
                      where booth.NightMarketId == marketId
                      orderby complaint.CreatedAt descending
                      select complaint)
                      .Take(count)
                      .ToListAsync(cancellationToken);
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

    public async Task<Dictionary<Guid, int>> CountComplaintsByBoothIdsAsync(IEnumerable<Guid> boothIds, CancellationToken cancellationToken = default)
    {
        var ids = boothIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        var counts = await _context.Complaints
            .Where(c => ids.Contains(c.BoothId) && c.Status == ComplaintStatus.Pending)
            .GroupBy(c => c.BoothId)
            .Select(g => new { BoothId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.BoothId, x => x.Count);
    }

    public async Task<Dictionary<Guid, int>> CountActiveBoothsByMarketIdsAsync(IEnumerable<Guid> marketIds, CancellationToken cancellationToken = default)
    {
        var ids = marketIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        var counts = await _context.Booths
            .Where(b => ids.Contains(b.NightMarketId) && b.Status == BoothStatus.Active)
            .GroupBy(b => b.NightMarketId)
            .Select(g => new { MarketId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.MarketId, x => x.Count);
    }

    public async Task<Dictionary<Guid, int>> CountBoothsByMarketIdsAsync(IEnumerable<Guid> marketIds, CancellationToken cancellationToken = default)
    {
        var ids = marketIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        var counts = await _context.Booths
            .Where(b => ids.Contains(b.NightMarketId))
            .GroupBy(b => b.NightMarketId)
            .Select(g => new { MarketId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.MarketId, x => x.Count);
    }

    public async Task<Dictionary<Guid, int>> CountSeriousComplaintsByMarketIdsAsync(IEnumerable<Guid> marketIds, CancellationToken cancellationToken = default)
    {
        var ids = marketIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        var counts = await (from complaint in _context.Complaints
                            join booth in _context.Booths on complaint.BoothId equals booth.Id
                            where ids.Contains(booth.NightMarketId)
                                  && complaint.Status == ComplaintStatus.Pending
                            group complaint by booth.NightMarketId into g
                            select new { MarketId = g.Key, Count = g.Count() })
                            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.MarketId, x => x.Count);
    }
}

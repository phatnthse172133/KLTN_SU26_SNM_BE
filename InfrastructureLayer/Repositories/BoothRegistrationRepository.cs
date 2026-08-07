using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class BoothRegistrationRepository : GenericRepository<BoothRegistration>, IBoothRegistrationRepository
{
    public BoothRegistrationRepository(SNMDbContext context) : base(context) { }

    public async Task<PagedResult<BoothRegistration>> GetByOwnerPagedAsync(
        Guid ownerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(registration => registration.OwnerId == ownerId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(registration => registration.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<BoothRegistration>(items, totalCount);
    }

    public async Task<PagedResult<BoothRegistration>> GetPendingPagedAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(x => x.Status == BoothRegistrationStatus.PendingReview);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<BoothRegistration>(items, total);
    }

    public Task<bool> HasPendingAsync(Guid ownerId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(x => x.OwnerId == ownerId && x.Status == BoothRegistrationStatus.PendingReview, cancellationToken);

    public async Task<PagedResult<BoothRegistration>> GetByMarketOwnerPagedAsync(
        Guid marketOwnerId, Guid? marketId, BoothRegistrationStatus? status,
        string? keyword,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = from reg in _dbSet.AsNoTracking()
                        .Include(r => r.Booth)
                        .Include(r => r.Owner)
                    join market in _context.NightMarkets on reg.RequestedNightMarketId equals market.Id
                    where market.MarketOwnerId == marketOwnerId && !market.IsDeleted
                    select new { reg, market };

        if (marketId.HasValue)
            query = query.Where(x => x.reg.RequestedNightMarketId == marketId.Value);

        if (status.HasValue)
            query = query.Where(x => x.reg.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim().ToLower();
            query = query.Where(x => x.reg.BoothName.ToLower().Contains(normalized));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.reg.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.reg)
            .ToListAsync(cancellationToken);

        return new PagedResult<BoothRegistration>(items, total);
    }

    public async Task<Dictionary<BoothRegistrationStatus, int>> GetCountsByMarketOwnerAsync(
        Guid marketOwnerId, Guid? marketId, CancellationToken cancellationToken = default)
    {
        var query = from reg in _dbSet.AsNoTracking()
                    join market in _context.NightMarkets on reg.RequestedNightMarketId equals market.Id
                    where market.MarketOwnerId == marketOwnerId && !market.IsDeleted
                    select new { reg, market };

        if (marketId.HasValue)
            query = query.Where(x => x.reg.RequestedNightMarketId == marketId.Value);

        return await query
            .GroupBy(x => x.reg.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
    }

    public async Task<int> UpdateStatusWithConcurrencyAsync(
        Guid registrationId, BoothRegistrationStatus expectedStatus, BoothRegistrationStatus newStatus,
        string? rejectReason, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(r => r.Id == registrationId && r.Status == expectedStatus)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, newStatus)
                .SetProperty(r => r.RejectReason, rejectReason)
                .SetProperty(r => r.UpdatedAt, updatedAt), cancellationToken);
    }

}

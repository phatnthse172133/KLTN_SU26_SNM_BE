using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories
{
    public class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly SNMDbContext _context;

        public SubscriptionRepository(SNMDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<SubscriptionView>> GetAdminSubscriptionsAsync(PackageType? type, SubscriptionStatus? status, int pageIndex, int pageSize, CancellationToken ct = default)
        {
            var boothQuery = _context.BoothSubscriptions
                .Include(bs => bs.Booth).ThenInclude(b => b.BoothOwner)
                .Include(bs => bs.Package)
                .Select(bs => new SubscriptionView
                {
                    Id = bs.Id,
                    OwnerId = bs.Booth.BoothOwnerId,
                    OwnerName = bs.Booth.BoothOwner.FullName,
                    OwnerEmail = bs.Booth.BoothOwner.Email,
                    PackageType = PackageType.Booth,
                    PackageId = bs.PackageId,
                    PackageName = bs.Package.PackageName,
                    StartDate = bs.StartDate,
                    EndDate = bs.EndDate,
                    Status = bs.Status,
                    AdminNotes = bs.AdminNotes,
                    PaidAmount = bs.PaidAmount,
                    PayOSOrderCode = bs.PayOSOrderCode,
                    PaidAt = bs.PaidAt,
                    CreatedAt = bs.CreatedAt,
                    BuyerName = bs.BuyerName,
                    BuyerEmail = bs.BuyerEmail,
                    BuyerPhone = bs.BuyerPhone
                });

            var marketQuery = _context.MarketSubscriptions
                .Include(ms => ms.MarketOwner)
                .Include(ms => ms.Package)
                .Select(ms => new SubscriptionView
                {
                    Id = ms.Id,
                    OwnerId = ms.MarketOwnerId,
                    OwnerName = ms.MarketOwner.FullName,
                    OwnerEmail = ms.MarketOwner.Email,
                    PackageType = PackageType.Market,
                    PackageId = ms.PackageId,
                    PackageName = ms.Package.PackageName,
                    StartDate = ms.StartDate,
                    EndDate = ms.EndDate,
                    Status = ms.Status,
                    AdminNotes = ms.AdminNotes,
                    PaidAmount = ms.PaidAmount,
                    PayOSOrderCode = ms.PayOSOrderCode,
                    PaidAt = ms.PaidAt,
                    CreatedAt = ms.CreatedAt,
                    BuyerName = ms.BuyerName,
                    BuyerEmail = ms.BuyerEmail,
                    BuyerPhone = ms.BuyerPhone
                });

            var combinedQuery = boothQuery.Concat(marketQuery);

            if (type.HasValue)
            {
                combinedQuery = combinedQuery.Where(s => s.PackageType == type.Value);
            }

            if (status.HasValue)
            {
                combinedQuery = combinedQuery.Where(s => s.Status == status.Value);
            }

            var totalCount = await combinedQuery.CountAsync(ct);
            var items = await combinedQuery
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new PagedResult<SubscriptionView>(items, totalCount);
        }

        public Task<BoothSubscription?> GetBoothSubscriptionByIdAsync(Guid id, CancellationToken ct = default)
        {
            return _context.BoothSubscriptions
                .Include(bs => bs.Package)
                .Include(bs => bs.Booth)
                .FirstOrDefaultAsync(bs => bs.Id == id, ct);
        }

        public Task<MarketSubscription?> GetMarketSubscriptionByIdAsync(Guid id, CancellationToken ct = default)
        {
            return _context.MarketSubscriptions
                .Include(ms => ms.Package)
                .FirstOrDefaultAsync(ms => ms.Id == id, ct);
        }

        public Task<Package?> GetPackageByIdAsync(Guid id, CancellationToken ct = default)
        {
            return _context.Packages.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
        }

        public Task<PackagePolicy?> GetActivePackagePolicyAsync(Guid packageId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return _context.Set<PackagePolicy>()
                .Where(p => p.PackageId == packageId && p.IsActive && p.EffectiveFrom <= now)
                .OrderByDescending(p => p.EffectiveFrom)
                .ThenByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        public async Task SaveChangesAsync(CancellationToken ct = default)
        {
            await _context.SaveChangesAsync(ct);
        }

        public Task<BoothSubscription?> GetActiveBoothSubscriptionAsync(Guid boothId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return _context.BoothSubscriptions
                .Include(bs => bs.Package)
                .Where(bs => bs.BoothId == boothId && bs.Status == SubscriptionStatus.Active && bs.StartDate <= now && bs.EndDate > now)
                .OrderByDescending(bs => bs.EndDate)
                .FirstOrDefaultAsync(ct);
        }

        public Task<BoothSubscription?> GetLatestApprovedBoothSubscriptionAsync(Guid boothId, CancellationToken ct = default)
        {
            return _context.BoothSubscriptions
                .Where(bs => bs.BoothId == boothId && bs.Status == SubscriptionStatus.Active)
                .OrderByDescending(bs => bs.EndDate)
                .FirstOrDefaultAsync(ct);
        }

        public Task<MarketSubscription?> GetActiveMarketSubscriptionAsync(Guid marketOwnerId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return _context.MarketSubscriptions
                .Include(ms => ms.Package)
                .Where(ms => ms.MarketOwnerId == marketOwnerId && ms.Status == SubscriptionStatus.Active && ms.StartDate <= now && ms.EndDate > now)
                .OrderByDescending(ms => ms.EndDate)
                .FirstOrDefaultAsync(ct);
        }

        public Task<MarketSubscription?> GetLatestApprovedMarketSubscriptionAsync(Guid marketOwnerId, CancellationToken ct = default)
        {
            return _context.MarketSubscriptions
                .Where(ms => ms.MarketOwnerId == marketOwnerId && ms.Status == SubscriptionStatus.Active)
                .OrderByDescending(ms => ms.EndDate)
                .FirstOrDefaultAsync(ct);
        }

        public Task<bool> HasPendingBoothSubscriptionAsync(Guid boothId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return _context.BoothSubscriptions
                .AnyAsync(bs => bs.BoothId == boothId
                    && (bs.Status == SubscriptionStatus.PendingPayment
                        || (bs.Status == SubscriptionStatus.Active && bs.StartDate > now)), ct);
        }

        public Task<bool> HasPendingMarketSubscriptionAsync(Guid marketOwnerId, CancellationToken ct = default)
        {
            return _context.MarketSubscriptions
                .AnyAsync(ms => ms.MarketOwnerId == marketOwnerId && ms.Status == SubscriptionStatus.PendingPayment, ct);
        }

        public Task<List<BoothSubscription>> GetBoothSubscriptionHistoryAsync(Guid boothId, CancellationToken ct = default)
        {
            return _context.BoothSubscriptions
                .Include(bs => bs.Package)
                .Where(bs => bs.BoothId == boothId)
                .OrderByDescending(bs => bs.CreatedAt)
                .ToListAsync(ct);
        }

        public Task<List<MarketSubscription>> GetMarketSubscriptionHistoryAsync(Guid marketOwnerId, CancellationToken ct = default)
        {
            return _context.MarketSubscriptions
                .Include(ms => ms.Package)
                .Where(ms => ms.MarketOwnerId == marketOwnerId)
                .OrderByDescending(ms => ms.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task AddBoothSubscriptionAsync(BoothSubscription subscription, CancellationToken ct = default)
        {
            await _context.BoothSubscriptions.AddAsync(subscription, ct);
        }

        public async Task AddMarketSubscriptionAsync(MarketSubscription subscription, CancellationToken ct = default)
        {
            await _context.MarketSubscriptions.AddAsync(subscription, ct);
        }

        public Task<PackagePrice?> GetPackagePriceByIdAsync(Guid packagePriceId, CancellationToken ct = default)
        {
            return _context.PackagePrices.FirstOrDefaultAsync(pp => pp.Id == packagePriceId, ct);
        }

        public Task<List<PackagePrice>> GetActivePricesForPackageAsync(Guid packageId, CancellationToken ct = default)
        {
            return _context.PackagePrices
                .Where(pp => pp.PackageId == packageId && !pp.IsDeleted)
                .ToListAsync(ct);
        }

        public async Task BeginTransactionAsync(CancellationToken ct = default)
        {
            await _context.Database.BeginTransactionAsync(ct);
        }

        public async Task CommitTransactionAsync(CancellationToken ct = default)
        {
            await _context.Database.CommitTransactionAsync(ct);
        }

        public async Task RollbackTransactionAsync(CancellationToken ct = default)
        {
            await _context.Database.RollbackTransactionAsync(ct);
        }

        public async Task<int> UpdateBoothSubscriptionStatusAsync(Guid subscriptionId, SubscriptionStatus expectedStatus, SubscriptionStatus newStatus, DateTime startDate, DateTime endDate, string? adminNotes, CancellationToken ct = default)
        {
            return await _context.BoothSubscriptions
                .Where(bs => bs.Id == subscriptionId && bs.Status == expectedStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(bs => bs.Status, newStatus)
                    .SetProperty(bs => bs.StartDate, startDate)
                    .SetProperty(bs => bs.EndDate, endDate)
                    .SetProperty(bs => bs.AdminNotes, adminNotes)
                    .SetProperty(bs => bs.UpdatedAt, DateTime.UtcNow), ct);
        }

        public async Task<int> UpdateMarketSubscriptionStatusAsync(Guid subscriptionId, SubscriptionStatus expectedStatus, SubscriptionStatus newStatus, DateTime startDate, DateTime endDate, string? adminNotes, CancellationToken ct = default)
        {
            return await _context.MarketSubscriptions
                .Where(ms => ms.Id == subscriptionId && ms.Status == expectedStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(ms => ms.Status, newStatus)
                    .SetProperty(ms => ms.StartDate, startDate)
                    .SetProperty(ms => ms.EndDate, endDate)
                    .SetProperty(ms => ms.AdminNotes, adminNotes)
                    .SetProperty(ms => ms.UpdatedAt, DateTime.UtcNow), ct);
        }

        public async Task<int> CancelActiveBoothSubscriptionsAsync(Guid boothId, CancellationToken ct = default)
        {
            return await _context.BoothSubscriptions
                .Where(bs => bs.BoothId == boothId && bs.Status == SubscriptionStatus.Active)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(bs => bs.Status, SubscriptionStatus.Cancelled)
                    .SetProperty(bs => bs.UpdatedAt, DateTime.UtcNow), ct);
        }

        public async Task<int> CancelActiveMarketSubscriptionsAsync(Guid marketOwnerId, CancellationToken ct = default)
        {
            return await _context.MarketSubscriptions
                .Where(ms => ms.MarketOwnerId == marketOwnerId && ms.Status == SubscriptionStatus.Active)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(ms => ms.Status, SubscriptionStatus.Cancelled)
                    .SetProperty(ms => ms.UpdatedAt, DateTime.UtcNow), ct);
        }

        public Task<BoothSubscription?> GetBoothSubscriptionByOrderCodeAsync(long orderCode, CancellationToken ct = default)
        {
            return _context.BoothSubscriptions
                .Include(bs => bs.Package)
                .Include(bs => bs.Booth)
                .FirstOrDefaultAsync(bs => bs.PayOSOrderCode == orderCode, ct);
        }

        public Task<MarketSubscription?> GetMarketSubscriptionByOrderCodeAsync(long orderCode, CancellationToken ct = default)
        {
            return _context.MarketSubscriptions
                .Include(ms => ms.Package)
                .Include(ms => ms.MarketOwner)
                .FirstOrDefaultAsync(ms => ms.PayOSOrderCode == orderCode, ct);
        }

        public async Task<int> ActivateBoothSubscriptionAsync(Guid subscriptionId, DateTime startDate, DateTime endDate, DateTime paidAt, CancellationToken ct = default)
        {
            return await _context.BoothSubscriptions
                .Where(bs => bs.Id == subscriptionId && bs.Status == SubscriptionStatus.PendingPayment)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(bs => bs.Status, SubscriptionStatus.Active)
                    .SetProperty(bs => bs.StartDate, startDate)
                    .SetProperty(bs => bs.EndDate, endDate)
                    .SetProperty(bs => bs.PaidAt, paidAt)
                    .SetProperty(bs => bs.UpdatedAt, DateTime.UtcNow), ct);
        }

        public async Task<int> ActivateMarketSubscriptionAsync(Guid subscriptionId, DateTime startDate, DateTime endDate, DateTime paidAt, CancellationToken ct = default)
        {
            return await _context.MarketSubscriptions
                .Where(ms => ms.Id == subscriptionId && ms.Status == SubscriptionStatus.PendingPayment)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(ms => ms.Status, SubscriptionStatus.Active)
                    .SetProperty(ms => ms.StartDate, startDate)
                    .SetProperty(ms => ms.EndDate, endDate)
                    .SetProperty(ms => ms.PaidAt, paidAt)
                    .SetProperty(ms => ms.UpdatedAt, DateTime.UtcNow), ct);
        }

        public async Task<int> CancelBoothSubscriptionAsync(Guid subscriptionId, CancellationToken ct = default)
        {
            return await _context.BoothSubscriptions
                .Where(bs => bs.Id == subscriptionId && bs.Status == SubscriptionStatus.PendingPayment)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(bs => bs.Status, SubscriptionStatus.Cancelled)
                    .SetProperty(bs => bs.UpdatedAt, DateTime.UtcNow), ct);
        }

        public async Task<int> CancelMarketSubscriptionAsync(Guid subscriptionId, CancellationToken ct = default)
        {
            return await _context.MarketSubscriptions
                .Where(ms => ms.Id == subscriptionId && ms.Status == SubscriptionStatus.PendingPayment)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(ms => ms.Status, SubscriptionStatus.Cancelled)
                    .SetProperty(ms => ms.UpdatedAt, DateTime.UtcNow), ct);
        }

        public async Task<PackagePolicy?> GetPackagePolicyByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.PackagePolicies
                .FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        public async Task<List<PackagePolicy>> GetPackagePoliciesByPackageAsync(Guid packageId, CancellationToken ct = default)
        {
            return await _context.PackagePolicies
                .Where(p => p.PackageId == packageId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task AddPackagePolicyAsync(PackagePolicy policy, CancellationToken ct = default)
        {
            await _context.PackagePolicies.AddAsync(policy, ct);
        }

        public async Task<int> DeactivateActivePolicyAsync(Guid packageId, CancellationToken ct = default)
        {
            return await _context.PackagePolicies
                .Where(p => p.PackageId == packageId && p.IsActive)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.IsActive, false)
                    .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);
        }

        public async Task<int> ActivatePolicyAsync(Guid policyId, CancellationToken ct = default)
        {
            return await _context.PackagePolicies
                .Where(p => p.Id == policyId && p.EffectiveFrom <= DateTime.UtcNow)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.IsActive, true)
                    .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);
        }
    }
}

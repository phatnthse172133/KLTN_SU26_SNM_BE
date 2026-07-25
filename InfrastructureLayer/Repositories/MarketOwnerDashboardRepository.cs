using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class MarketOwnerDashboardRepository : IMarketOwnerDashboardRepository
{
    private readonly SNMDbContext _context;

    public MarketOwnerDashboardRepository(SNMDbContext context)
    {
        _context = context;
    }

    public Task<List<Guid>> GetOwnedMarketIdsAsync(Guid marketOwnerId, CancellationToken ct = default)
    {
        return _context.NightMarkets
            .AsNoTracking()
            .Where(nm => nm.MarketOwnerId == marketOwnerId && !nm.IsDeleted)
            .Select(nm => nm.Id)
            .ToListAsync(ct);
    }

    public Task<bool> IsMarketOwnedByAsync(Guid marketId, Guid marketOwnerId, CancellationToken ct = default)
    {
        return _context.NightMarkets
            .AsNoTracking()
            .AnyAsync(nm => nm.Id == marketId && nm.MarketOwnerId == marketOwnerId && !nm.IsDeleted, ct);
    }

    public Task<int> CountNightMarketsAsync(Guid marketOwnerId, CancellationToken ct = default)
    {
        return _context.NightMarkets
            .AsNoTracking()
            .CountAsync(nm => nm.MarketOwnerId == marketOwnerId && !nm.IsDeleted, ct);
    }

    public Task<int> CountActiveBoothsAsync(List<Guid> marketIds, Guid? marketId, CancellationToken ct = default)
    {
        if (marketIds.Count == 0) return Task.FromResult(0);
        var filtered = marketId.HasValue ? new List<Guid> { marketId.Value } : marketIds;
        return _context.Booths
            .AsNoTracking()
            .CountAsync(b => filtered.Contains(b.NightMarketId) && b.Status == BoothStatus.Active, ct);
    }

    public Task<int> CountPendingRegistrationsAsync(List<Guid> marketIds, Guid? marketId, CancellationToken ct = default)
    {
        if (marketIds.Count == 0) return Task.FromResult(0);
        var filtered = marketId.HasValue ? new List<Guid> { marketId.Value } : marketIds;
        return _context.BoothRegistrations
            .AsNoTracking()
            .CountAsync(br => filtered.Contains(br.RequestedNightMarketId) && br.Status == BoothRegistrationStatus.PendingReview, ct);
    }

    public async Task<RegistrationStatusCounts> CountRegistrationStatusesAsync(
        List<Guid> marketIds, Guid? marketId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        if (marketIds.Count == 0)
            return new RegistrationStatusCounts(0, 0, 0);

        var filtered = marketId.HasValue ? new List<Guid> { marketId.Value } : marketIds;

        var groups = await _context.BoothRegistrations
            .AsNoTracking()
            .Where(br => filtered.Contains(br.RequestedNightMarketId)
                && br.CreatedAt >= fromUtc && br.CreatedAt < toUtc
                && (br.Status == BoothRegistrationStatus.PendingReview
                    || br.Status == BoothRegistrationStatus.Approved
                    || br.Status == BoothRegistrationStatus.Rejected))
            .GroupBy(br => br.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var pending = groups.FirstOrDefault(x => x.Status == BoothRegistrationStatus.PendingReview)?.Count ?? 0;
        var approved = groups.FirstOrDefault(x => x.Status == BoothRegistrationStatus.Approved)?.Count ?? 0;
        var rejected = groups.FirstOrDefault(x => x.Status == BoothRegistrationStatus.Rejected)?.Count ?? 0;

        return new RegistrationStatusCounts(pending, approved, rejected);
    }

    public async Task<ComplaintStatusCounts> CountComplaintStatusesAsync(
        List<Guid> marketIds, Guid? marketId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        if (marketIds.Count == 0)
            return new ComplaintStatusCounts(0, 0, 0);

        var filtered = marketId.HasValue ? new List<Guid> { marketId.Value } : marketIds;

        var groups = await _context.Complaints
            .AsNoTracking()
            .Where(c => filtered.Contains(c.Booth.NightMarketId)
                && c.CreatedAt >= fromUtc && c.CreatedAt < toUtc)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var pending = groups.FirstOrDefault(x => x.Status == ComplaintStatus.Pending)?.Count ?? 0;
        var resolved = groups.FirstOrDefault(x => x.Status == ComplaintStatus.Resolved)?.Count ?? 0;
        var rejected = groups.FirstOrDefault(x => x.Status == ComplaintStatus.Rejected)?.Count ?? 0;

        return new ComplaintStatusCounts(pending, resolved, rejected);
    }

    public async Task<BoothStatusCounts> CountBoothStatusesAsync(
        List<Guid> marketIds, Guid? marketId, CancellationToken ct = default)
    {
        if (marketIds.Count == 0)
            return new BoothStatusCounts(0, 0, 0, 0);

        var filtered = marketId.HasValue ? new List<Guid> { marketId.Value } : marketIds;

        var groups = await _context.Booths
            .AsNoTracking()
            .Where(b => filtered.Contains(b.NightMarketId))
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new BoothStatusCounts(
            Active: groups.FirstOrDefault(x => x.Status == BoothStatus.Active)?.Count ?? 0,
            Inactive: groups.FirstOrDefault(x => x.Status == BoothStatus.Inactive)?.Count ?? 0,
            Suspended: groups.FirstOrDefault(x => x.Status == BoothStatus.Suspended)?.Count ?? 0,
            Closed: groups.FirstOrDefault(x => x.Status == BoothStatus.Closed)?.Count ?? 0
        );
    }

    public async Task<int> CountValidOrdersAsync(
        List<Guid> marketIds, Guid? marketId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        if (marketIds.Count == 0) return 0;
        var filtered = marketId.HasValue ? new List<Guid> { marketId.Value } : marketIds;

        var validStatuses = new[] { OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.Completed };

        return await _context.Orders
            .AsNoTracking()
            .Where(o => o.Payments.Any(payment => payment.Status == PaymentStatus.Paid)
                && validStatuses.Contains(o.Status)
                && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc
                && o.OrderDetails.Any(od => filtered.Contains(od.FoodItem.Booth.NightMarketId)))
            .Select(o => o.Id)
            .Distinct()
            .CountAsync(ct);
    }

    public async Task<List<OrderTrendRow>> GetOrderTrendAsync(
        List<Guid> marketIds, Guid? marketId, DateTime fromUtc, DateTime toUtc, string granularity, CancellationToken ct = default)
    {
        if (marketIds.Count == 0) return new List<OrderTrendRow>();
        var filtered = marketId.HasValue ? new List<Guid> { marketId.Value } : marketIds;

        var validStatuses = new[] { OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.Completed };

        var query = _context.Orders
            .AsNoTracking()
            .Where(o => o.Payments.Any(payment => payment.Status == PaymentStatus.Paid)
                && validStatuses.Contains(o.Status)
                && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc
                && o.OrderDetails.Any(od => filtered.Contains(od.FoodItem.Booth.NightMarketId)));

        if (granularity == "month")
        {
            var raw = await query
                .Select(o => new { o.Id, o.CreatedAt })
                .Distinct()
                .GroupBy(o => new
                {
                    Year = o.CreatedAt.AddHours(7).Year,
                    Month = o.CreatedAt.AddHours(7).Month
                })
                .Select(g => new { g.Key.Year, g.Key.Month, OrderCount = g.Count() })
                .ToListAsync(ct);

            return raw
                .Select(r => new OrderTrendRow(new DateTime(r.Year, r.Month, 1), r.OrderCount))
                .OrderBy(x => x.BucketStart)
                .ToList();
        }
        else
        {
            var raw = await query
                .Select(o => new { o.Id, o.CreatedAt })
                .Distinct()
                .GroupBy(o => new
                {
                    Year = o.CreatedAt.AddHours(7).Year,
                    Month = o.CreatedAt.AddHours(7).Month,
                    Day = o.CreatedAt.AddHours(7).Day
                })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, OrderCount = g.Count() })
                .ToListAsync(ct);

            return raw
                .Select(r => new OrderTrendRow(new DateTime(r.Year, r.Month, r.Day), r.OrderCount))
                .OrderBy(x => x.BucketStart)
                .ToList();
        }
    }

    public async Task<List<PeakHourRow>> GetPeakHoursAsync(
        List<Guid> marketIds, Guid? marketId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        if (marketIds.Count == 0) return new List<PeakHourRow>();
        var filtered = marketId.HasValue ? new List<Guid> { marketId.Value } : marketIds;

        var validStatuses = new[] { OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.Completed };

        var raw = await queryableValidOrders(_context, filtered, validStatuses, fromUtc, toUtc)
            .Select(o => new { o.Id, o.CreatedAt })
            .Distinct()
            .GroupBy(o => o.CreatedAt.AddHours(7).Hour)
            .Select(g => new { Hour = g.Key, OrderCount = g.Count() })
            .ToListAsync(ct);

        return raw
            .Select(r => new PeakHourRow(r.Hour, r.OrderCount))
            .OrderBy(x => x.Hour)
            .ToList();
    }

    public async Task<List<ZoneActivityRow>> GetZoneActivityAsync(
        List<Guid> marketIds, Guid? marketId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        if (marketIds.Count == 0) return new List<ZoneActivityRow>();
        var filtered = marketId.HasValue ? new List<Guid> { marketId.Value } : marketIds;

        var validStatuses = new[] { OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.Completed };

        var raw = await _context.Orders
            .AsNoTracking()
            .Where(o => o.Payments.Any(payment => payment.Status == PaymentStatus.Paid)
                && validStatuses.Contains(o.Status)
                && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc
                && o.OrderDetails.Any(od => filtered.Contains(od.FoodItem.Booth.NightMarketId)))
            .SelectMany(o => o.OrderDetails
                .Where(od => filtered.Contains(od.FoodItem.Booth.NightMarketId))
                .Select(od => new
                {
                    OrderId = o.Id,
                    ZoneId = (Guid?)od.FoodItem.Booth.ZoneId
                }))
            .Distinct()
            .GroupBy(x => x.ZoneId)
            .Select(g => new { ZoneId = g.Key, OrderCount = g.Select(x => x.OrderId).Distinct().Count() })
            .ToListAsync(ct);

        var zoneIds = raw.Where(x => x.ZoneId.HasValue).Select(x => x.ZoneId!.Value).ToList();
        var zoneNames = zoneIds.Count > 0
            ? await _context.Zones
                .AsNoTracking()
                .Where(z => zoneIds.Contains(z.Id) && !z.IsDeleted)
                .ToDictionaryAsync(z => z.Id, z => z.ZoneName, ct)
            : new Dictionary<Guid, string>();

        var result = raw
            .Select(x => new ZoneActivityRow(
                x.ZoneId.HasValue && zoneNames.TryGetValue(x.ZoneId.Value, out var name) ? name : "Unassigned",
                x.OrderCount
            ))
            .OrderByDescending(x => x.OrderCount)
            .ToList();

        return result;
    }

    private static IQueryable<Order> queryableValidOrders(
        SNMDbContext context, List<Guid> filtered, OrderStatus[] validStatuses, DateTime fromUtc, DateTime toUtc)
    {
        return context.Orders
            .AsNoTracking()
            .Where(o => o.Payments.Any(payment => payment.Status == PaymentStatus.Paid)
                && validStatuses.Contains(o.Status)
                && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc
                && o.OrderDetails.Any(od => filtered.Contains(od.FoodItem.Booth.NightMarketId)));
    }
}

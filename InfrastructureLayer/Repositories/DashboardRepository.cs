using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly SNMDbContext _context;

        public DashboardRepository(SNMDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardStatsModel> GetAdminStatsAsync(DateTime startDate, DateTime endDate)
        {
            var totalUsers = await _context.Users.CountAsync(user => user.CreatedAt < endDate);
            var totalMarkets = await _context.NightMarkets.CountAsync(market => market.CreatedAt < endDate && !market.IsDeleted);
            var totalBooths = await _context.Booths.CountAsync(booth => booth.CreatedAt < endDate);

            // A subscription row is also the historical sale record. Its current
            // status can later become Expired or Cancelled, but a confirmed payment
            // must remain visible in platform revenue. Old verified subscriptions
            // created before PaidAt was persisted use StartDate only when Active;
            // pending/cancelled rows never qualify through that fallback.
            var boothRevenue = await _context.BoothSubscriptions
                .Where(b => b.PaidAmount > 0 && (
                    (b.PaidAt.HasValue && b.PaidAt.Value >= startDate && b.PaidAt.Value < endDate) ||
                    (!b.PaidAt.HasValue && b.Status == DomainLayer.Enums.GeneralEnum.SubscriptionStatus.Active && b.StartDate >= startDate && b.StartDate < endDate)))
                .SumAsync(b => b.PaidAmount);

            var marketRevenue = await _context.MarketSubscriptions
                .Where(m => m.PaidAmount > 0 && (
                    (m.PaidAt.HasValue && m.PaidAt.Value >= startDate && m.PaidAt.Value < endDate) ||
                    (!m.PaidAt.HasValue && m.Status == DomainLayer.Enums.GeneralEnum.SubscriptionStatus.Active && m.StartDate >= startDate && m.StartDate < endDate)))
                .SumAsync(m => m.PaidAmount);

            var newBooths = await _context.Booths.CountAsync(booth => booth.CreatedAt >= startDate && booth.CreatedAt < endDate);
            var newReviews = await _context.Reviews.CountAsync(review => review.CreatedAt >= startDate && review.CreatedAt < endDate);
            var newComplaints = await _context.Complaints.CountAsync(complaint => complaint.CreatedAt >= startDate && complaint.CreatedAt < endDate);

            var boothSubscriptions = _context.BoothSubscriptions.Where(subscription => subscription.CreatedAt < endDate);
            var marketSubscriptions = _context.MarketSubscriptions.Where(subscription => subscription.CreatedAt < endDate);
            var totalSubscriptions = await boothSubscriptions.CountAsync() + await marketSubscriptions.CountAsync();
            var activeSubscriptions =
                await boothSubscriptions.CountAsync(subscription => subscription.Status == DomainLayer.Enums.GeneralEnum.SubscriptionStatus.Active) +
                await marketSubscriptions.CountAsync(subscription => subscription.Status == DomainLayer.Enums.GeneralEnum.SubscriptionStatus.Active);
            var newSubscriptions =
                await boothSubscriptions.CountAsync(subscription => subscription.CreatedAt >= startDate) +
                await marketSubscriptions.CountAsync(subscription => subscription.CreatedAt >= startDate);

            return new DashboardStatsModel
            {
                TotalUsers = totalUsers,
                TotalMarkets = totalMarkets,
                TotalBooths = totalBooths,
                TotalRevenue = boothRevenue + marketRevenue,
                NewBooths = newBooths,
                NewReviews = newReviews,
                NewComplaints = newComplaints,
                TotalSubscriptions = totalSubscriptions,
                ActiveSubscriptions = activeSubscriptions,
                NewSubscriptions = newSubscriptions
            };
        }

        public async Task<List<RevenueChartModel>> GetRevenueChartAsync(DateTime startDate, DateTime endDate, string granularity)
        {
            var boothRev = await _context.BoothSubscriptions.AsNoTracking()
                .Where(b => b.PaidAmount > 0 && (
                    (b.PaidAt.HasValue && b.PaidAt.Value >= startDate && b.PaidAt.Value < endDate) ||
                    (!b.PaidAt.HasValue && b.Status == DomainLayer.Enums.GeneralEnum.SubscriptionStatus.Active && b.StartDate >= startDate && b.StartDate < endDate)))
                .Select(b => new { Date = b.PaidAt ?? b.StartDate, Revenue = b.PaidAmount })
                .ToListAsync();

            var marketRev = await _context.MarketSubscriptions.AsNoTracking()
                .Where(m => m.PaidAmount > 0 && (
                    (m.PaidAt.HasValue && m.PaidAt.Value >= startDate && m.PaidAt.Value < endDate) ||
                    (!m.PaidAt.HasValue && m.Status == DomainLayer.Enums.GeneralEnum.SubscriptionStatus.Active && m.StartDate >= startDate && m.StartDate < endDate)))
                .Select(m => new { Date = m.PaidAt ?? m.StartDate, Revenue = m.PaidAmount })
                .ToListAsync();

            var boothDates = await _context.Booths.AsNoTracking()
                .Where(item => item.CreatedAt >= startDate && item.CreatedAt < endDate)
                .Select(item => item.CreatedAt)
                .ToListAsync();
            var reviewDates = await _context.Reviews.AsNoTracking()
                .Where(item => item.CreatedAt >= startDate && item.CreatedAt < endDate)
                .Select(item => item.CreatedAt)
                .ToListAsync();
            var complaintDates = await _context.Complaints.AsNoTracking()
                .Where(item => item.CreatedAt >= startDate && item.CreatedAt < endDate)
                .Select(item => item.CreatedAt)
                .ToListAsync();
            var boothSubscriptionDates = await _context.BoothSubscriptions.AsNoTracking()
                .Where(item => item.CreatedAt < endDate)
                .Select(item => new { item.CreatedAt, item.Status })
                .ToListAsync();
            var marketSubscriptionDates = await _context.MarketSubscriptions.AsNoTracking()
                .Where(item => item.CreatedAt < endDate)
                .Select(item => new { item.CreatedAt, item.Status })
                .ToListAsync();

            DateTime Bucket(DateTime value) => granularity == "month"
                ? new DateTime(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                : value.Date;

            var revenueByBucket = boothRev.Select(item => new { item.Date, item.Revenue })
                .Concat(marketRev.Select(item => new { item.Date, item.Revenue }))
                .GroupBy(item => Bucket(item.Date))
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Revenue));
            var boothsByBucket = boothDates.GroupBy(Bucket).ToDictionary(group => group.Key, group => group.Count());
            var reviewsByBucket = reviewDates.GroupBy(Bucket).ToDictionary(group => group.Key, group => group.Count());
            var complaintsByBucket = complaintDates.GroupBy(Bucket).ToDictionary(group => group.Key, group => group.Count());
            var allSubscriptions = boothSubscriptionDates.Select(item => new { item.CreatedAt, item.Status })
                .Concat(marketSubscriptionDates.Select(item => new { item.CreatedAt, item.Status }))
                .ToList();
            var newSubscriptionsByBucket = allSubscriptions
                .Where(item => item.CreatedAt >= startDate)
                .GroupBy(item => Bucket(item.CreatedAt))
                .ToDictionary(group => group.Key, group => group.Count());

            var result = new List<RevenueChartModel>();
            for (var cursor = Bucket(startDate); cursor < endDate; cursor = granularity == "month" ? cursor.AddMonths(1) : cursor.AddDays(1))
            {
                var bucketEnd = granularity == "month" ? cursor.AddMonths(1) : cursor.AddDays(1);
                result.Add(new RevenueChartModel
                {
                    Date = cursor.ToString("yyyy-MM-dd"),
                    Revenue = revenueByBucket.GetValueOrDefault(cursor),
                    NewBooths = boothsByBucket.GetValueOrDefault(cursor),
                    NewReviews = reviewsByBucket.GetValueOrDefault(cursor),
                    NewComplaints = complaintsByBucket.GetValueOrDefault(cursor),
                    NewSubscriptions = newSubscriptionsByBucket.GetValueOrDefault(cursor),
                    TotalSubscriptions = allSubscriptions.Count(item => item.CreatedAt < bucketEnd),
                    ActiveSubscriptions = allSubscriptions.Count(item => item.CreatedAt < bucketEnd &&
                        item.Status == DomainLayer.Enums.GeneralEnum.SubscriptionStatus.Active)
                });
            }

            return result;
        }

        public async Task<List<DashboardPendingComplaintModel>> GetPendingComplaintsAsync(int limit = 5)
        {
            return await _context.Complaints
                .Include(c => c.Booth)
                .Include(c => c.Customer)
                .Where(c => c.Status == DomainLayer.Enums.GeneralEnum.ComplaintStatus.Pending)
                .OrderByDescending(c => c.CreatedAt)
                .Take(limit)
                .Select(c => new DashboardPendingComplaintModel
                {
                    Id = c.Id,
                    Title = c.Title,
                    CustomerName = c.Customer != null ? c.Customer.FullName : "Unknown",
                    BoothName = c.Booth != null ? c.Booth.BoothName : "Unknown",
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<List<DashboardRecentRegistrationModel>> GetRecentBoothRegistrationsAsync(int limit = 5)
        {
            return await _context.Booths
                .AsNoTracking()
                .Include(b => b.BoothOwner)
                .Include(b => b.NightMarket)
                .OrderByDescending(b => b.CreatedAt)
                .Take(limit)
                .Select(b => new DashboardRecentRegistrationModel
                {
                    Id = b.Id,
                    BoothName = b.BoothName,
                    OwnerName = b.BoothOwner != null ? b.BoothOwner.FullName : "Unknown",
                    MarketName = b.NightMarket != null ? b.NightMarket.Name : "Unknown",
                    CreatedAt = b.CreatedAt,
                    Status = b.Status.ToString()
                })
                .ToListAsync();
        }
    }
}

using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class BoothDashboardRepository : IBoothDashboardRepository
{
    private readonly SNMDbContext _context;

    public BoothDashboardRepository(SNMDbContext context)
    {
        _context = context;
    }

    public async Task<List<OrderRevenueRow>> GetCompletedOrdersAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var orders = await _context.Orders
            .Where(o => o.BoothOwnerId == boothOwnerId
                && o.Status == OrderStatus.Completed
                && o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
            .Select(o => new
            {
                o.Id,
                o.FinalAmount,
                o.CreatedAt,
                HasPaid = o.Payments.Any(p => p.Status == PaymentStatus.Paid)
            })
            .ToListAsync(ct);

        return orders.Select(o => new OrderRevenueRow
        {
            OrderId = o.Id,
            FinalAmount = o.FinalAmount,
            CreatedAt = o.CreatedAt,
            HasPaidPayment = o.HasPaid
        }).ToList();
    }

    public async Task<List<OrderStatusCountRow>> GetOrderStatusCountsAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        return await _context.Orders
            .Where(o => o.BoothOwnerId == boothOwnerId && o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
            .GroupBy(o => o.Status)
            .Select(g => new OrderStatusCountRow
            {
                Status = g.Key,
                Count = g.Count()
            })
            .ToListAsync(ct);
    }

    public async Task<List<TopFoodRow>> GetTopFoodsAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, int top, CancellationToken ct = default)
    {
        return await _context.OrderDetails
            .Where(od => od.Order.BoothOwnerId == boothOwnerId
                && od.Order.Status == OrderStatus.Completed
                && od.Order.Payments.Any(p => p.Status == PaymentStatus.Paid)
                && od.Order.CreatedAt >= fromDate && od.Order.CreatedAt <= toDate)
            .GroupBy(od => new { od.FoodItemId, od.FoodItem.Name })
            .Select(g => new TopFoodRow
            {
                FoodItemId = g.Key.FoodItemId,
                FoodName = g.Key.Name,
                QuantitySold = g.Sum(od => od.Quantity),
                Revenue = g.Sum(od => od.TotalPrice)
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(top)
            .ToListAsync(ct);
    }

    public async Task<List<BoothPeakHourRow>> GetPeakHoursAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        return await _context.Orders
            .Where(o => o.BoothOwnerId == boothOwnerId
                && o.Status == OrderStatus.Completed
                && o.Payments.Any(p => p.Status == PaymentStatus.Paid)
                && o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
            .GroupBy(o => o.CreatedAt.Hour)
            .Select(g => new BoothPeakHourRow
            {
                Hour = g.Key,
                OrderCount = g.Count()
            })
            .OrderBy(x => x.Hour)
            .ToListAsync(ct);
    }

    public async Task<(double avgRating, int count)> GetRatingSummaryAsync(Guid boothId, CancellationToken ct = default)
    {
        var reviews = await _context.Reviews
            .Where(r => r.BoothId == boothId && r.IsVisible)
            .Select(r => (double)r.Rating)
            .ToListAsync(ct);

        if (reviews.Count == 0) return (0, 0);
        return (reviews.Average(), reviews.Count);
    }

    public async Task<int> GetOutOfStockCountAsync(Guid boothId, CancellationToken ct = default)
    {
        return await _context.FoodItems
            .CountAsync(f => f.BoothId == boothId && !f.IsDeleted && !f.IsAvailable, ct);
    }

    public async Task<List<ActivePromotionRow>> GetActivePromotionsAsync(Guid boothId, DateTime nowUtc, CancellationToken ct = default)
    {
        return await _context.Promotions
            .Where(p => p.BoothId == boothId && !p.IsDeleted
                && p.Status == PromotionStatus.Active
                && p.StartDate <= nowUtc && p.EndDate > nowUtc)
            .Select(p => new ActivePromotionRow
            {
                PromotionId = p.Id,
                Title = p.Title,
                Status = p.Status,
                StartDate = p.StartDate,
                EndDate = p.EndDate
            })
            .ToListAsync(ct);
    }

    public async Task<List<RatingTrendRow>> GetRatingTrendAsync(Guid boothId, DateTime fromDate, DateTime toDate, string granularity, CancellationToken ct = default)
    {
        var reviews = await _context.Reviews
            .Where(r => r.BoothId == boothId && r.IsVisible
                && r.CreatedAt >= fromDate && r.CreatedAt <= toDate)
            .Select(r => new { r.Rating, r.CreatedAt })
            .ToListAsync(ct);

        var result = new List<RatingTrendRow>();

        if (granularity == "hour")
        {
            for (var hour = fromDate; hour <= toDate; hour = hour.AddHours(1))
            {
                var hourReviews = reviews.Where(r => r.CreatedAt >= hour && r.CreatedAt < hour.AddHours(1)).ToList();
                result.Add(new RatingTrendRow
                {
                    BucketStart = hour,
                    AverageRating = hourReviews.Count > 0 ? hourReviews.Average(r => (double)r.Rating) : 0,
                    ReviewCount = hourReviews.Count
                });
            }
        }
        else if (granularity == "day")
        {
            for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
            {
                var dayReviews = reviews.Where(r => r.CreatedAt.Date == date).ToList();
                result.Add(new RatingTrendRow
                {
                    BucketStart = date,
                    AverageRating = dayReviews.Count > 0 ? dayReviews.Average(r => (double)r.Rating) : 0,
                    ReviewCount = dayReviews.Count
                });
            }
        }
        else
        {
            for (var date = new DateTime(fromDate.Year, fromDate.Month, 1); date <= toDate; date = date.AddMonths(1))
            {
                var nextMonth = date.AddMonths(1);
                var monthReviews = reviews.Where(r => r.CreatedAt >= date && r.CreatedAt < nextMonth).ToList();
                result.Add(new RatingTrendRow
                {
                    BucketStart = date,
                    AverageRating = monthReviews.Count > 0 ? monthReviews.Average(r => (double)r.Rating) : 0,
                    ReviewCount = monthReviews.Count
                });
            }
        }

        return result;
    }

    public async Task<ConversionRow> GetConversionAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var orders = await _context.Orders
            .Where(o => o.BoothOwnerId == boothOwnerId && o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
            .Select(o => o.Status)
            .ToListAsync(ct);

        return new ConversionRow
        {
            OrdersPlaced = orders.Count,
            OrdersCompleted = orders.Count(s => s == OrderStatus.Completed),
            OrdersCancelled = orders.Count(s => s == OrderStatus.Cancelled)
        };
    }

    public async Task<int> GetDistinctCustomerCountAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        return await _context.Orders
            .Where(o => o.BoothOwnerId == boothOwnerId && o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
            .Select(o => o.CustomerId)
            .Distinct()
            .CountAsync(ct);
    }

    public async Task<int> GetActiveMenuItemCountAsync(Guid boothId, CancellationToken ct = default)
    {
        return await _context.FoodItems
            .CountAsync(f => f.BoothId == boothId && !f.IsDeleted && f.IsAvailable, ct);
    }

    public async Task<List<RecentOrderRow>> GetRecentOrdersAsync(Guid boothOwnerId, int count, CancellationToken ct = default)
    {
        return await _context.Orders
            .Where(o => o.BoothOwnerId == boothOwnerId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(count)
            .Select(o => new RecentOrderRow
            {
                OrderId = o.Id,
                OrderCode = o.OrderCode,
                Status = o.Status,
                FinalAmount = o.FinalAmount,
                CreatedAt = o.CreatedAt,
                HasPaidPayment = o.Payments.Any(p => p.Status == PaymentStatus.Paid)
            })
            .ToListAsync(ct);
    }

    public async Task<List<TopFoodRow>> GetTopFoodsByRevenueAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, int top, CancellationToken ct = default)
    {
        return await _context.OrderDetails
            .Where(od => od.Order.BoothOwnerId == boothOwnerId
                && od.Order.Status == OrderStatus.Completed
                && od.Order.Payments.Any(p => p.Status == PaymentStatus.Paid)
                && od.Order.CreatedAt >= fromDate && od.Order.CreatedAt <= toDate)
            .GroupBy(od => new { od.FoodItemId, od.FoodItem.Name })
            .Select(g => new TopFoodRow
            {
                FoodItemId = g.Key.FoodItemId,
                FoodName = g.Key.Name,
                QuantitySold = g.Sum(od => od.Quantity),
                Revenue = g.Sum(od => od.TotalPrice)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(top)
            .ToListAsync(ct);
    }

    public async Task<List<PromotionPerformanceRow>> GetPromotionPerformanceAsync(Guid boothId, CancellationToken ct = default)
    {
        return await _context.Promotions
            .Where(p => p.BoothId == boothId && !p.IsDeleted)
            .Select(p => new PromotionPerformanceRow
            {
                PromotionId = p.Id,
                Title = p.Title,
                Status = p.Status,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                UsageCount = p.PromotionUsages.Count(u => u.Status == PromotionUsageStatus.Consumed),
                DiscountTotal = p.PromotionUsages
                    .Where(u => u.Status == PromotionUsageStatus.Consumed)
                    .Sum(u => (decimal?)u.DiscountAmount) ?? 0m
            })
            .OrderByDescending(x => x.UsageCount)
            .ThenByDescending(x => x.StartDate)
            .ToListAsync(ct);
    }

    public async Task<int> GetFeaturedFoodCountAsync(Guid boothId, CancellationToken ct = default)
    {
        return await _context.FoodItems
            .CountAsync(f => f.BoothId == boothId && !f.IsDeleted && f.IsFeatured, ct);
    }
}

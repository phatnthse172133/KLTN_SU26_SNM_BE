using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Subscriptions;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.BoothDashboard;

public class BoothDashboardService : IBoothDashboardService
{
    private readonly IBoothDashboardRepository _dashboardRepo;
    private readonly ISubscriptionEntitlementService _entitlements;
    private readonly IBoothRepository _boothRepo;

    public BoothDashboardService(
        IBoothDashboardRepository dashboardRepo,
        ISubscriptionEntitlementService entitlements,
        IBoothRepository boothRepo)
    {
        _dashboardRepo = dashboardRepo;
        _entitlements = entitlements;
        _boothRepo = boothRepo;
    }

    public async Task<ApiResponse<BoothDashboardResponse>> GetDashboardAsync(
        Guid boothOwnerId,
        DashboardPeriod period,
        CancellationToken ct = default)
    {
        var booth = await _boothRepo.GetByOwnerIdAsync(boothOwnerId, ct);
        if (booth is null)
            throw AppException.NotFound("You do not have a booth.");

        var entitlements = await _entitlements.GetBoothEntitlementsAsync(booth.Id);
        var (fromDate, toDate, granularity) = GetRange(period);
        var nowUtc = DateTime.UtcNow;
        var todayStart = DateTime.UtcNow.Date;

        var completedOrders = await _dashboardRepo.GetCompletedOrdersAsync(boothOwnerId, fromDate, toDate, ct);

        var validOrders = completedOrders
            .Where(o => o.HasPaidPayment)
            .ToList();

        var totalRevenue = validOrders.Sum(o => o.FinalAmount);
        var totalOrders = validOrders.Count;
        var todayOrders = validOrders.Count(o => o.CreatedAt >= todayStart);
        var averageOrderValue = totalOrders > 0 ? Math.Round(totalRevenue / totalOrders, 2) : 0m;

        var (averageRating, reviewCount) = await _dashboardRepo.GetRatingSummaryAsync(booth.Id, ct);
        var outOfStockCount = await _dashboardRepo.GetOutOfStockCountAsync(booth.Id, ct);
        var activeMenuItemCount = await _dashboardRepo.GetActiveMenuItemCountAsync(booth.Id, ct);
        var activePromotions = await _dashboardRepo.GetActivePromotionsAsync(booth.Id, nowUtc, ct);

        var statusCounts = await _dashboardRepo.GetOrderStatusCountsAsync(boothOwnerId, fromDate, toDate, ct);
        var orderStatusBreakdown = statusCounts.Select(s => new OrderStatusBreakdown
        {
            Status = s.Status.ToString(),
            Count = s.Count
        }).ToList();

        var placedOrderCount = statusCounts.Sum(s => s.Count);
        var completedOrderCount = statusCounts.Where(s => s.Status == OrderStatus.Completed).Sum(s => s.Count);
        var cancelledOrderCount = statusCounts.Where(s => s.Status == OrderStatus.Cancelled).Sum(s => s.Count);

        var recentOrderRows = await _dashboardRepo.GetRecentOrdersAsync(boothOwnerId, 10, ct);
        var recentOrders = recentOrderRows.Select(r => new RecentOrderItem
        {
            OrderId = r.OrderId,
            OrderCode = r.OrderCode,
            Status = r.Status.ToString(),
            FinalAmount = r.FinalAmount,
            CreatedAt = r.CreatedAt,
            IsPaid = r.HasPaidPayment
        }).ToList();

        List<RevenueTrendBucket>? revenueTrend = null;
        List<TopFoodItem>? topFoods = null;
        List<PeakHourBucket>? peakHours = null;
        if (entitlements.AdvancedAnalytics)
        {
            revenueTrend = BuildRevenueTrend(validOrders, fromDate, toDate, granularity);

            var topFoodsRows = await _dashboardRepo.GetTopFoodsAsync(boothOwnerId, fromDate, toDate, 5, ct);
            topFoods = topFoodsRows.Select(r => new TopFoodItem
            {
                FoodItemId = r.FoodItemId,
                FoodName = r.FoodName,
                QuantitySold = r.QuantitySold,
                Revenue = r.Revenue
            }).ToList();

            var phRows = await _dashboardRepo.GetPeakHoursAsync(boothOwnerId, fromDate, toDate, ct);
            peakHours = phRows.Select(r => new PeakHourBucket
            {
                Hour = r.Hour,
                Label = $"{r.Hour:00}:00",
                OrderCount = r.OrderCount
            }).ToList();
        }

        var response = new BoothDashboardResponse
        {
            Range = new DashboardRangeInfo
            {
                Period = period.ToString(),
                FromDate = fromDate,
                ToDate = toDate,
                Granularity = granularity
            },
            Summary = new BoothDashboardSummary
            {
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                TodayOrders = todayOrders,
                PlacedOrders = placedOrderCount,
                CompletedOrders = completedOrderCount,
                CancelledOrders = cancelledOrderCount,
                AverageOrderValue = averageOrderValue,
                AverageRating = Math.Round(averageRating, 2),
                ReviewCount = reviewCount,
                OutOfStockCount = outOfStockCount,
                ActiveMenuItemCount = activeMenuItemCount,
                ActivePromotionCount = activePromotions.Count
            },
            Entitlements = new BoothDashboardEntitlements
            {
                AnalyticsTier = ResolveAnalyticsTier(entitlements),
                AdvancedAnalyticsEnabled = entitlements.AdvancedAnalytics,
                PromotionEnabled = entitlements.Promotion,
                FeaturedBoothEnabled = entitlements.FeaturedBooth,
                FeaturedFoodEnabled = entitlements.FeaturedFood,
                PauseBoothEnabled = entitlements.PauseBooth,
                ReviewReplyEnabled = entitlements.ReviewReply
            },
            RevenueTrend = revenueTrend,
            OrderStatusBreakdown = orderStatusBreakdown,
            TopFoods = topFoods,
            PeakHours = peakHours,
            ActivePromotions = activePromotions.Select(p => new ActivePromotionSummary
            {
                PromotionId = p.PromotionId,
                Title = p.Title,
                Status = p.Status.ToString(),
                StartDate = p.StartDate,
                EndDate = p.EndDate
            }).ToList(),
            RecentOrders = recentOrders
        };

        return ApiResponse<BoothDashboardResponse>.SuccessResponse(response);
    }

    internal static string ResolveAnalyticsTier(BoothEntitlements entitlements)
    {
        if (entitlements.FeaturedBooth) return "Featured";
        if (entitlements.AdvancedAnalytics) return "Growth";
        return "Free";
    }

    private static List<RevenueTrendBucket> BuildRevenueTrend(
        List<OrderRevenueRow> orders, DateTime fromDate, DateTime toDate, string granularity)
    {
        var buckets = new List<RevenueTrendBucket>();

        if (granularity == "hour")
        {
            for (var hour = fromDate; hour <= toDate; hour = hour.AddHours(1))
            {
                var nextHour = hour.AddHours(1);
                var hourOrders = orders.Where(o => o.CreatedAt >= hour && o.CreatedAt < nextHour).ToList();
                buckets.Add(new RevenueTrendBucket
                {
                    BucketStart = hour,
                    Label = hour.ToString("HH:00"),
                    Revenue = hourOrders.Sum(o => o.FinalAmount),
                    OrderCount = hourOrders.Count
                });
            }
        }
        else if (granularity == "day")
        {
            for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
            {
                var dayOrders = orders.Where(o => o.CreatedAt.Date == date).ToList();
                buckets.Add(new RevenueTrendBucket
                {
                    BucketStart = date,
                    Label = date.ToString("MM-dd"),
                    Revenue = dayOrders.Sum(o => o.FinalAmount),
                    OrderCount = dayOrders.Count
                });
            }
        }
        else if (granularity == "week")
        {
            for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(7))
            {
                var weekEnd = date.AddDays(6);
                var weekOrders = orders.Where(o => o.CreatedAt >= date && o.CreatedAt < weekEnd.AddDays(1)).ToList();
                buckets.Add(new RevenueTrendBucket
                {
                    BucketStart = date,
                    Label = $"{date:MM-dd} ~ {weekEnd:MM-dd}",
                    Revenue = weekOrders.Sum(o => o.FinalAmount),
                    OrderCount = weekOrders.Count
                });
            }
        }
        else
        {
            for (var date = new DateTime(fromDate.Year, fromDate.Month, 1); date <= toDate; date = date.AddMonths(1))
            {
                var nextMonth = date.AddMonths(1);
                var monthOrders = orders.Where(o => o.CreatedAt >= date && o.CreatedAt < nextMonth).ToList();
                buckets.Add(new RevenueTrendBucket
                {
                    BucketStart = date,
                    Label = date.ToString("yyyy-MM"),
                    Revenue = monthOrders.Sum(o => o.FinalAmount),
                    OrderCount = monthOrders.Count
                });
            }
        }

        return buckets;
    }

    private static (DateTime fromDate, DateTime toDate, string granularity) GetRange(DashboardPeriod period)
    {
        var now = DateTime.UtcNow;
        return period switch
        {
            DashboardPeriod.Today => (now.Date, now, "hour"),
            DashboardPeriod.Week => (now.Date.AddDays(-6), now, "day"),
            DashboardPeriod.Month => (now.Date.AddDays(-29), now, "day"),
            DashboardPeriod.Year => (new DateTime(now.Year, now.Month, 1).AddMonths(-11), now, "month"),
            _ => (now.Date.AddDays(-6), now, "day")
        };
    }
}

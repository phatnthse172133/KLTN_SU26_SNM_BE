using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Subscriptions;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.BoothDashboard;

public class BoothAnalyticsService : IBoothAnalyticsService
{
    private readonly IBoothDashboardRepository _repo;
    private readonly ISubscriptionEntitlementService _entitlements;
    private readonly IBoothRepository _boothRepo;

    public BoothAnalyticsService(
        IBoothDashboardRepository repo,
        ISubscriptionEntitlementService entitlements,
        IBoothRepository boothRepo)
    {
        _repo = repo;
        _entitlements = entitlements;
        _boothRepo = boothRepo;
    }

    public async Task<ApiResponse<BoothAnalyticsResponse>> GetAnalyticsAsync(
        Guid boothOwnerId,
        DashboardPeriod period,
        CancellationToken ct = default)
    {
        var booth = await _boothRepo.GetByOwnerIdAsync(boothOwnerId, ct);
        if (booth is null)
            throw AppException.NotFound("You do not have a booth.");

        await _entitlements.RequireBoothFeatureAsync(
            booth.Id,
            e => e.AdvancedAnalytics,
            "Your current plan does not include advanced analytics. Upgrade to Booth Boost or Booth Featured to access analytics.",
            "ADVANCED_ANALYTICS_NOT_INCLUDED");

        var (fromDate, toDate, granularity) = GetRange(period);

        var completedOrders = await _repo.GetCompletedOrdersAsync(boothOwnerId, fromDate, toDate, ct);
        var validOrders = completedOrders.Where(o => o.HasPaidPayment).ToList();

        var totalRevenue = validOrders.Sum(o => o.FinalAmount);
        var totalOrders = validOrders.Count;
        var distinctCustomers = await _repo.GetDistinctCustomerCountAsync(boothOwnerId, fromDate, toDate, ct);
        var avgOrderValue = totalOrders > 0 ? (double)totalRevenue / totalOrders : 0;

        var conversion = await _repo.GetConversionAsync(boothOwnerId, fromDate, toDate, ct);
        var completionRate = conversion.OrdersPlaced > 0 ? (double)conversion.OrdersCompleted / conversion.OrdersPlaced * 100 : 0;
        var cancellationRate = conversion.OrdersPlaced > 0 ? (double)conversion.OrdersCancelled / conversion.OrdersPlaced * 100 : 0;

        var (avgRating, reviewCount) = await _repo.GetRatingSummaryAsync(booth.Id, ct);

        var revenueTrend = BuildRevenueTrend(validOrders, fromDate, toDate, granularity);
        var orderTrend = BuildOrderTrend(validOrders, fromDate, toDate, granularity);

        var topFoodsRows = await _repo.GetTopFoodsAsync(boothOwnerId, fromDate, toDate, 10, ct);
        var topFoods = topFoodsRows.Select(r => new TopFoodItem
        {
            FoodItemId = r.FoodItemId,
            FoodName = r.FoodName,
            QuantitySold = r.QuantitySold,
            Revenue = r.Revenue
        }).ToList();

        var topFoodsByRevenueRows = await _repo.GetTopFoodsByRevenueAsync(boothOwnerId, fromDate, toDate, 5, ct);
        var topFoodsByRevenue = topFoodsByRevenueRows.Select(r => new TopFoodItem
        {
            FoodItemId = r.FoodItemId,
            FoodName = r.FoodName,
            QuantitySold = r.QuantitySold,
            Revenue = r.Revenue
        }).ToList();

        var peakData = await _repo.GetPeakHoursAsync(boothOwnerId, fromDate, toDate, ct);
        var peakHours = peakData.Select(p => new PeakHourBucket
        {
            Hour = p.Hour,
            Label = $"{p.Hour:00}:00",
            OrderCount = p.OrderCount
        }).ToList();

        var ratingTrendRows = await _repo.GetRatingTrendAsync(booth.Id, fromDate, toDate, granularity, ct);
        var ratingTrend = ratingTrendRows.Select(r => new RatingTrendBucket
        {
            BucketStart = r.BucketStart,
            Label = granularity == "hour" ? r.BucketStart.ToString("HH:00") : granularity == "day" ? r.BucketStart.ToString("MM-dd") : r.BucketStart.ToString("yyyy-MM"),
            AverageRating = Math.Round(r.AverageRating, 2),
            ReviewCount = r.ReviewCount
        }).ToList();

        var response = new BoothAnalyticsResponse
        {
            Range = new DashboardRangeInfo
            {
                Period = period.ToString(),
                FromDate = fromDate,
                ToDate = toDate,
                Granularity = granularity
            },
            Summary = new AnalyticsSummary
            {
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                TotalCustomers = distinctCustomers,
                AverageOrderValue = Math.Round(avgOrderValue, 2),
                CompletionRate = Math.Round(completionRate, 2),
                CancellationRate = Math.Round(cancellationRate, 2),
                AverageRating = Math.Round(avgRating, 2),
                ReviewCount = reviewCount
            },
            RevenueTrend = revenueTrend,
            OrderTrend = orderTrend,
            TopFoods = topFoods,
            TopFoodsByRevenue = topFoodsByRevenue,
            PeakHours = peakHours,
            RatingTrend = ratingTrend,
            Conversion = new ConversionFunnel
            {
                OrdersPlaced = conversion.OrdersPlaced,
                OrdersCompleted = conversion.OrdersCompleted,
                OrdersCancelled = conversion.OrdersCancelled,
                CompletionRate = Math.Round(completionRate, 2),
                CancellationRate = Math.Round(cancellationRate, 2)
            }
        };

        return ApiResponse<BoothAnalyticsResponse>.SuccessResponse(response);
    }

    public async Task<ApiResponse<BoothRevenueSeriesResponse>> GetRevenueSeriesAsync(
        Guid boothOwnerId,
        int days,
        CancellationToken ct = default)
    {
        if (days < 1 || days > 90)
            throw AppException.BadRequest("The requested range must be between 1 and 90 days.", "INVALID_ANALYTICS_RANGE");

        var booth = await _boothRepo.GetByOwnerIdAsync(boothOwnerId, ct);
        if (booth is null)
            throw AppException.NotFound("You do not have a booth.");

        await _entitlements.RequireBoothFeatureAsync(
            booth.Id,
            e => e.AdvancedAnalytics,
            "Your current plan does not include advanced analytics. Upgrade to Booth Boost or Booth Featured to access analytics.",
            "ADVANCED_ANALYTICS_NOT_INCLUDED");

        var toDate = DateTime.UtcNow;
        var fromDate = toDate.Date.AddDays(-(days - 1));

        var completedOrders = await _repo.GetCompletedOrdersAsync(boothOwnerId, fromDate, toDate, ct);
        var validOrders = completedOrders.Where(o => o.HasPaidPayment).ToList();

        var points = BuildRevenueTrend(validOrders, fromDate, toDate, "day");

        var response = new BoothRevenueSeriesResponse
        {
            Range = new DashboardRangeInfo
            {
                Period = $"Last{days}Days",
                FromDate = fromDate,
                ToDate = toDate,
                Granularity = "day"
            },
            Points = points,
            TotalRevenue = validOrders.Sum(o => o.FinalAmount),
            TotalOrders = validOrders.Count
        };

        return ApiResponse<BoothRevenueSeriesResponse>.SuccessResponse(response);
    }

    public async Task<ApiResponse<BoothPromotionPerformanceResponse>> GetPromotionPerformanceAsync(
        Guid boothOwnerId,
        CancellationToken ct = default)
    {
        var booth = await _boothRepo.GetByOwnerIdAsync(boothOwnerId, ct);
        if (booth is null)
            throw AppException.NotFound("You do not have a booth.");

        await _entitlements.RequireBoothFeatureAsync(
            booth.Id,
            e => e.FeaturedBooth,
            "Promotion and recommendation performance analytics require the Booth Featured plan.",
            "FEATURED_ANALYTICS_NOT_INCLUDED");

        var entitlements = await _entitlements.GetBoothEntitlementsAsync(booth.Id);
        var promotionRows = await _repo.GetPromotionPerformanceAsync(booth.Id, ct);
        var featuredFoodCount = await _repo.GetFeaturedFoodCountAsync(booth.Id, ct);

        var response = new BoothPromotionPerformanceResponse
        {
            Promotions = promotionRows.Select(r => new PromotionPerformanceItem
            {
                PromotionId = r.PromotionId,
                Title = r.Title,
                Status = r.Status.ToString(),
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                UsageCount = r.UsageCount,
                DiscountTotal = r.DiscountTotal
            }).ToList(),
            Recommendation = new RecommendationPerformance
            {
                RecommendationPriority = entitlements.RecommendationPriority,
                FeaturedBoothActive = entitlements.FeaturedBooth,
                FeaturedFoodEnabled = entitlements.FeaturedFood,
                FeaturedFoodCount = featuredFoodCount
            }
        };

        return ApiResponse<BoothPromotionPerformanceResponse>.SuccessResponse(response);
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

    private static List<OrderTrendBucket> BuildOrderTrend(
        List<OrderRevenueRow> orders, DateTime fromDate, DateTime toDate, string granularity)
    {
        var buckets = new List<OrderTrendBucket>();

        if (granularity == "hour")
        {
            for (var hour = fromDate; hour <= toDate; hour = hour.AddHours(1))
            {
                var nextHour = hour.AddHours(1);
                buckets.Add(new OrderTrendBucket
                {
                    BucketStart = hour,
                    Label = hour.ToString("HH:00"),
                    OrderCount = orders.Count(o => o.CreatedAt >= hour && o.CreatedAt < nextHour)
                });
            }
        }
        else if (granularity == "day")
        {
            for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
            {
                var dayOrders = orders.Where(o => o.CreatedAt.Date == date).ToList();
                buckets.Add(new OrderTrendBucket
                {
                    BucketStart = date,
                    Label = date.ToString("MM-dd"),
                    OrderCount = dayOrders.Count
                });
            }
        }
        else
        {
            for (var date = new DateTime(fromDate.Year, fromDate.Month, 1); date <= toDate; date = date.AddMonths(1))
            {
                var nextMonth = date.AddMonths(1);
                var monthOrders = orders.Where(o => o.CreatedAt >= date && o.CreatedAt < nextMonth).ToList();
                buckets.Add(new OrderTrendBucket
                {
                    BucketStart = date,
                    Label = date.ToString("yyyy-MM"),
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

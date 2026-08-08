using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ApplicationLayer.DTOs;

public class BoothDashboardResponse
{
    public DashboardRangeInfo Range { get; set; } = new();
    public BoothDashboardSummary Summary { get; set; } = new();
    public BoothDashboardEntitlements Entitlements { get; set; } = new();
    public List<RevenueTrendBucket>? RevenueTrend { get; set; }
    public List<OrderStatusBreakdown>? OrderStatusBreakdown { get; set; }
    public List<TopFoodItem>? TopFoods { get; set; }
    public List<PeakHourBucket>? PeakHours { get; set; }
    public List<ActivePromotionSummary>? ActivePromotions { get; set; }
    public List<RecentOrderItem>? RecentOrders { get; set; }
}

public class BoothDashboardSummary
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int TodayOrders { get; set; }
    public int PlacedOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int OutOfStockCount { get; set; }
    public int ActiveMenuItemCount { get; set; }
    public int ActivePromotionCount { get; set; }
}

public class BoothDashboardEntitlements
{
    public string AnalyticsTier { get; set; } = "Free";
    public bool AdvancedAnalyticsEnabled { get; set; }
    public bool PromotionEnabled { get; set; }
    public bool FeaturedBoothEnabled { get; set; }
    public bool FeaturedFoodEnabled { get; set; }
    public bool PauseBoothEnabled { get; set; }
    public bool ReviewReplyEnabled { get; set; }
}

public class RecentOrderItem
{
    public Guid OrderId { get; set; }
    public long OrderCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal FinalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsPaid { get; set; }
}

public class BoothRevenueSeriesResponse
{
    public DashboardRangeInfo Range { get; set; } = new();
    public List<RevenueTrendBucket> Points { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
}

public class BoothPromotionPerformanceResponse
{
    public List<PromotionPerformanceItem> Promotions { get; set; } = new();
    public RecommendationPerformance Recommendation { get; set; } = new();
}

public class PromotionPerformanceItem
{
    public Guid PromotionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int UsageCount { get; set; }
    public decimal DiscountTotal { get; set; }
}

public class RecommendationPerformance
{
    public double RecommendationPriority { get; set; }
    public bool FeaturedBoothActive { get; set; }
    public bool FeaturedFoodEnabled { get; set; }
    public int FeaturedFoodCount { get; set; }
}

public class RevenueTrendBucket
{
    public DateTime BucketStart { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
}

public class OrderStatusBreakdown
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class TopFoodItem
{
    public Guid FoodItemId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public class ActivePromotionSummary
{
    public Guid PromotionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

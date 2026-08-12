using System;
using System.Collections.Generic;

namespace ApplicationLayer.DTOs;

public class BoothAnalyticsResponse
{
    public DashboardRangeInfo Range { get; set; } = new();
    public AnalyticsSummary Summary { get; set; } = new();
    public List<RevenueTrendBucket>? RevenueTrend { get; set; }
    public List<OrderTrendBucket>? OrderTrend { get; set; }
    public List<TopFoodItem>? TopFoods { get; set; }
    public List<TopFoodItem>? TopFoodsByRevenue { get; set; }
    public List<PeakHourBucket>? PeakHours { get; set; }
    public List<RatingTrendBucket>? RatingTrend { get; set; }
    public ConversionFunnel? Conversion { get; set; }
}

public class AnalyticsSummary
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int TotalCustomers { get; set; }
    public double AverageOrderValue { get; set; }
    public double CompletionRate { get; set; }
    public double CancellationRate { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

public class RatingTrendBucket
{
    public DateTime BucketStart { get; set; }
    public string Label { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

public class ConversionFunnel
{
    public int OrdersPlaced { get; set; }
    public int OrdersCompleted { get; set; }
    public int OrdersCancelled { get; set; }
    public double CompletionRate { get; set; }
    public double CancellationRate { get; set; }
}

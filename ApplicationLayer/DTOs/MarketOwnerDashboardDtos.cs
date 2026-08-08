using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ApplicationLayer.DTOs;

public enum DashboardPeriod
{
    Week = 0,
    Month = 1,
    Year = 2,
    Today = 3
}

public class MarketOwnerDashboardResponse
{
    public DashboardRangeInfo Range { get; set; } = new();
    public DashboardSummary Summary { get; set; } = new();
    public DashboardEntitlements Entitlements { get; set; } = new();
    public List<OrderTrendBucket>? OrderTrend { get; set; }
    public ComplaintStatusBreakdown? ComplaintStatus { get; set; }
    public BoothStatusBreakdown? BoothStatus { get; set; }
    public AdvancedInsights? Advanced { get; set; }
}

public class DashboardRangeInfo
{
    public string Period { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string Granularity { get; set; } = string.Empty;
}

public class DashboardSummary
{
    public int NightMarkets { get; set; }
    public int ActiveBooths { get; set; }
    public int? ValidOrders { get; set; }
    public int? PendingComplaints { get; set; }
}

public class DashboardEntitlements
{
    public bool AdvancedReportsEnabled { get; set; }
    public bool AiInsightsEnabled { get; set; }
    public bool ZoneInsightsEnabled { get; set; }
}

public class OrderTrendBucket
{
    public DateTime BucketStart { get; set; }
    public string Label { get; set; } = string.Empty;
    public int OrderCount { get; set; }
}

public class ComplaintStatusBreakdown
{
    public int Pending { get; set; }
    public int Resolved { get; set; }
    public int Rejected { get; set; }
}

public class BoothStatusBreakdown
{
    public int Active { get; set; }
    public int Inactive { get; set; }
    public int Suspended { get; set; }
    public int Closed { get; set; }
}

public class AdvancedInsights
{
    public List<PeakHourBucket> PeakHours { get; set; } = new();
    public List<ZoneActivityBucket>? ZoneActivity { get; set; }
}

public class PeakHourBucket
{
    public int Hour { get; set; }
    public string Label { get; set; } = string.Empty;
    public int OrderCount { get; set; }
}

public class ZoneActivityBucket
{
    public string ZoneName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
}

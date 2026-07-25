using System;

namespace DomainLayer.Entities
{
    public class DashboardStatsModel
    {
        public int TotalUsers { get; set; }
        public int TotalMarkets { get; set; }
        public int TotalBooths { get; set; }
        public decimal TotalRevenue { get; set; }
        public int NewBooths { get; set; }
        public int NewReviews { get; set; }
        public int NewComplaints { get; set; }
        public int TotalSubscriptions { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int NewSubscriptions { get; set; }
    }

    public class RevenueChartModel
    {
        public string Date { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int NewBooths { get; set; }
        public int NewReviews { get; set; }
        public int NewComplaints { get; set; }
        public int NewSubscriptions { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int TotalSubscriptions { get; set; }
    }
}

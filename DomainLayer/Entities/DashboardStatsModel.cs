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

    public class DashboardPendingComplaintModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string BoothName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class DashboardRecentRegistrationModel
    {
        public Guid Id { get; set; }
        public string BoothName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string MarketName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}

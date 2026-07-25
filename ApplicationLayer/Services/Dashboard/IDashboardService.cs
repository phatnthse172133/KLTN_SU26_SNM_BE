using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.Dashboard
{
    public class DashboardStatsDto
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

    public class RevenueChartDto
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

    public interface IDashboardService
    {
        Task<DashboardStatsDto> GetAdminStatsAsync(DateTime startDate, DateTime endDate);
        Task<List<RevenueChartDto>> GetRevenueChartAsync(DateTime startDate, DateTime endDate, string granularity);
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository
{
    public interface IDashboardRepository
    {
        Task<DashboardStatsModel> GetAdminStatsAsync(DateTime startDate, DateTime endDate);
        Task<List<RevenueChartModel>> GetRevenueChartAsync(DateTime startDate, DateTime endDate, string granularity);
        Task<List<DashboardPendingComplaintModel>> GetPendingComplaintsAsync(int limit = 5);
    }
}

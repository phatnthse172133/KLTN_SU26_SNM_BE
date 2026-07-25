using System.Collections.Generic;
using System.Threading.Tasks;
using DomainLayer.InterfaceRepository;
using DomainLayer.Entities;
using System.Linq;
using ApplicationLayer.Exceptions;

namespace ApplicationLayer.Services.Dashboard
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardRepository _dashboardRepository;

        public DashboardService(IDashboardRepository dashboardRepository)
        {
            _dashboardRepository = dashboardRepository;
        }

        public async Task<DashboardStatsDto> GetAdminStatsAsync(DateTime startDate, DateTime endDate)
        {
            ValidateDateRange(startDate, endDate);
            var res = await _dashboardRepository.GetAdminStatsAsync(startDate, endDate);
            return new DashboardStatsDto {
                TotalUsers = res.TotalUsers,
                TotalMarkets = res.TotalMarkets,
                TotalBooths = res.TotalBooths,
                TotalRevenue = res.TotalRevenue,
                NewBooths = res.NewBooths,
                NewReviews = res.NewReviews,
                NewComplaints = res.NewComplaints,
                TotalSubscriptions = res.TotalSubscriptions,
                ActiveSubscriptions = res.ActiveSubscriptions,
                NewSubscriptions = res.NewSubscriptions
            };
        }

        public async Task<List<RevenueChartDto>> GetRevenueChartAsync(DateTime startDate, DateTime endDate, string granularity)
        {
            ValidateDateRange(startDate, endDate);
            var res = await _dashboardRepository.GetRevenueChartAsync(startDate, endDate, granularity);
            return res.Select(r => new RevenueChartDto
            {
                Date = r.Date,
                Revenue = r.Revenue,
                NewBooths = r.NewBooths,
                NewReviews = r.NewReviews,
                NewComplaints = r.NewComplaints,
                NewSubscriptions = r.NewSubscriptions,
                ActiveSubscriptions = r.ActiveSubscriptions,
                TotalSubscriptions = r.TotalSubscriptions
            }).ToList();
        }

        private static void ValidateDateRange(DateTime startDate, DateTime endDate)
        {
            if (startDate >= endDate)
                throw AppException.BadRequest("Start date must be before end date.", "INVALID_DATE_RANGE");
            if (endDate - startDate > TimeSpan.FromDays(366))
                throw AppException.BadRequest("Date range cannot exceed 366 days.", "INVALID_DATE_RANGE");
        }
    }
}

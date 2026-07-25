using System.Threading.Tasks;
using ApplicationLayer.Services.Dashboard;
using ApplicationLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/dashboard")]
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public AdminDashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var range = NormalizeRange(startDate, endDate);
            var stats = await _dashboardService.GetAdminStatsAsync(range.Start, range.End);
            return Ok(new { Message = "Dashboard stats retrieved successfully", Data = stats });
        }

        [HttpGet("revenue-chart")]
        public async Task<IActionResult> GetRevenueChart(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string granularity = "day",
            [FromQuery] int days = 30)
        {
            if (startDate is null && endDate is null)
                startDate = DateTime.UtcNow.Date.AddDays(-Math.Clamp(days, 1, 366) + 1);

            var range = NormalizeRange(startDate, endDate);
            var normalizedGranularity = granularity.Equals("month", StringComparison.OrdinalIgnoreCase) ? "month" : "day";
            var chart = await _dashboardService.GetRevenueChartAsync(range.Start, range.End, normalizedGranularity);
            return Ok(new { Message = "Revenue chart retrieved successfully", Data = chart });
        }

        private static (DateTime Start, DateTime End) NormalizeRange(DateTime? startDate, DateTime? endDate)
        {
            var end = (endDate ?? DateTime.UtcNow.Date.AddDays(1)).ToUniversalTime();
            var start = (startDate ?? end.AddDays(-30)).ToUniversalTime();

            if (end <= start)
                throw AppException.BadRequest("End date must be later than start date.", "INVALID_DATE_RANGE");
            if ((end - start).TotalDays > 366)
                throw AppException.BadRequest("Dashboard date range must not exceed 366 days.", "INVALID_DATE_RANGE");

            return (start, end);
        }
    }
}

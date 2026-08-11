using System.Security.Claims;
using ApplicationLayer.DTOs;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.BoothDashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "BoothOwner")]
[Route("api/booth-owner")]
public class BoothOwnerDashboardController : ControllerBase
{
    private readonly IBoothDashboardService _dashboardService;
    private readonly IBoothAnalyticsService _analyticsService;
    public BoothOwnerDashboardController(IBoothDashboardService dashboardService, IBoothAnalyticsService analyticsService)
    {
        _dashboardService = dashboardService;
        _analyticsService = analyticsService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] string range = "week", CancellationToken cancellationToken = default)
    {
        var period = range.ToLowerInvariant() switch
        {
            "today" => DashboardPeriod.Today,
            "week" => DashboardPeriod.Week,
            "month" => DashboardPeriod.Month,
            "year" => DashboardPeriod.Year,
            _ => DashboardPeriod.Week
        };

        var response = await _dashboardService.GetDashboardAsync(CurrentUserId, period, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics([FromQuery] string range = "week", CancellationToken cancellationToken = default)
    {
        var period = range.ToLowerInvariant() switch
        {
            "today" => DashboardPeriod.Today,
            "week" => DashboardPeriod.Week,
            "month" => DashboardPeriod.Month,
            "year" => DashboardPeriod.Year,
            _ => DashboardPeriod.Week
        };

        var response = await _analyticsService.GetAnalyticsAsync(CurrentUserId, period, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("analytics/revenue-series")]
    public async Task<IActionResult> GetRevenueSeries([FromQuery] int days = 14, CancellationToken cancellationToken = default)
    {
        var response = await _analyticsService.GetRevenueSeriesAsync(CurrentUserId, days, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("analytics/promotions")]
    public async Task<IActionResult> GetPromotionPerformance(CancellationToken cancellationToken = default)
    {
        var response = await _analyticsService.GetPromotionPerformanceAsync(CurrentUserId, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}

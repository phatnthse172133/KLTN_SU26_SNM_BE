using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.MarketOwnerDashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/market-owner/dashboard")]
[Authorize(Roles = "MarketOwner")]
public class MarketOwnerDashboardController : ControllerBase
{
    private readonly IMarketOwnerDashboardService _service;

    public MarketOwnerDashboardController(IMarketOwnerDashboardService service)
    {
        _service = service;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] Guid? marketId,
        [FromQuery] string period = "week",
        CancellationToken ct = default)
    {
        var parsedPeriod = period.ToLowerInvariant() switch
        {
            "week" => DashboardPeriod.Week,
            "month" => DashboardPeriod.Month,
            "year" => DashboardPeriod.Year,
            _ => throw ApplicationLayer.Exceptions.AppException.BadRequest("Invalid period. Use: week, month, or year.", "INVALID_DASHBOARD_PERIOD")
        };

        var result = await _service.GetDashboardAsync(CurrentUserId, marketId, parsedPeriod, ct);
        return Ok(result);
    }
}

using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.AdminModeration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminModerationController : ControllerBase
{
    private readonly IAdminModerationService _service;

    public AdminModerationController(IAdminModerationService service)
    {
        _service = service;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentUserName => User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Admin";

    // ─── Night Market Moderation ──────────────────────────────────

    [HttpGet("night-markets")]
    public async Task<IActionResult> GetMarkets([FromQuery] AdminMarketModerationQueryRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetMarketsAsync(request, cancellationToken));

    [HttpGet("night-markets/{marketId:guid}")]
    public async Task<IActionResult> GetMarketDetail(Guid marketId, CancellationToken cancellationToken)
    {
        var response = await _service.GetMarketDetailAsync(marketId, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpPatch("night-markets/{marketId:guid}/moderation-status")]
    public async Task<IActionResult> ChangeMarketModerationStatus(Guid marketId, ChangeModerationStatusRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.ChangeMarketModerationStatusAsync(CurrentUserId, CurrentUserName, marketId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("night-markets/{marketId:guid}/moderation-history")]
    public async Task<IActionResult> GetMarketHistory(
        Guid marketId,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetMarketHistoryAsync(marketId, page, pageSize, cancellationToken));

    // ─── Booth Moderation ─────────────────────────────────────────

    [HttpGet("booths")]
    public async Task<IActionResult> GetBooths([FromQuery] AdminBoothModerationQueryRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetBoothsAsync(request, cancellationToken));

    [HttpGet("booths/{boothId:guid}")]
    public async Task<IActionResult> GetBoothDetail(Guid boothId, CancellationToken cancellationToken)
    {
        var response = await _service.GetBoothDetailAsync(boothId, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpPost("moderation/booths/{boothId:guid}/ban")]
    public async Task<IActionResult> BanBooth(Guid boothId, BoothModerationActionRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.BanBoothAsync(CurrentUserId, CurrentUserName, boothId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("moderation/booths/{boothId:guid}/restore")]
    public async Task<IActionResult> RestoreBooth(Guid boothId, BoothModerationActionRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.RestoreBoothAsync(CurrentUserId, CurrentUserName, boothId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("booths/{boothId:guid}/moderation-history")]
    public async Task<IActionResult> GetBoothHistory(
        Guid boothId,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetBoothHistoryAsync(boothId, page, pageSize, cancellationToken));
}

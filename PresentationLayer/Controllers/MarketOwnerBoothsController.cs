using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Booths;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/market-owner/booths")]
[Authorize(Roles = "MarketOwner")]
public class MarketOwnerBoothsController : ControllerBase
{
    private readonly IBoothService _service;
    public MarketOwnerBoothsController(IBoothService service)
    {
        _service = service;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("~/api/market-owner/booth-owners/search")]
    public async Task<IActionResult> SearchBoothOwners(
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.SearchBoothOwnersAsync(keyword, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetBooths(
        [FromQuery] Guid? marketId,
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] PaginationReq pagination,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetByMarketOwnerAsync(CurrentUserId, marketId, keyword, status, pagination, cancellationToken));
    }

    [HttpPost("~/api/market-owner/markets/{marketId:guid}/booths")]
    public async Task<IActionResult> CreateBooth(
        Guid marketId,
        MarketOwnerCreateBoothRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _service.CreateByMarketOwnerAsync(CurrentUserId, marketId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("~/api/market-owner/markets/{marketId:guid}/layouts/{layoutId:guid}/slots/{nodeId:guid}/booths")]
    public async Task<IActionResult> CreateAndAssignBooth(
        Guid marketId, Guid layoutId, Guid nodeId,
        MarketOwnerCreateBoothRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _service.CreateAndAssignBoothAsync(CurrentUserId, marketId, layoutId, nodeId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPut("~/api/market-owner/markets/{marketId:guid}/layouts/{layoutId:guid}/slots/{nodeId:guid}/booths/{boothId:guid}")]
    public async Task<IActionResult> AssignBooth(
        Guid marketId, Guid layoutId, Guid nodeId, Guid boothId,
        CancellationToken cancellationToken)
    {
        var response = await _service.AssignBoothAsync(CurrentUserId, marketId, layoutId, nodeId, boothId, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpDelete("~/api/market-owner/markets/{marketId:guid}/layouts/{layoutId:guid}/slots/{nodeId:guid}/booth")]
    public async Task<IActionResult> ReleaseSlotBooth(
        Guid marketId, Guid layoutId, Guid nodeId,
        CancellationToken cancellationToken)
    {
        var response = await _service.ReleaseSlotBoothAsync(CurrentUserId, marketId, layoutId, nodeId, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPut("{boothId:guid}")]
    public async Task<IActionResult> UpdateBooth(
        Guid boothId,
        MarketOwnerUpdateBoothRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _service.UpdateByMarketOwnerAsync(CurrentUserId, boothId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPatch("{boothId:guid}/status")]
    public async Task<IActionResult> ChangeStatus(
        Guid boothId,
        MarketOwnerChangeBoothStatusRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _service.ChangeStatusByMarketOwnerAsync(CurrentUserId, boothId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpDelete("{boothId:guid}")]
    public async Task<IActionResult> DeleteBooth(
        Guid boothId,
        CancellationToken cancellationToken)
    {
        var response = await _service.DeleteByMarketOwnerAsync(CurrentUserId, boothId, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}

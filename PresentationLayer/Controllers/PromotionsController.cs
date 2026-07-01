using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Promotions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionService _service;

    public PromotionsController(IPromotionService service)
    {
        _service = service;
    }

    private Guid CurrentUserId
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string CurrentRole
        => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    [Authorize(Roles = "BoothOwner")]
    [HttpGet("api/booths/{boothId:guid}/promotions")]
    public async Task<IActionResult> GetByBooth(
        Guid boothId,
        [FromQuery] PromotionListRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.GetByBoothAsync(
            CurrentUserId,
            boothId,
            request,
            cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpGet("api/admin/promotions")]
    public async Task<IActionResult> GetAll(
        [FromQuery] PromotionListRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(request, cancellationToken));

    [Authorize]
    [HttpGet("api/promotions/{promotionId:guid}")]
    public async Task<IActionResult> Get(
        Guid promotionId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetAsync(
            CurrentUserId,
            CurrentRole,
            promotionId,
            cancellationToken));

    [Authorize(Roles = "BoothOwner")]
    [HttpPost("api/booths/{boothId:guid}/promotions")]
    public async Task<IActionResult> Create(
        Guid boothId,
        CreatePromotionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _service.CreateAsync(
            CurrentUserId,
            boothId,
            request,
            cancellationToken);
        return CreatedAtAction(
            nameof(Get),
            new { promotionId = response.Data!.Id },
            response);
    }

    [Authorize(Roles = "BoothOwner")]
    [HttpPut("api/promotions/{promotionId:guid}")]
    public async Task<IActionResult> Update(
        Guid promotionId,
        UpdatePromotionRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(
            CurrentUserId,
            promotionId,
            request,
            cancellationToken));

    [Authorize(Roles = "BoothOwner")]
    [HttpPatch("api/promotions/{promotionId:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid promotionId,
        CancellationToken cancellationToken)
        => Ok(await _service.ActivateAsync(
            CurrentUserId,
            promotionId,
            cancellationToken));

    [Authorize(Roles = "BoothOwner")]
    [HttpPatch("api/promotions/{promotionId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid promotionId,
        CancellationToken cancellationToken)
        => Ok(await _service.DeactivateAsync(
            CurrentUserId,
            promotionId,
            cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPatch("api/promotions/{promotionId:guid}/suspend")]
    public async Task<IActionResult> Suspend(
        Guid promotionId,
        CancellationToken cancellationToken)
        => Ok(await _service.SuspendAsync(promotionId, cancellationToken));

    [Authorize(Roles = "BoothOwner")]
    [HttpDelete("api/promotions/{promotionId:guid}")]
    public async Task<IActionResult> Delete(
        Guid promotionId,
        CancellationToken cancellationToken)
        => Ok(await _service.DeleteAsync(
            CurrentUserId,
            promotionId,
            cancellationToken));

    [Authorize(Roles = "BoothOwner,Admin")]
    [HttpGet("api/promotions/{promotionId:guid}/usage-statistics")]
    public async Task<IActionResult> GetUsageStatistics(
        Guid promotionId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetUsageStatisticsAsync(
            CurrentUserId,
            CurrentRole,
            promotionId,
            cancellationToken));

    [Authorize(Roles = "Customer")]
    [HttpGet("api/cart/available-promotions")]
    public async Task<IActionResult> GetAvailableForCart(
        [FromQuery] PaginationReq pagination,
        CancellationToken cancellationToken)
        => Ok(await _service.GetAvailableForCartAsync(
            CurrentUserId,
            pagination,
            cancellationToken));

    [Authorize(Roles = "Customer")]
    [HttpPost("api/cart/promotions/validate")]
    public async Task<IActionResult> ValidateForCart(
        ValidateCartPromotionRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.ValidateForCartAsync(
            CurrentUserId,
            request,
            cancellationToken));
}

using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.BoothRegistrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/booth-registrations")]
public class BoothRegistrationsController : ControllerBase
{
    private readonly IBoothRegistrationService _service;
    public BoothRegistrationsController(IBoothRegistrationService service)
    {
        _service = service;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [Authorize(Roles = "BoothOwner")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateBoothRegistrationRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.CreateAsync(UserId, request, cancellationToken);
        return response.Success ? CreatedAtAction(nameof(GetMine), new { }, response) : BadRequest(response);
    }

    [Authorize(Roles = "BoothOwner")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        [FromQuery] PaginationReq pagination,
        CancellationToken cancellationToken)
        => Ok(await _service.GetMineAsync(UserId, pagination, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending([FromQuery] PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetPendingAsync(pagination, cancellationToken));
    }

    [Authorize(Roles = "MarketOwner")]
    [HttpGet("market-owner")]
    public async Task<IActionResult> GetByMarketOwner(
        [FromQuery] Guid? marketId,
        [FromQuery] string? status,
        [FromQuery] string? keyword,
        [FromQuery] PaginationReq pagination,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetByMarketOwnerAsync(UserId, marketId, status, keyword, pagination, cancellationToken));
    }

    [Authorize(Roles = "MarketOwner")]
    [HttpGet("market-owner/counts")]
    public async Task<IActionResult> GetCountsByMarketOwner(
        [FromQuery] Guid? marketId,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetCountsByMarketOwnerAsync(UserId, marketId, cancellationToken));
    }

    [Authorize(Roles = "MarketOwner,Admin")]
    [HttpPut("{registrationId:guid}/review")]
    public async Task<IActionResult> Review(Guid registrationId, ReviewBoothRegistrationRequest request, CancellationToken cancellationToken)
    {
        var actorId = User.IsInRole("MarketOwner") ? UserId : (Guid?)null;
        var response = await _service.ReviewAsync(registrationId, request, cancellationToken, actorId);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}

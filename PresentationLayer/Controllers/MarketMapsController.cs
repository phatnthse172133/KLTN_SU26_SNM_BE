using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.MarketMaps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "MarketOwner")]
[Route("api/market-owner/night-markets/{nightMarketId:guid}/market-maps")]
public sealed class MarketMapsController : ControllerBase
{
    private readonly IMarketMapService _service;

    public MarketMapsController(IMarketMapService service)
        => _service = service;

    private Guid ActorId
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        Guid nightMarketId, CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(nightMarketId, ActorId, cancellationToken));

    [HttpGet("eligible-layouts")]
    public async Task<IActionResult> GetEligibleLayouts(
        Guid nightMarketId, CancellationToken cancellationToken)
        => Ok(await _service.GetEligibleLayoutsAsync(
            nightMarketId, ActorId, cancellationToken));

    [HttpGet("{marketMapId:guid}")]
    public async Task<IActionResult> Get(
        Guid nightMarketId, Guid marketMapId, CancellationToken cancellationToken)
        => Ok(await _service.GetByIdAsync(
            nightMarketId, marketMapId, ActorId, cancellationToken));

    [HttpPost("drafts")]
    public async Task<IActionResult> CreateDraft(
        Guid nightMarketId,
        CreateMarketMapDraftRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _service.CreateDraftAsync(
            nightMarketId, request, ActorId, cancellationToken);
        return CreatedAtAction(
            nameof(Get),
            new { nightMarketId, marketMapId = response.Data!.Id },
            response);
    }

    [HttpPut("{marketMapId:guid}/arrangement")]
    public async Task<IActionResult> Arrange(
        Guid nightMarketId,
        Guid marketMapId,
        ArrangeMarketMapRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.ArrangeAsync(
            nightMarketId, marketMapId, request, ActorId, cancellationToken));

    [HttpPut("{marketMapId:guid}/default-layout")]
    public async Task<IActionResult> SetDefaultLayout(
        Guid nightMarketId,
        Guid marketMapId,
        SetMarketMapDefaultLayoutRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.SetDefaultLayoutAsync(
            nightMarketId, marketMapId, request, ActorId, cancellationToken));

    [HttpGet("{marketMapId:guid}/preview")]
    public async Task<IActionResult> Preview(
        Guid nightMarketId,
        Guid marketMapId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetPreviewAsync(
            nightMarketId, marketMapId, ActorId, cancellationToken));

    [HttpPost("{marketMapId:guid}/validate")]
    public async Task<IActionResult> Validate(
        Guid nightMarketId,
        Guid marketMapId,
        CancellationToken cancellationToken)
        => Ok(await _service.ValidateAsync(
            nightMarketId, marketMapId, ActorId, cancellationToken));

    [HttpPost("{marketMapId:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid nightMarketId,
        Guid marketMapId,
        CancellationToken cancellationToken)
        => Ok(await _service.ActivateAsync(
            nightMarketId, marketMapId, ActorId, cancellationToken));
}

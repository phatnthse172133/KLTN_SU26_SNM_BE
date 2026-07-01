using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.BoothLocations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class BoothLocationsController : ControllerBase
{
    private readonly IBoothLocationService _service;
    public BoothLocationsController(IBoothLocationService service)
    {
        _service = service;
    }

    [HttpGet("api/layouts/{layoutId:guid}/booth-locations")]
    public async Task<IActionResult> GetByLayout(Guid layoutId, [FromQuery] Guid? zoneId, [FromQuery] PaginationReq request, CancellationToken token)
        => Ok(await _service.GetByLayoutAsync(layoutId, zoneId, request, token));

    [HttpGet("api/layouts/{layoutId:guid}/available-booth-locations")]
    public async Task<IActionResult> Available(Guid layoutId, [FromQuery] Guid? zoneId, [FromQuery] PaginationReq request, CancellationToken token)
        => Ok(await _service.GetAvailableAsync(layoutId, zoneId, request, token));

    [HttpGet("api/layout-nodes/{nodeId:guid}/availability")]
    public async Task<IActionResult> Availability(Guid nodeId, CancellationToken token)
        => Ok(await _service.GetAvailabilityAsync(nodeId, token));

    [HttpGet("api/booths/{boothId:guid}/location")]
    public async Task<IActionResult> GetByBooth(Guid boothId, CancellationToken token)
        => Ok(await _service.GetByBoothAsync(boothId, token));

    [HttpPost("api/booths/{boothId:guid}/location")]
    public async Task<IActionResult> Assign(Guid boothId, AssignBoothLocationRequest request, CancellationToken token)
        => Ok(await _service.AssignAsync(boothId, request, token));

    [HttpPut("api/booths/{boothId:guid}/location")]
    public async Task<IActionResult> Move(Guid boothId, AssignBoothLocationRequest request, CancellationToken token)
        => Ok(await _service.MoveAsync(boothId, request, token));

    [HttpDelete("api/booths/{boothId:guid}/location")]
    public async Task<IActionResult> Release(Guid boothId, CancellationToken token)
        => Ok(await _service.ReleaseAsync(boothId, token));
}

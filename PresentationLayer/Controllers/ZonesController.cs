using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Zones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class ZonesController : ControllerBase
{
    private readonly IZoneService _service;
    public ZonesController(IZoneService service) => _service = service;

    [HttpGet("api/night-markets/{nightMarketId:guid}/zones")]
    public async Task<IActionResult> GetAll(
        Guid nightMarketId, [FromQuery] ZoneListRequest request,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetAllAsync(nightMarketId, request, cancellationToken));

    [HttpGet("api/zones/{zoneId:guid}")]
    public async Task<IActionResult> Get(Guid zoneId, CancellationToken cancellationToken)
        => Ok(await _service.GetByIdAsync(zoneId, cancellationToken));

    [HttpPost("api/night-markets/{nightMarketId:guid}/zones")]
    public async Task<IActionResult> Create(
        Guid nightMarketId, CreateZoneRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.CreateAsync(nightMarketId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { zoneId = response.Data!.Id }, response);
    }

    [HttpPut("api/zones/{zoneId:guid}")]
    public async Task<IActionResult> Update(
        Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(zoneId, request, cancellationToken));

    [HttpPatch("api/zones/{zoneId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid zoneId, UpdateZoneStatusRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateStatusAsync(zoneId, request, cancellationToken));

    [HttpDelete("api/zones/{zoneId:guid}")]
    public async Task<IActionResult> Delete(Guid zoneId, CancellationToken cancellationToken)
        => Ok(await _service.DeleteAsync(zoneId, cancellationToken));
}

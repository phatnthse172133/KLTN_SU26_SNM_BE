using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Zones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/admin/zones")]
[Authorize(Roles = "Admin")]
public class ZonesController : ControllerBase
{
    private readonly IZoneService _service;

    public ZonesController(IZoneService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationReq pagination, [FromQuery] Guid? nightMarketId, CancellationToken cancellationToken = default)
        => Ok(await _service.GetAllAsync(pagination, nightMarketId, cancellationToken));

    [HttpGet("{zoneId:guid}")]
    public async Task<IActionResult> Get(Guid zoneId, CancellationToken cancellationToken)
        => Ok(await _service.GetByIdAsync(zoneId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateZoneRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { zoneId = response.Data!.Id }, response);
    }

    [HttpPut("{zoneId:guid}")]
    public async Task<IActionResult> Update(Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(zoneId, request, cancellationToken));

    [HttpDelete("{zoneId:guid}")]
    public async Task<IActionResult> Delete(Guid zoneId, CancellationToken cancellationToken)
        => Ok(await _service.DeleteAsync(zoneId, cancellationToken));
}

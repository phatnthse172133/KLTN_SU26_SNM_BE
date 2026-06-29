using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.MarketLayouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class MarketLayoutsController : ControllerBase
{
    private readonly IMarketLayoutService _service;
    public MarketLayoutsController(IMarketLayoutService service) => _service = service;

    [HttpGet("api/night-markets/{nightMarketId:guid}/layouts")]
    public async Task<IActionResult> GetAll(
        Guid nightMarketId, [FromQuery] MarketLayoutListRequest request,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetAllAsync(nightMarketId, request, cancellationToken));

    [HttpGet("api/layouts/{layoutId:guid}")]
    public async Task<IActionResult> Get(Guid layoutId, CancellationToken cancellationToken)
        => Ok(await _service.GetByIdAsync(layoutId, cancellationToken));

    [HttpPost("api/night-markets/{nightMarketId:guid}/layouts")]
    public async Task<IActionResult> Create(
        Guid nightMarketId, CreateMarketLayoutRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.CreateAsync(nightMarketId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { layoutId = response.Data!.Id }, response);
    }

    [HttpPut("api/layouts/{layoutId:guid}")]
    public async Task<IActionResult> Update(
        Guid layoutId, UpdateMarketLayoutRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(layoutId, request, cancellationToken));

    [HttpPut("api/layouts/{layoutId:guid}/image")]
    public async Task<IActionResult> UpdateImage(
        Guid layoutId, [FromForm] UpdateMarketLayoutImageRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateImageAsync(layoutId, request, cancellationToken));

    [HttpGet("api/layouts/{layoutId:guid}/editor-data")]
    public async Task<IActionResult> GetEditorData(Guid layoutId, CancellationToken cancellationToken)
        => Ok(await _service.GetEditorDataAsync(layoutId, cancellationToken));

    [HttpPost("api/layouts/{layoutId:guid}/validate")]
    public async Task<IActionResult> Validate(Guid layoutId, CancellationToken cancellationToken)
        => Ok(await _service.ValidateAsync(layoutId, cancellationToken));

    [HttpPost("api/layouts/{layoutId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid layoutId, CancellationToken cancellationToken)
        => Ok(await _service.ActivateAsync(layoutId, cancellationToken));

    [HttpPost("api/layouts/{layoutId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid layoutId, CancellationToken cancellationToken)
        => Ok(await _service.DeactivateAsync(layoutId, cancellationToken));

    [HttpDelete("api/layouts/{layoutId:guid}")]
    public async Task<IActionResult> Delete(Guid layoutId, CancellationToken cancellationToken)
        => Ok(await _service.DeleteAsync(layoutId, cancellationToken));
}

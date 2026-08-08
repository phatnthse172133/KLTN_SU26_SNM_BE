using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using System.Security.Claims;
using ApplicationLayer.Services.MarketLayouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "Admin,MarketOwner")]
public class MarketLayoutsController : ControllerBase
{
    private readonly IMarketLayoutService _service;
    public MarketLayoutsController(IMarketLayoutService service)
    {
        _service = service;
    }

    private Guid? ActorId => User.IsInRole("Admin") ? null : Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("api/night-markets/{nightMarketId:guid}/layouts")]
    public async Task<IActionResult> GetAll(Guid nightMarketId, [FromQuery] MarketLayoutListRequest request, CancellationToken cancellationToken = default)
        => Ok(await _service.GetAllAsync(nightMarketId, request, cancellationToken, ActorId));

    [HttpGet("api/layouts/{layoutId:guid}")]
    public async Task<IActionResult> Get(Guid layoutId, CancellationToken cancellationToken)
        => Ok(await _service.GetByIdAsync(layoutId, cancellationToken, ActorId));

    [HttpPost("api/night-markets/{nightMarketId:guid}/layouts")]
    public async Task<IActionResult> Create(Guid nightMarketId, CreateMarketLayoutRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.CreateAsync(nightMarketId, request, cancellationToken, ActorId);
        return CreatedAtAction(nameof(Get), new { layoutId = response.Data!.Id }, response);
    }

    [HttpPut("api/layouts/{layoutId:guid}")]
    public async Task<IActionResult> Update( Guid layoutId, UpdateMarketLayoutRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(layoutId, request, cancellationToken, ActorId));

    [HttpPut("api/layouts/{layoutId:guid}/image")]
    public async Task<IActionResult> UpdateImage(Guid layoutId, [FromForm] UpdateMarketLayoutImageRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateImageAsync(layoutId, request, cancellationToken, ActorId));

    [HttpPatch("api/layouts/{layoutId:guid}/dimensions")]
    public async Task<IActionResult> UpdateDimensions(Guid layoutId, UpdateMarketLayoutDimensionsRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateDimensionsAsync(layoutId, request, cancellationToken, ActorId));

    [HttpPut("api/layouts/{layoutId:guid}/graph")]
    public async Task<IActionResult> SaveGraph(Guid layoutId, SaveGraphRequest request, CancellationToken cancellationToken)
        => Ok(await _service.SaveGraphTransactionalAsync(layoutId, request, cancellationToken, ActorId));

    [HttpGet("api/layouts/{layoutId:guid}/editor-data")]
    public async Task<IActionResult> GetEditorData(Guid layoutId, CancellationToken cancellationToken)
        => Ok(await _service.GetEditorDataAsync(layoutId, cancellationToken, ActorId));

    [HttpPost("api/layouts/{layoutId:guid}/generation-preview")]
    public async Task<IActionResult> GenerationPreview(Guid layoutId, GenerateLayoutRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GenerationPreviewAsync(layoutId, request, cancellationToken, ActorId));

    [HttpPost("api/layouts/{layoutId:guid}/apply-generation")]
    public async Task<IActionResult> ApplyGeneration(Guid layoutId, GenerateLayoutRequest request, CancellationToken cancellationToken)
        => Ok(await _service.ApplyGenerationAsync(layoutId, request, cancellationToken, ActorId));

    [HttpPost("api/layouts/{layoutId:guid}/validate")]
    public async Task<IActionResult> Validate(Guid layoutId, CancellationToken cancellationToken)
        => Ok(await _service.ValidateAsync(layoutId, cancellationToken, ActorId));

    [HttpPost("api/layouts/{layoutId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid layoutId, CancellationToken cancellationToken)
        => Ok(await _service.ActivateAsync(layoutId, cancellationToken, ActorId));

    [HttpPost("api/layouts/{layoutId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid layoutId, CancellationToken cancellationToken)
        => Ok(await _service.DeactivateAsync(layoutId, cancellationToken, ActorId));

    [HttpDelete("api/layouts/{layoutId:guid}")]
    public async Task<IActionResult> Delete(Guid layoutId, CancellationToken cancellationToken)
        => Ok(await _service.DeleteAsync(layoutId, cancellationToken, ActorId));
}

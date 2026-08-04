using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.LayoutEdges;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class LayoutEdgesController : ControllerBase
{
    private readonly ILayoutEdgeService _service;
    public LayoutEdgesController(ILayoutEdgeService service)
    {
        _service = service;
    }

    [HttpGet("api/layouts/{layoutId:guid}/edges")]
    public async Task<IActionResult> GetAll(Guid layoutId, [FromQuery] PaginationReq request, CancellationToken token)
        => Ok(await _service.GetAllAsync(layoutId, request, token));

    [HttpGet("api/layout-edges/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken token) => Ok(await _service.GetAsync(id, token));

    [HttpPost("api/layouts/{layoutId:guid}/edges")]
    public async Task<IActionResult> Create(Guid layoutId, CreateLayoutEdgeRequest request, CancellationToken token)
        => Ok(await _service.CreateAsync(layoutId, request, token));

    [HttpPost("api/layouts/{layoutId:guid}/edges/batch")]
    public async Task<IActionResult> CreateBatch(Guid layoutId, List<CreateLayoutEdgeRequest> request, CancellationToken token)
        => Ok(await _service.CreateBatchAsync(layoutId, request, token));

    [HttpPut("api/layout-edges/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateLayoutEdgeRequest request, CancellationToken token)
        => Ok(await _service.UpdateAsync(id, request, token));

    [HttpPatch("api/layout-edges/{id:guid}/accessibility")]
    public async Task<IActionResult> Accessibility(Guid id, UpdateAccessibilityRequest request, CancellationToken token)
        => Ok(await _service.UpdateAccessibilityAsync(id, request, token));

    [HttpDelete("api/layout-edges/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token) => Ok(await _service.DeleteAsync(id, token));
}

using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.LayoutNodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "Admin,MarketOwner")]
public class LayoutNodesController : ControllerBase
{
    private readonly ILayoutNodeService _service;
    public LayoutNodesController(ILayoutNodeService service)
    {
        _service = service;
    }

    private Guid? ActorId => User.IsInRole("Admin") ? null : Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("api/layouts/{layoutId:guid}/nodes")]
    public async Task<IActionResult> GetAll(Guid layoutId, [FromQuery] MapListRequest request, CancellationToken token)
        => Ok(await _service.GetAllAsync(layoutId, request, token, ActorId));

    [HttpGet("api/layout-nodes/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken token) => Ok(await _service.GetAsync(id, token, ActorId));

    [HttpPost("api/layouts/{layoutId:guid}/nodes")]
    public async Task<IActionResult> Create(Guid layoutId, CreateLayoutNodeRequest request, CancellationToken token)
        => Ok(await _service.CreateAsync(layoutId, request, token, ActorId));

    [HttpPost("api/layouts/{layoutId:guid}/nodes/batch")]
    public async Task<IActionResult> CreateBatch(Guid layoutId, List<CreateLayoutNodeRequest> request, CancellationToken token)
        => Ok(await _service.CreateBatchAsync(layoutId, request, token, ActorId));

    [HttpPut("api/layout-nodes/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateLayoutNodeRequest request, CancellationToken token)
        => Ok(await _service.UpdateAsync(id, request, token, ActorId));

    [HttpPatch("api/layout-nodes/{id:guid}/position")]
    public async Task<IActionResult> Position(Guid id, UpdateLayoutNodePositionRequest request, CancellationToken token)
        => Ok(await _service.UpdatePositionAsync(id, request, token, ActorId));

    [HttpPatch("api/layout-nodes/{id:guid}/accessibility")]
    public async Task<IActionResult> Accessibility(Guid id, UpdateAccessibilityRequest request, CancellationToken token)
        => Ok(await _service.UpdateAccessibilityAsync(id, request, token, ActorId));

    [HttpDelete("api/layout-nodes/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token) => Ok(await _service.DeleteAsync(id, token, ActorId));
}

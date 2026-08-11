using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.LayoutEdges;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "Admin,MarketOwner")]
public class LayoutEdgesController : ControllerBase
{
    private readonly ILayoutEdgeService _service;
    public LayoutEdgesController(ILayoutEdgeService service)
    {
        _service = service;
    }

    private Guid? ActorId => User.IsInRole("Admin") ? null : Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("api/layouts/{layoutId:guid}/edges")]
    public async Task<IActionResult> GetAll(Guid layoutId, [FromQuery] PaginationReq request, CancellationToken token)
        => Ok(await _service.GetAllAsync(layoutId, request, token, ActorId));

    [HttpGet("api/layout-edges/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken token) => Ok(await _service.GetAsync(id, token, ActorId));

    [HttpPost("api/layouts/{layoutId:guid}/edges")]
    public async Task<IActionResult> Create(Guid layoutId, CreateLayoutEdgeRequest request, CancellationToken token)
        => Ok(await _service.CreateAsync(layoutId, request, token, ActorId));

    [HttpPost("api/layouts/{layoutId:guid}/edges/batch")]
    public async Task<IActionResult> CreateBatch(Guid layoutId, List<CreateLayoutEdgeRequest> request, CancellationToken token)
        => Ok(await _service.CreateBatchAsync(layoutId, request, token, ActorId));

    [HttpPut("api/layout-edges/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateLayoutEdgeRequest request, CancellationToken token)
        => Ok(await _service.UpdateAsync(id, request, token, ActorId));

    [HttpPatch("api/layout-edges/{id:guid}/accessibility")]
    public async Task<IActionResult> Accessibility(Guid id, UpdateAccessibilityRequest request, CancellationToken token)
        => Ok(await _service.UpdateAccessibilityAsync(id, request, token, ActorId));

    [HttpDelete("api/layout-edges/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token) => Ok(await _service.DeleteAsync(id, token, ActorId));
}

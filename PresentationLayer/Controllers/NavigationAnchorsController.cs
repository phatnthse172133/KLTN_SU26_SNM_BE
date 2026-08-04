using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.NavigationAnchors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
public class NavigationAnchorsController : ControllerBase
{
    private readonly INavigationAnchorService _service;
    public NavigationAnchorsController(INavigationAnchorService service) => _service = service;

    [AllowAnonymous]
    [HttpGet("api/night-markets/{marketId:guid}/navigation-entrances")]
    public async Task<IActionResult> Get(Guid marketId, [FromQuery] Guid? targetBoothId,
        [FromQuery] int? expectedLayoutVersion, [FromQuery] int? expectedGraphRevision, CancellationToken token)
        => Ok(await _service.GetEntrancesAsync(marketId, targetBoothId, expectedLayoutVersion, expectedGraphRevision, token));

    [AllowAnonymous]
    [HttpGet("api/night-markets/{marketId:guid}/navigation-entrances/nearest")]
    public async Task<IActionResult> Nearest(Guid marketId, [FromQuery] double latitude, [FromQuery] double longitude,
        [FromQuery] Guid? targetBoothId, [FromQuery] int? expectedLayoutVersion,
        [FromQuery] int? expectedGraphRevision, CancellationToken token)
        => Ok(await _service.GetNearestEntrancesAsync(marketId, latitude, longitude, targetBoothId, expectedLayoutVersion, expectedGraphRevision, token));

    [Authorize(Roles = "Admin")]
    [HttpPost("api/layouts/{layoutId:guid}/navigation-anchors")]
    public async Task<IActionResult> Create(Guid layoutId, SaveNavigationAnchorRequest request, CancellationToken token)
        => Ok(await _service.CreateAsync(layoutId, request, token));

    [Authorize(Roles = "Admin")]
    [HttpPut("api/navigation-anchors/{anchorId:guid}")]
    public async Task<IActionResult> Update(Guid anchorId, SaveNavigationAnchorRequest request, CancellationToken token)
        => Ok(await _service.UpdateAsync(anchorId, request, token));

    [Authorize(Roles = "Admin")]
    [HttpDelete("api/navigation-anchors/{anchorId:guid}")]
    public async Task<IActionResult> Delete(Guid anchorId, CancellationToken token)
        => Ok(await _service.DeleteAsync(anchorId, token));
}

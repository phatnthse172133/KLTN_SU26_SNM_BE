using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.MapNavigation;
using ApplicationLayer.Services.IndoorPositioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[AllowAnonymous]
public class MapNavigationController : ControllerBase
{
    private readonly IMapNavigationService _service;
    private readonly IIndoorPositioningService _positioning;
    public MapNavigationController(IMapNavigationService service, IIndoorPositioningService positioning)
    {
        _service = service;
        _positioning = positioning;
    }

    [HttpGet("api/night-markets/{nightMarketId:guid}/map")]
    public async Task<IActionResult> GetMap(Guid nightMarketId, CancellationToken token)
        => Ok(await _service.GetMapAsync(nightMarketId, token));

    [HttpGet("api/layouts/{layoutId:guid}/starting-points")]
    public async Task<IActionResult> StartingPoints(Guid layoutId, [FromQuery] PaginationReq pagination, CancellationToken token)
        => Ok(await _service.GetStartingPointsAsync(layoutId, pagination, token));

    [HttpPost("api/layouts/{layoutId:guid}/nearest-node")]
    public async Task<IActionResult> Nearest(Guid layoutId, NearestNodeRequest request, CancellationToken token)
        => Ok(await _service.FindNearestNodeAsync(layoutId, request, token));

    [HttpPost("api/layouts/{layoutId:guid}/navigation/snap-position")]
    public async Task<IActionResult> Snap(Guid layoutId, SnapIndoorPositionRequest request, CancellationToken token)
        => Ok(await _positioning.SnapAsync(layoutId, request, token));

    [HttpPost("api/layouts/{layoutId:guid}/routes/from-snapped-position/to-booth")]
    public async Task<IActionResult> RouteFromSnappedPosition(Guid layoutId, RouteFromSnappedPositionRequest request, CancellationToken token)
        => Ok(await _positioning.RouteFromSnappedPositionAsync(layoutId, request, token));

    [HttpGet("api/layouts/{layoutId:guid}/routes/to-booth")]
    public async Task<IActionResult> Route(
        Guid layoutId, [FromQuery] Guid fromNodeId, [FromQuery] Guid boothId,
        [FromQuery] int? expectedLayoutVersion, [FromQuery] int? expectedGraphRevision,
        CancellationToken token)
        => Ok(await _service.FindRouteToBoothAsync(
            layoutId, fromNodeId, boothId, token, expectedLayoutVersion, expectedGraphRevision));
}

using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.NightMarkets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/night-markets")]
public class NightMarketsController : ControllerBase
{
    private readonly INightMarketService _service;

    public NightMarketsController(INightMarketService service)
    {
        _service = service;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentUserRole => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] NightMarketListRequest request, CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetAllAsync(request, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("options")]
    public async Task<IActionResult> GetOptions(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetOptionsAsync(cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var response = await _service.GetAsync(id, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/booths")]
    public async Task<IActionResult> GetBooths(
        Guid id,
        [FromQuery] PaginationReq pagination,
        CancellationToken cancellationToken)
        => Ok(await _service.GetBoothsAsync(id, pagination, cancellationToken));

    [AllowAnonymous]
    [HttpGet("{id:guid}/foods")]
    public async Task<IActionResult> GetFoods(
        Guid id,
        [FromQuery] PaginationReq pagination,
        CancellationToken cancellationToken)
        => Ok(await _service.GetFoodsAsync(id, pagination, cancellationToken));

    [Authorize(Roles = "MarketOwner")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetMineAsync(CurrentUserId, cancellationToken));
    }

    [Authorize(Roles = "Admin,MarketOwner")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateNightMarketRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.CreateAsync(request, CurrentUserId, cancellationToken);
        return response.Success
            ? StatusCode(StatusCodes.Status201Created, response)
            : BadRequest(response);
    }

    [Authorize(Roles = "Admin,MarketOwner")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateNightMarketRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpdateAsync(id, request, CurrentUserId, CurrentUserRole, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [Authorize(Roles = "Admin,MarketOwner")]
    [HttpPut("{id:guid}/geographic-location")]
    public async Task<IActionResult> UpdateGeographicLocation(Guid id, UpdateNightMarketGeographicLocationRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _service.UpdateGeographicLocationAsync(id, request, CurrentUserId, CurrentUserRole, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/navigation")]
    public async Task<IActionResult> GetNavigationInfo(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _service.GetNavigationInfoAsync(id, cancellationToken));
    }

    [Authorize(Roles = "Admin,MarketOwner")]
    [HttpGet("{id:guid}/deletion-impact")]
    public async Task<IActionResult> GetDeletionImpact(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _service.GetDeletionImpactAsync(id, CurrentUserId, CurrentUserRole, cancellationToken));
    }

    [Authorize(Roles = "Admin,MarketOwner")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _service.DeleteAsync(id, CurrentUserId, CurrentUserRole, cancellationToken));
    }
}

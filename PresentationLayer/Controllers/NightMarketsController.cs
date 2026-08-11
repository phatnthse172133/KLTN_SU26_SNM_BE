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
    private Guid? OptionalUserId => User.Identity?.IsAuthenticated == true
        && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private bool IsAdmin => User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] NightMarketListRequest request, CancellationToken cancellationToken = default)
    {
        return IsAdmin
            ? Ok(await _service.GetAllAsync(request, true, cancellationToken))
            : Ok(await _service.GetCustomerAllAsync(request, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("options")]
    public async Task<IActionResult> GetOptions(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetOptionsAsync(IsAdmin, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var canReadOwnedOrModeratedMarket = User.Identity?.IsAuthenticated == true
            && (User.IsInRole("Admin") || User.IsInRole("MarketOwner"));

        if (!canReadOwnedOrModeratedMarket)
            return Ok(await _service.GetCustomerAsync(id, cancellationToken));

        var response = await _service.GetAsync(id, OptionalUserId, CurrentUserRole, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

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
        return response.Success ? CreatedAtAction(nameof(Get), new { id = response.Data!.Id }, response) : BadRequest(response);
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
    [HttpGet("{id:guid}/navigation-info")]
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

    [Authorize(Roles = "MarketOwner")]
    [HttpGet("{id:guid}/activation-readiness")]
    public async Task<IActionResult> GetActivationReadiness(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _service.GetActivationReadinessAsync(
            id, CurrentUserId, CurrentUserRole, cancellationToken));
    }

    [Authorize(Roles = "MarketOwner")]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> PatchStatus(Guid id, [FromBody] PatchNightMarketStatusRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _service.PatchStatusAsync(id, request, CurrentUserId, CurrentUserRole, cancellationToken));
    }

    [Authorize(Roles = "MarketOwner,Admin")]
    [HttpGet("/api/market-owner/night-markets/{marketId:guid}/images")]
    [HttpGet("{marketId:guid}/images")]
    public async Task<IActionResult> GetImages(Guid marketId, CancellationToken cancellationToken)
    {
        return Ok(await _service.GetImagesAsync(marketId, CurrentUserId, CurrentUserRole, cancellationToken));
    }

    [Authorize(Roles = "MarketOwner,Admin")]
    [HttpPost("/api/market-owner/night-markets/{marketId:guid}/images")]
    [HttpPost("{marketId:guid}/images")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(Guid marketId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Failure("Image file is required.", "IMAGE_FILE_REQUIRED"));

        await using var stream = file.OpenReadStream();
        return Ok(await _service.UploadImageAsync(marketId, stream, file.FileName, file.ContentType, file.Length, CurrentUserId, CurrentUserRole, cancellationToken));
    }

    [Authorize(Roles = "MarketOwner,Admin")]
    [HttpDelete("/api/market-owner/night-markets/{marketId:guid}/images/{imageId:guid}")]
    [HttpDelete("{marketId:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid marketId, Guid imageId, CancellationToken cancellationToken)
    {
        return Ok(await _service.DeleteImageAsync(marketId, imageId, CurrentUserId, CurrentUserRole, cancellationToken));
    }

    [Authorize(Roles = "MarketOwner,Admin")]
    [HttpPatch("/api/market-owner/night-markets/{marketId:guid}/images/{imageId:guid}/cover")]
    [HttpPatch("{marketId:guid}/images/{imageId:guid}/cover")]
    public async Task<IActionResult> SetCoverImage(Guid marketId, Guid imageId, CancellationToken cancellationToken)
    {
        return Ok(await _service.SetCoverImageAsync(marketId, imageId, CurrentUserId, CurrentUserRole, cancellationToken));
    }
}

using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize]
[Route("api/device-tokens")]
public class DeviceTokensController : ControllerBase
{
    private readonly IDeviceTokenService _service;

    public DeviceTokensController(IDeviceTokenService service)
    {
        _service = service;
    }

    private Guid CurrentUserId
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Register(
        RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _service.RegisterAsync(
            CurrentUserId,
            request,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpDelete]
    public async Task<IActionResult> Remove(
        [FromQuery] string? token,
        [FromQuery] string? deviceId,
        CancellationToken cancellationToken)
    {
        await _service.RemoveAsync(
            CurrentUserId,
            token,
            deviceId,
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("all")]
    public async Task<IActionResult> RemoveAll(
        CancellationToken cancellationToken)
    {
        await _service.RemoveAllAsync(CurrentUserId, cancellationToken);
        return NoContent();
    }
}

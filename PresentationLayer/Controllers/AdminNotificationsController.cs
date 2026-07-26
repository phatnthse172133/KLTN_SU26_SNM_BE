using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/notifications")]
public class AdminNotificationsController : ControllerBase
{
    private readonly INotificationService _service;

    public AdminNotificationsController(INotificationService service)
    {
        _service = service;
    }

    private Guid CurrentUserId
        => Guid.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] AdminNotificationListRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.GetAdminNotificationsAsync(
            request,
            cancellationToken));

    [HttpGet("{batchId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid batchId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetAdminNotificationDetailAsync(
            batchId,
            cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(
        AdminCreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _service.CreateByAdminAsync(
            CurrentUserId,
            request,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpDelete("{batchId:guid}")]
    public IActionResult Delete(Guid batchId)
    {
        return StatusCode(StatusCodes.Status405MethodNotAllowed, new
        {
            Success = false,
            Message = "Notifications cannot be deleted.",
            ErrorCode = "NOTIFICATION_DELETE_NOT_ALLOWED"
        });
    }
}

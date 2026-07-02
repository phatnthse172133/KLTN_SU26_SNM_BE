using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service)
    {
        _service = service;
    }

    private Guid CurrentUserId
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] NotificationListRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.GetAsync(
            CurrentUserId,
            request,
            cancellationToken));

    [HttpGet("{notificationId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid notificationId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetDetailAsync(
            CurrentUserId,
            notificationId,
            cancellationToken));

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(
        CancellationToken cancellationToken)
        => Ok(await _service.GetUnreadCountAsync(
            CurrentUserId,
            cancellationToken));

    [HttpPatch("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid notificationId,
        CancellationToken cancellationToken)
        => Ok(await _service.MarkReadAsync(
            CurrentUserId,
            notificationId,
            cancellationToken));

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(
        CancellationToken cancellationToken)
        => Ok(await _service.MarkAllReadAsync(
            CurrentUserId,
            cancellationToken));

    [HttpDelete("{notificationId:guid}")]
    public async Task<IActionResult> Delete(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(
            CurrentUserId,
            notificationId,
            cancellationToken);
        return NoContent();
    }
}

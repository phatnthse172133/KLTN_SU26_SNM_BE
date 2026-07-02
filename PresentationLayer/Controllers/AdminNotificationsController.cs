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

    [HttpPost]
    public async Task<IActionResult> Create(
        AdminCreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _service.CreateByAdminAsync(
            request,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}

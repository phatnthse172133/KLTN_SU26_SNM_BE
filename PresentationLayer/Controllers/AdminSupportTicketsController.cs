using System.Security.Claims;
using ApplicationLayer.DTOs;
using ApplicationLayer.Services.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/support/tickets")]
public class AdminSupportTicketsController : ControllerBase
{
    private readonly ISupportTicketService _service;
    public AdminSupportTicketsController(ISupportTicketService service) => _service = service;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] SupportTicketQuery query, CancellationToken ct)
        => Ok(await _service.GetAdminAsync(query, ct));

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(CancellationToken ct)
        => Ok(await _service.GetMetricsAsync(ct));

    [HttpGet("{ticketId:guid}")]
    public async Task<IActionResult> GetDetail(Guid ticketId, CancellationToken ct)
        => Ok(await _service.GetAdminDetailAsync(ticketId, ct));

    [HttpPatch("{ticketId:guid}")]
    public async Task<IActionResult> Update(Guid ticketId, UpdateSupportTicketRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(CurrentUserId, ticketId, request, ct));

    [HttpPost("{ticketId:guid}/messages")]
    public async Task<IActionResult> Reply(Guid ticketId, SendSupportMessageRequest request, CancellationToken ct)
        => Ok(await _service.ReplyAsync(CurrentUserId, "Admin", ticketId, request, true, ct));

    [HttpPost("{ticketId:guid}/attachments")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> AddAttachment(Guid ticketId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("Select an evidence file.");
        await using var stream = file.OpenReadStream();
        return Ok(await _service.AddAttachmentAsync(CurrentUserId, ticketId, stream, file.FileName, file.ContentType, file.Length, true, ct));
    }
}

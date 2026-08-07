using System.Security.Claims;
using ApplicationLayer.DTOs;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "BoothOwner,MarketOwner")]
[Route("api/support/tickets")]
public class SupportTicketsController : ControllerBase
{
    private readonly ISupportTicketService _service;
    public SupportTicketsController(ISupportTicketService service) => _service = service;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? "User";

    [HttpPost]
    public async Task<IActionResult> Create(CreateSupportTicketRequest request, CancellationToken ct)
        => Ok(await _service.CreateAsync(CurrentUserId, CurrentRole, request, ct));

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] PaginationReq query, CancellationToken ct)
        => Ok(await _service.GetMineAsync(CurrentUserId, query, ct));

    [HttpGet("mine/{ticketId:guid}")]
    public async Task<IActionResult> GetDetail(Guid ticketId, CancellationToken ct)
        => Ok(await _service.GetMineDetailAsync(CurrentUserId, ticketId, ct));

    [HttpPost("mine/{ticketId:guid}/messages")]
    public async Task<IActionResult> Reply(Guid ticketId, SendSupportMessageRequest request, CancellationToken ct)
        => Ok(await _service.ReplyAsync(CurrentUserId, CurrentRole, ticketId, request, false, ct));

    [HttpPost("mine/{ticketId:guid}/attachments")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> AddAttachment(Guid ticketId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("Select an evidence file.");
        await using var stream = file.OpenReadStream();
        return Ok(await _service.AddAttachmentAsync(CurrentUserId, ticketId, stream, file.FileName, file.ContentType, file.Length, false, ct));
    }
}

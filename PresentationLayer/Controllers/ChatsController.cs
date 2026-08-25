using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Chats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "Customer,BoothOwner")]
[Route("api/chats")]
public class ChatsController : ControllerBase
{
    private readonly IChatService _service;

    public ChatsController(IChatService service)
    {
        _service = service;
    }

    private Guid CurrentUserId
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [Authorize(Roles = "Customer")]
    [HttpPost("customer-booth")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> CreateCustomerBoothConversation(
        [FromBody] CreateCustomerBoothConversationRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.CreateCustomerBoothConversationAsync(
            CurrentUserId,
            request,
            cancellationToken));

    [HttpGet]
    public async Task<IActionResult> GetConversations(
        [FromQuery] ChatListRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.GetConversationsAsync(
            CurrentUserId,
            request,
            cancellationToken));

    [HttpGet("{conversationId:guid}")]
    public async Task<IActionResult> GetConversation(
        Guid conversationId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetConversationAsync(
            CurrentUserId,
            conversationId,
            cancellationToken));

    [HttpGet("{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid conversationId,
        [FromQuery] MessageListRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.GetMessagesAsync(
            CurrentUserId,
            conversationId,
            request,
            cancellationToken));

    [HttpPost("{conversationId:guid}/messages")]
    [EnableRateLimiting("ChatSendPolicy")]
    public async Task<IActionResult> SendMessage(
        Guid conversationId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.SendMessageAsync(
            CurrentUserId,
            conversationId,
            request,
            cancellationToken));

    [HttpPost("{conversationId:guid}/messages/attachment")]
    [EnableRateLimiting("ChatSendPolicy")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> SendAttachmentMessage(
        Guid conversationId,
        IFormFile file,
        [FromForm] string? content,
        [FromForm] Guid? clientMessageId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { success = false, message = "Attachment file is required.", errorCode = "CHAT_ATTACHMENT_REQUIRED" });

        await using var stream = file.OpenReadStream();
        return Ok(await _service.SendAttachmentMessageAsync(
            CurrentUserId,
            conversationId,
            stream,
            file.FileName,
            file.ContentType ?? string.Empty,
            file.Length,
            content,
            clientMessageId,
            cancellationToken));
    }

    [HttpPatch("{conversationId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid conversationId,
        CancellationToken cancellationToken)
        => Ok(await _service.MarkReadAsync(
            CurrentUserId,
            conversationId,
            cancellationToken));

    [HttpDelete("messages/{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await _service.DeleteMessageAsync(
            CurrentUserId,
            messageId,
            cancellationToken);
        return NoContent();
    }
}

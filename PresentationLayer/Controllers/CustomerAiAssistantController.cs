using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Assistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/customer/ai/conversations")]
[Authorize(Roles = "Customer")]
[EnableRateLimiting("AssistantApiPolicy")]
public sealed class CustomerAiAssistantController(IAssistantService service) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(CreateAssistantConversationRequest request, CancellationToken ct)
        => Ok(await service.CreateConversationAsync(CurrentUserId, request, ct));

    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Send(Guid id, SendAssistantMessageRequest request, CancellationToken ct)
        => Ok(await service.SendMessageAsync(CurrentUserId, id, request, ct));

    [HttpPost("{id:guid}/meal-plans/{planId:guid}/add-to-cart")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMealPlanToCart(Guid id, Guid planId, CancellationToken ct)
        => Ok(await service.AddMealPlanToCartAsync(CurrentUserId, id, planId, ct));
}

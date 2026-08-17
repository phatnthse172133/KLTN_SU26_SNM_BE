using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
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
    [ProducesResponseType(typeof(ApiResponse<CreateAssistantConversationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(CreateAssistantConversationRequest request, CancellationToken ct)
        => Ok(await service.CreateConversationAsync(CurrentUserId, request, ct));

    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType(typeof(ApiResponse<AssistantTurnResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Send(Guid id, SendAssistantMessageRequest request, CancellationToken ct)
        => Ok(await service.SendMessageAsync(CurrentUserId, id, request, ct));

    [HttpPost("{id:guid}/meal-plans/{planId:guid}/add-to-cart")]
    [ProducesResponseType(typeof(ApiResponse<CartBatchAddResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMealPlanToCart(Guid id, Guid planId, CancellationToken ct)
        => Ok(await service.AddMealPlanToCartAsync(CurrentUserId, id, planId, ct));

    [HttpPatch("{id:guid}/meal-plans/{planId:guid}/items/{itemId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AssistantMealPlanResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMealPlanItemQuantity(
        Guid id,
        Guid planId,
        Guid itemId,
        UpdateAssistantMealPlanItemQuantityRequest request,
        CancellationToken ct)
        => Ok(await service.UpdateMealPlanItemQuantityAsync(CurrentUserId, id, planId, itemId, request, ct));

    [HttpDelete("{id:guid}/meal-plans/{planId:guid}/items/{itemId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AssistantMealPlanResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMealPlanItem(Guid id, Guid planId, Guid itemId, CancellationToken ct)
        => Ok(await service.RemoveMealPlanItemAsync(CurrentUserId, id, planId, itemId, ct));

    [HttpPost("{id:guid}/meal-plans/{planId:guid}/items/{itemId:guid}/replacements")]
    [ProducesResponseType(typeof(ApiResponse<AssistantMealPlanReplacementsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetMealPlanItemReplacements(Guid id, Guid planId, Guid itemId, CancellationToken ct)
        => Ok(await service.GetMealPlanItemReplacementsAsync(CurrentUserId, id, planId, itemId, ct));

    [HttpPost("{id:guid}/meal-plans/{planId:guid}/items/{itemId:guid}/replace")]
    [ProducesResponseType(typeof(ApiResponse<AssistantMealPlanResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ReplaceMealPlanItem(
        Guid id,
        Guid planId,
        Guid itemId,
        ReplaceAssistantMealPlanItemRequest request,
        CancellationToken ct)
        => Ok(await service.ReplaceMealPlanItemAsync(CurrentUserId, id, planId, itemId, request, ct));
}

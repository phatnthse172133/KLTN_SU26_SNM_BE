using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Menus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/booths/mine/{boothId:guid}/menu")]
[Authorize(Roles = "BoothOwner")]
public class MenuController : ControllerBase
{
    private readonly IMenuService _service;

    public MenuController(IMenuService service)
    {
        _service = service;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetMyBoothMenu(Guid boothId, [FromQuery] PaginationReq pagination, CancellationToken cancellationToken)
        => Ok(await _service.GetMyBoothMenuAsync(
            CurrentUserId, boothId, pagination, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CreateFoodItem(Guid boothId, CreateFoodItemRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.CreateFoodItemAsync(CurrentUserId, boothId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("v2")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateFoodItemV2(Guid boothId, CreateFoodItemV2Request request, CancellationToken cancellationToken)
        => Ok(await _service.CreateFoodItemV2Async(CurrentUserId, boothId, request, cancellationToken));

    [HttpPut("{foodItemId:guid}")]
    public async Task<IActionResult> UpdateFoodItem(Guid boothId, Guid foodItemId, UpdateFoodItemRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpdateFoodItemAsync(CurrentUserId, boothId, foodItemId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPut("v2/{foodItemId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateFoodItemV2(Guid boothId, Guid foodItemId, UpdateFoodItemV2Request request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateFoodItemV2Async(CurrentUserId, boothId, foodItemId, request, cancellationToken));

    [HttpPatch("{foodItemId:guid}/availability")]
    public async Task<IActionResult> UpdateAvailability(Guid boothId, Guid foodItemId, UpdateFoodAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpdateAvailabilityAsync(CurrentUserId, boothId, foodItemId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPatch("{foodItemId:guid}/featured")]
    public async Task<IActionResult> UpdateFeatured(Guid boothId, Guid foodItemId, UpdateFoodFeaturedRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpdateFeaturedAsync(CurrentUserId, boothId, foodItemId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpDelete("{foodItemId:guid}")]
    public async Task<IActionResult> DeleteFoodItem(Guid boothId, Guid foodItemId, CancellationToken cancellationToken)
    {
        var response = await _service.DeleteFoodItemAsync(CurrentUserId, boothId, foodItemId, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}

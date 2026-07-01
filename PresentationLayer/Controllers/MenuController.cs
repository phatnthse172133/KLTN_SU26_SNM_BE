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

    [HttpPut("{foodItemId:guid}")]
    public async Task<IActionResult> UpdateFoodItem(Guid boothId, Guid foodItemId, UpdateFoodItemRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpdateFoodItemAsync(CurrentUserId, boothId, foodItemId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

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

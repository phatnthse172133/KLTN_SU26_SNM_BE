using System.Security.Claims;
using ApplicationLayer.AI.DTOs;
using ApplicationLayer.AI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/food-tags")]
public class FoodTagsController : ControllerBase
{
    private readonly IFoodTagService _service;

    public FoodTagsController(IFoodTagService service)
    {
        _service = service;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get([FromQuery] FoodTagQueryRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetAsync(request, cancellationToken));

    [HttpPut("/api/food-items/{foodItemId:guid}/tags")]
    [Authorize(Roles = "Admin,BoothOwner")]
    public async Task<IActionResult> UpdateFoodItemTags(
        Guid foodItemId,
        UpdateFoodItemTagsRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.UpdateFoodItemTagsAsync(
            CurrentUserId,
            foodItemId,
            request,
            User.IsInRole("Admin"),
            cancellationToken));
}

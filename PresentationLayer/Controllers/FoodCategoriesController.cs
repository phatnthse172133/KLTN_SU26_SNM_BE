using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.FoodCategories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/booths/mine/{boothId:guid}/food-categories")]
[Authorize(Roles = "BoothOwner")]
public class FoodCategoriesController : ControllerBase
{
    private readonly IFoodCategoryService _service;

    public FoodCategoriesController(IFoodCategoryService service)
    {
        _service = service;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid boothId, [FromQuery] PaginationReq pagination, CancellationToken cancellationToken = default)
        => Ok(await _service.GetMyBoothCategoriesAsync(CurrentUserId, boothId, pagination, cancellationToken));

    [HttpGet("{categoryId:guid}")]
    public async Task<IActionResult> Get(Guid boothId, Guid categoryId, CancellationToken cancellationToken)
    {
        var response = await _service.GetMyBoothCategoryAsync(CurrentUserId, boothId, categoryId, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

}

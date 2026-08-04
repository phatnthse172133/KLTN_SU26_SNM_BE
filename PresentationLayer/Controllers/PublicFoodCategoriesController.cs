using ApplicationLayer.Helppers;
using ApplicationLayer.Services.FoodCategories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/food-categories")]
[AllowAnonymous]
public sealed class PublicFoodCategoriesController : ControllerBase
{
    private readonly IFoodCategoryService _service;

    public PublicFoodCategoriesController(IFoodCategoryService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] PaginationReq pagination, CancellationToken cancellationToken)
        => Ok(await _service.GetSelectableAsync(pagination, cancellationToken));
}

using ApplicationLayer.Services.Menus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/food-metadata/catalogs")]
[AllowAnonymous]
public sealed class FoodMetadataCatalogController(IFoodMetadataCatalogService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await service.GetActiveAsync(ct));
}

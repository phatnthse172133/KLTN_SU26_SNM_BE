using System.Security.Claims;
using ApplicationLayer.AI.DTOs;
using ApplicationLayer.AI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/customer/food-profile")]
[Authorize(Roles = "Customer")]
public sealed class CustomerFoodProfileController(ICustomerFoodProfileService service) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await service.GetMineAsync(CurrentUserId, ct));

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Put(UpdateCustomerFoodProfileRequest request, CancellationToken ct) => Ok(await service.UpdateMineAsync(CurrentUserId, request, ct));
}

using System.Security.Claims;
using ApplicationLayer.Services.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/customer/checkout")]
public sealed class CustomerCheckoutController : ControllerBase
{
    private readonly ICustomerCheckoutService _checkout;
    public CustomerCheckoutController(ICustomerCheckoutService checkout) => _checkout = checkout;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] Guid? promotionId, CancellationToken cancellationToken)
    {
        var result = await _checkout.GetPreviewAsync(CurrentUserId, promotionId, cancellationToken);
        return Ok(new
        {
            Success = true,
            Code = "SUCCESS",
            Message = "Success",
            Data = result
        });
    }
        //=> Ok(await _checkout.GetPreviewAsync(CurrentUserId, promotionId, cancellationToken));
}

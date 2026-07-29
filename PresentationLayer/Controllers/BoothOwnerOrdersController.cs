using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "BoothOwner")]
[Route("api/booth-owner/orders")]
public sealed class BoothOwnerOrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    public BoothOwnerOrdersController(IOrderService orders) => _orders = orders;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPut("{orderCode:long}/status")]
    public async Task<IActionResult> UpdateStatus(long orderCode, [FromBody] UpdateBoothOwnerOrderStatusRequest request,
        CancellationToken cancellationToken)
        => Ok(await _orders.UpdateBoothOwnerOrderStatusAsync(CurrentUserId, orderCode, request, cancellationToken));
}

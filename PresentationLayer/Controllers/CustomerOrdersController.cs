using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/customer/orders")]
public sealed class CustomerOrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public CustomerOrdersController(IOrderService orders) => _orders = orders;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetHistory([FromQuery] CustomerOrderHistoryRequest request, CancellationToken cancellationToken)
        => Ok(await _orders.GetCustomerHistoryAsync(CurrentUserId, request, cancellationToken));

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetDetail(Guid orderId, CancellationToken cancellationToken)
        => Ok(await _orders.GetCustomerDetailAsync(CurrentUserId, orderId, cancellationToken));
}

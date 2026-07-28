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
    private readonly ICustomerCheckoutService _checkout;

    public CustomerOrdersController(IOrderService orders, ICustomerCheckoutService checkout)
        => (_orders, _checkout) = (orders, checkout);

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetHistory([FromQuery] CustomerOrderHistoryRequest request, CancellationToken cancellationToken)
        => Ok(await _orders.GetCustomerHistoryAsync(CurrentUserId, request, cancellationToken));

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetDetail(Guid orderId, CancellationToken cancellationToken)
        => Ok(await _orders.GetCustomerDetailAsync(CurrentUserId, orderId, cancellationToken));

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutCartBoothRequest request,
        CancellationToken cancellationToken)
        => Ok(await _checkout.CheckoutBoothAsync(CurrentUserId, request, cancellationToken));

    [HttpPost("{orderId:guid}/payment/reconcile")]
    public async Task<IActionResult> ReconcilePayment(Guid orderId, CancellationToken cancellationToken)
    {
        var ownedOrder = await _orders.GetCustomerDetailAsync(CurrentUserId, orderId, cancellationToken);
        await _orders.ActiveCheckPaymentStatus(ownedOrder.Data!.OrderCode);
        return Ok(await _orders.GetCustomerDetailAsync(CurrentUserId, orderId, cancellationToken));
    }
}

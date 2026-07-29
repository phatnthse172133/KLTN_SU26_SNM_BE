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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerOrderRequest request, CancellationToken cancellationToken)
        => Ok(await _checkout.CreateOrderAsync(CurrentUserId, request, Request.Headers["Idempotency-Key"].FirstOrDefault(), cancellationToken));

    [HttpGet("{orderId:guid}/payment-status")]
    public async Task<IActionResult> GetPaymentStatus(Guid orderId, CancellationToken cancellationToken)
        => Ok(await _orders.GetPaymentStatusAsync(CurrentUserId, orderId, cancellationToken));

    [HttpPost("{orderId:guid}/payments/reconcile")]
    public async Task<IActionResult> ReconcilePayment(Guid orderId, CancellationToken cancellationToken)
        => Ok(await _orders.ReconcileCustomerPaymentAsync(CurrentUserId, orderId, cancellationToken));

    [HttpPost("{orderId:guid}/payments/retry")]
    public async Task<IActionResult> RetryPayment(Guid orderId, CancellationToken cancellationToken)
        => Ok(await _orders.RetryPaymentAsync(CurrentUserId, orderId, cancellationToken));

    [HttpPost("{orderId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid orderId, [FromBody] CancelCustomerOrderRequest? request, CancellationToken cancellationToken)
        => Ok(await _orders.CancelCustomerOrderAsync(CurrentUserId, orderId, request?.Reason, cancellationToken));
}

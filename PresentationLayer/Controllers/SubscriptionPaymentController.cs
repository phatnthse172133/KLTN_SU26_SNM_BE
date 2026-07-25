using ApplicationLayer.DTOs.Subscriptions;
using ApplicationLayer.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PresentationLayer.Controllers
{
    [ApiController]
    [Route("api/subscriptions")]
    [Authorize]
    public class SubscriptionPaymentController : ControllerBase
    {
        private readonly IOwnerSubscriptionService _service;

        public SubscriptionPaymentController(IOwnerSubscriptionService service)
        {
            _service = service;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("{subscriptionId}/payment-status")]
        public async Task<IActionResult> GetPaymentStatus(Guid subscriptionId, CancellationToken cancellationToken)
            => Ok(await _service.GetPaymentStatusAsync(CurrentUserId, subscriptionId, cancellationToken));

        [HttpPost("{subscriptionId}/cancel-payment")]
        public async Task<IActionResult> CancelPayment(Guid subscriptionId, CancellationToken cancellationToken)
            => Ok(await _service.CancelPaymentAsync(CurrentUserId, subscriptionId, cancellationToken));
    }
}

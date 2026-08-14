using ApplicationLayer.DTOs.Subscriptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PresentationLayer.Controllers
{
    [ApiController]
    [Route("api/booths/{boothId}/subscriptions")]
    [Authorize(Roles = "BoothOwner")]
    public class BoothSubscriptionController : ControllerBase
    {
        private readonly IOwnerSubscriptionService _service;

        public BoothSubscriptionController(IOwnerSubscriptionService service)
        {
            _service = service;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent(Guid boothId, CancellationToken cancellationToken)
            => Ok(await _service.GetBoothCurrentAsync(CurrentUserId, boothId, cancellationToken));

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(Guid boothId, CancellationToken cancellationToken)
            => Ok(await _service.GetBoothHistoryAsync(CurrentUserId, boothId, cancellationToken));

        [HttpPost("quote")]
        public async Task<IActionResult> Quote(Guid boothId, [FromBody] SubscriptionQuoteRequest request, CancellationToken cancellationToken)
            => Ok(await _service.QuoteBoothAsync(CurrentUserId, boothId, request, cancellationToken));

        [HttpPost("purchase")]
        public async Task<IActionResult> Purchase(Guid boothId, [FromBody] PurchaseSubscriptionRequest request, CancellationToken cancellationToken)
            => Ok(await _service.PurchaseBoothAsync(CurrentUserId, boothId, request, cancellationToken));

        [HttpPost("renew")]
        public async Task<IActionResult> Renew(Guid boothId, [FromBody] RenewSubscriptionRequest request, CancellationToken cancellationToken)
            => Ok(await _service.RenewBoothAsync(CurrentUserId, boothId, request, cancellationToken));
    }
}

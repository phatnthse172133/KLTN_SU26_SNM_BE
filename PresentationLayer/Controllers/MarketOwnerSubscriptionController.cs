using ApplicationLayer.DTOs.Subscriptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace PresentationLayer.Controllers
{
    [ApiController]
    [Route("api/market-owner/subscriptions")]
    [Authorize(Roles = "MarketOwner")]
    public class MarketOwnerSubscriptionController : ControllerBase
    {
        private readonly IOwnerSubscriptionService _service;

        public MarketOwnerSubscriptionController(IOwnerSubscriptionService service)
        {
            _service = service;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
            => Ok(await _service.GetMarketCurrentAsync(CurrentUserId, cancellationToken));

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
            => Ok(await _service.GetMarketHistoryAsync(CurrentUserId, cancellationToken));

        [HttpPost("quote")]
        public async Task<IActionResult> Quote([FromBody] SubscriptionQuoteRequest request, CancellationToken cancellationToken)
            => Ok(await _service.QuoteMarketAsync(CurrentUserId, request, cancellationToken));

        [HttpPost("purchase")]
        public async Task<IActionResult> Purchase([FromBody] PurchaseSubscriptionRequest request, CancellationToken cancellationToken)
            => Ok(await _service.PurchaseMarketAsync(CurrentUserId, request, cancellationToken));

        [HttpPost("renew")]
        public async Task<IActionResult> Renew([FromBody] RenewSubscriptionRequest request, CancellationToken cancellationToken)
            => Ok(await _service.RenewMarketAsync(CurrentUserId, request, cancellationToken));
    }
}

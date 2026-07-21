using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Threading;
using static DomainLayer.Enums.GeneralEnum;

namespace PresentationLayer.Controllers
{
    [Route("api/admin/subscriptions")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminSubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;

        public AdminSubscriptionController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        [HttpGet]
        public async Task<ActionResult<PaginationResp<AdminSubscriptionDto>>> GetSubscriptions(
            [FromQuery] PackageType? type,
            [FromQuery] SubscriptionStatus? status,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var result = await _subscriptionService.GetSubscriptionsAsync(type, status, pageIndex, pageSize, cancellationToken);
            return Ok(result);
        }

        [HttpPut("{id}/verify")]
        [Obsolete("Manual subscription verification is disabled. Payments are now processed automatically via PayOS webhook.")]
        public IActionResult VerifySubscription(Guid id, [FromQuery] PackageType type, [FromBody] VerifySubscriptionRequest request)
        {
            return StatusCode(410, new { Message = "Manual subscription verification is no longer available. Payments are processed automatically via PayOS." });
        }
    }
}

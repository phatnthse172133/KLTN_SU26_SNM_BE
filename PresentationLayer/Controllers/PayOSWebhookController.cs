using ApplicationLayer.Services.PayOS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PayOS.Models.Webhooks;
using System.Threading.Tasks;

namespace PresentationLayer.Controllers
{
    [ApiController]
    [Route("api/Webhook/payos")]
    public class PayOSWebhookController : ControllerBase
    {
        private readonly IPayOSWebhookDispatcher _dispatcher;
        private readonly ILogger<PayOSWebhookController> _logger;

        public PayOSWebhookController(IPayOSWebhookDispatcher dispatcher, ILogger<PayOSWebhookController> logger)
        {
            _dispatcher = dispatcher;
            _logger = logger;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook([FromBody] Webhook body)
        {
            var result = await _dispatcher.DispatchAsync(body);

            switch (result)
            {
                case WebhookDispatchResult.InvalidSignature:
                    _logger.LogWarning("PayOS webhook: invalid signature.");
                    return BadRequest(new { error = -1, message = "Invalid webhook signature." });

                case WebhookDispatchResult.NotFound:
                    _logger.LogWarning("PayOS webhook: order code not found or unknown prefix.");
                    // A link can be created immediately before its local transaction
                    // commits. Non-2xx asks PayOS to retry instead of losing that event.
                    return NotFound(new { error = -1, message = "Matching payment is not available yet." });

                case WebhookDispatchResult.NotSuccessful:
                    _logger.LogInformation("PayOS webhook: payment not successful.");
                    return Ok(new { error = 0, message = "Webhook acknowledged but payment not successful." });

                case WebhookDispatchResult.AlreadyProcessed:
                    _logger.LogInformation("PayOS webhook: already processed, skipping.");
                    return Ok(new { error = 0, message = "Webhook already processed." });

                case WebhookDispatchResult.Conflict:
                    _logger.LogCritical("PayOS webhook: conflict -- order code {OrderCode} exists in multiple domains. Manual resolution required.", body?.Data?.OrderCode);
                    return Ok(new { error = 0, message = "Webhook acknowledged. Conflict detected -- manual resolution required." });

                case WebhookDispatchResult.SubscriptionHandled:
                case WebhookDispatchResult.OrderHandled:
                    _logger.LogInformation("PayOS webhook: handled successfully ({Result}).", result);
                    return Ok(new { error = 0, message = "Webhook handled successfully." });

                default:
                    _logger.LogWarning("PayOS webhook: unknown result {Result}.", result);
                    return Ok(new { error = 0, message = "Webhook acknowledged." });
            }
        }
    }
}

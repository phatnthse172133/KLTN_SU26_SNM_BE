using ApplicationLayer.Services.PayOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PayOS.Models.Webhooks;
using System;
using System.Threading.Tasks;

namespace PresentationLayer.Controllers
{
    [Route("api/webhook")]
    [ApiController]
    [Obsolete("Use /api/webhooks/payos instead. This endpoint is kept for backward compatibility.")]
    public class WebhookController : ControllerBase
    {
        private readonly IPayOSWebhookDispatcher _dispatcher;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(IPayOSWebhookDispatcher dispatcher, ILogger<WebhookController> logger)
        {
            _dispatcher = dispatcher;
            _logger = logger;
        }

        [HttpPost("payos")]
        public async Task<IActionResult> ReceivePayOSWebhook([FromBody] Webhook body)
        {
            var result = await _dispatcher.DispatchAsync(body);

            switch (result)
            {
                case WebhookDispatchResult.InvalidSignature:
                    _logger.LogWarning("PayOS webhook (legacy): invalid signature.");
                    return BadRequest(new { error = -1, message = "Invalid webhook signature." });

                case WebhookDispatchResult.NotFound:
                    _logger.LogWarning("PayOS webhook (legacy): order code not found or unknown prefix.");
                    return Ok(new { error = 0, message = "Webhook acknowledged but no matching record found." });

                case WebhookDispatchResult.NotSuccessful:
                    _logger.LogInformation("PayOS webhook (legacy): payment not successful.");
                    return Ok(new { error = 0, message = "Webhook acknowledged but payment not successful." });

                case WebhookDispatchResult.AlreadyProcessed:
                    _logger.LogInformation("PayOS webhook (legacy): already processed, skipping.");
                    return Ok(new { error = 0, message = "Webhook already processed." });

                case WebhookDispatchResult.Conflict:
                    _logger.LogCritical("PayOS webhook (legacy): conflict -- order code {OrderCode} exists in multiple domains. Manual resolution required.", body?.Data?.OrderCode);
                    return Ok(new { error = 0, message = "Webhook acknowledged. Conflict detected -- manual resolution required." });

                case WebhookDispatchResult.SubscriptionHandled:
                case WebhookDispatchResult.OrderHandled:
                    _logger.LogInformation("PayOS webhook (legacy): handled successfully ({Result}).", result);
                    return Ok(new { error = 0, message = "Webhook handled successfully." });

                default:
                    _logger.LogWarning("PayOS webhook (legacy): unknown result {Result}.", result);
                    return Ok(new { error = 0, message = "Webhook acknowledged." });
            }
        }
    }
}

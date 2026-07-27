using ApplicationLayer.Services.PayOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PayOS.Models.Webhooks;
using static System.Runtime.InteropServices.JavaScript.JSType;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PresentationLayer.Controllers
{
    [Route("api/webhook")]
    [ApiController]
    [Obsolete("Use /api/webhooks/payos instead. This endpoint is kept for backward compatibility.")]
    public class WebhookController : ControllerBase
    {
        private readonly IPayOSWebhookDispatcher _dispatcher;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            IPayOSWebhookDispatcher dispatcher,
            ILogger<WebhookController> logger)
        {
            _dispatcher = dispatcher;
            _logger = logger;
        }

        [HttpPost("payos")]
        public async Task<IActionResult> ReceivePayOSWebhook([FromBody] Webhook body)
        {
            var result = await _dispatcher.DispatchAsync(body);
            return result switch
            {
                WebhookDispatchResult.InvalidSignature => BadRequest(new { error = -1, message = "Invalid webhook signature." }),
                WebhookDispatchResult.NotFound => NotFound(new { error = -1, message = "Matching payment is not available yet." }),
                _ => Ok(new { error = 0, message = "Webhook acknowledged.", result = result.ToString() })
            };
        }
    }
}

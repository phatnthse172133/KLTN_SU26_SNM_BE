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
        private readonly IOrderService _orderService;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(IOrderService orderService, ILogger<WebhookController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpPost("payos")]
        public async Task<IActionResult> ReceivePayOSWebhook([FromBody] Webhook body)
        {
            var result = await _dispatcher.DispatchAsync(body);

            //var data = bodyReceived.Data;

            //// Kiểm tra xem đây có phải là tín hiệu hoàn tiền hay không
            //if (!string.IsNullOrEmpty(data.Reference) && data.Reference.StartsWith("refund_"))
            //{
            //    _logger.LogInformation($"[Webhook Payout] Nhận tín hiệu xử lý hoàn tiền cho ID: {data.Reference}");

            //    // Gọi hàm xử lý cập nhật trạng thái sang Refunded/Paid (Hàm đã viết ở câu trước)
            //    var result = await _orderService.ProcessPayoutWebhookAsync(bodyReceived);
            //    return result ? Ok() : BadRequest("Xử lý webhook Payout thất bại");
            //}

            // Nếu không phải tín hiệu hoàn tiền, thì đây là tín hiệu thanh toán
            bool isSuccess = await _orderService.ProcessPaymentWebhookAsync(bodyReceived);
            return isSuccess ? Ok() : BadRequest("Xử lý webhook Payment thất bại");
        }
    }
}

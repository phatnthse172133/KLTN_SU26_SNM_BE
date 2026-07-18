using ApplicationLayer.Services.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using PayOS.Models.Webhooks;
using static System.Runtime.InteropServices.JavaScript.JSType;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PresentationLayer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(IOrderService orderService, ILogger<WebhookController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }
        // GET: api/<WebhookController>
        //[HttpGet]
        //public IEnumerable<string> Get()
        //{
        //    return new string[] { "value1", "value2" };
        //}

        //// GET api/<WebhookController>/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

        // POST api/<WebhookController>
        [HttpPost("payos")]
        public async Task<IActionResult> ReceivePayOSWebhook([FromBody] Webhook bodyReceived)
        {
            if (bodyReceived == null)
            {
                return BadRequest();
            }

            var data = bodyReceived.Data;

            // Kiểm tra xem đây có phải là tín hiệu hoàn tiền hay không
            if (!string.IsNullOrEmpty(data.Reference) && data.Reference.StartsWith("refund_"))
            {
                _logger.LogInformation($"[Webhook Payout] Nhận tín hiệu xử lý hoàn tiền cho ID: {data.Reference}");

                // Gọi hàm xử lý cập nhật trạng thái sang Refunded/Paid (Hàm đã viết ở câu trước)
                var result = await _orderService.ProcessPayoutWebhookAsync(bodyReceived);
                return result ? Ok() : BadRequest("Xử lý webhook Payout thất bại");
            }

            // Nếu không phải tín hiệu hoàn tiền, thì đây là tín hiệu thanh toán
            bool isSuccess = await _orderService.ProcessPaymentWebhookAsync(bodyReceived);
            return isSuccess ? Ok() : BadRequest("Xử lý webhook Payment thất bại");
        }

        // PUT api/<WebhookController>/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE api/<WebhookController>/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}

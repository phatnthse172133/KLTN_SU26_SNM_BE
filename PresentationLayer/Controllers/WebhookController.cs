using ApplicationLayer.Services.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using PayOS.Models.Webhooks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PresentationLayer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public WebhookController(IOrderService orderService)
        {
            _orderService = orderService;
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

            // Truyền nguyên cái object body nhận được xuống tầng Application để xác thực và xử lý
            bool isSuccess = await _orderService.ProcessPaymentWebhookAsync(bodyReceived);

            if (!isSuccess)
            {
                return BadRequest(new { error = -1, message = "Xác thực thất bại hoặc đơn hàng không hợp lệ." });
            }

            // Trả về kết quả báo cho PayOS biết Server đã xử lý xong, đừng bắn lại nữa
            return Ok(new { error = 0, message = "Webhook handled successfully" });
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

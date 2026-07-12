using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Orders;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PresentationLayer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        //// GET: api/<OrderController>
        //[HttpGet]
        //public IEnumerable<string> Get()
        //{
        //    return new string[] { "value1", "value2" };
        //}

        // GET api/<OrderController>/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

        // POST api/<OrderController>
        //[HttpPost]
        //public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        //{
        //    if (dto == null || dto.Items.Count == 0)
        //    {
        //        return BadRequest(new { message = "Giỏ hàng không có sản phẩm nào!" });
        //    }

        //    var response = await _orderService.CreateOrderAsync(dto);
        //    return response.Success ? Ok(response) : BadRequest(response);
        //}

        //// PUT api/<OrderController>/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE api/<OrderController>/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}

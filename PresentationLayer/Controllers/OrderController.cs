using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.Orders;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

        private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // POST api/<OrderController>
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            if (dto == null || dto.Items.Count == 0)
            {
                return BadRequest(new { message = "Giỏ hàng không có sản phẩm nào!" });
            }

            var response = await _orderService.CreateOrderAsync(dto);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPut("update-status/{orderCode}")]
        public async Task<IActionResult> UpdateOrderStatus([FromBody] UpdateOrderStatusDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ!" });
            }

            var response = await _orderService.UpdateOrderStatusByBoothOwnerAsync(CurrentUserId, dto);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        
    }
}

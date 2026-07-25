using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.Orders;
using Microsoft.AspNetCore.Authorization;
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
        //TEST, khi nÃ o cháº¡y tháº­t láº¥y dÃ²ng trÃªn, cÃ²n khi test thÃ¬ dÃ¹ng dÃ²ng dÆ°á»›i
        //private Guid CurrentUserId => Guid.Parse("22222222-2222-2222-2222-222222222222");

        // POST api/<OrderController>
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            if (dto == null || dto.Items.Count == 0)
            {
                return BadRequest(new { message = "Giá» hÃ ng khÃ´ng cÃ³ sáº£n pháº©m nÃ o!" });
            }

            var response = await _orderService.CreateOrderAsync(dto);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPut("update-status/{orderCode}")]
        public async Task<IActionResult> UpdateOrderStatus([FromRoute] long orderCode, [FromBody] UpdateOrderStatusDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new { message = "Dá»¯ liá»‡u khÃ´ng há»£p lá»‡!" });
            }

            var response = await _orderService.UpdateOrderStatusByBoothOwnerAsync(CurrentUserId, dto);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPut("orders/{orderCode}/cancel")]
        public async Task<IActionResult> CancelOrder([FromRoute] long orderCode)
        {
            var response = await _orderService.CancelOrder(orderCode);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpGet("orders/{orderCode}/check-payment-status")]
        public async Task<IActionResult> CheckPaymentStatus([FromRoute] long orderCode)
        {
            var response = await _orderService.ActiveCheckPaymentStatus(orderCode);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("orders/{orderCode}/pay-remaining")]
        [Authorize(Roles = "Customer,BoothOwner")]
        public async Task<IActionResult> PayRemainingAmount([FromRoute] long orderCode)
        {
            var response = await _orderService.PayRemainingAmountAsync(CurrentUserId, orderCode);
            if (response.Success)
                return Ok(response);

            return response.ErrorCode switch
            {
                "ORDER_NOT_FOUND" => NotFound(response),
                "ORDER_ACCESS_DENIED" => StatusCode(StatusCodes.Status403Forbidden, response),
                "ORDER_NOT_UNDERPAID" or "ORDER_ALREADY_FULLY_PAID" => Conflict(response),
                "SUPPLEMENTAL_PAYMENT_LINK_FAILED" => StatusCode(StatusCodes.Status502BadGateway, response),
                _ => BadRequest(response)
            };
        }

    }
}

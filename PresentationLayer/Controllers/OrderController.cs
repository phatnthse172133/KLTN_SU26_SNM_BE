using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        [Authorize(Roles = "Customer,BoothOwner")]
        [EnableRateLimiting("OrderApiPolicy")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            if (dto == null || dto.Items.Count == 0)
            {
                return BadRequest(new { message = "Giá» hÃ ng khÃ´ng cÃ³ sáº£n pháº©m nÃ o!" });
            }

            if (dto.CheckoutRequestId == Guid.Empty)
            {
                if (!Guid.TryParse(Request.Headers["Idempotency-Key"].FirstOrDefault(), out var requestId))
                    return BadRequest(new { message = "A GUID CheckoutRequestId or Idempotency-Key header is required." });
                dto.CheckoutRequestId = requestId;
            }

            var role = User.FindFirstValue(ClaimTypes.Role);
            if (role == "Customer")
            {
                dto.CustomerId = CurrentUserId;
                dto.IsCreatedByBooth = false;
            }
            else
            {
                dto.BoothOwnerId = CurrentUserId;
                dto.IsCreatedByBooth = true;
            }

            var response = await _orderService.CreateOrderAsync(dto);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPut("update-status/{orderCode}")]
        [Authorize(Roles = "BoothOwner")]
        public async Task<IActionResult> UpdateOrderStatus([FromRoute] long orderCode, [FromBody] UpdateOrderStatusDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new { message = "Dá»¯ liá»‡u khÃ´ng há»£p lá»‡!" });
            }

            if (dto.OrderCode != 0 && dto.OrderCode != orderCode)
                return BadRequest(new { message = "Order code in route and body must match." });

            dto.OrderCode = orderCode;
            var response = await _orderService.UpdateOrderStatusByBoothOwnerAsync(CurrentUserId, dto);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPut("{orderCode}/Customer/Cancel")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CancelOrderByCustomer([FromRoute] long orderCode)
        {
            var response = await _orderService.CancelOrderByCustomer(CurrentUserId, orderCode);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPut("{orderCode}/BoothOwner/Cancel")]
        [Authorize(Roles = "BoothOwner")]
        public async Task<IActionResult> CancelOrderByBoothOwner([FromRoute] long orderCode, [FromBody] RefundQRRequest request)
        {
            var response = await _orderService.CancelOrderByBoothOwnerAsync(CurrentUserId, orderCode, request);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("{orderCode}/BoothOwner/Refund/Reconcile")]
        [Authorize(Roles = "BoothOwner")]
        public async Task<IActionResult> ReconcileRefund([FromRoute] long orderCode)
        {
            var response = await _orderService.ReconcileRefundAsync(CurrentUserId, orderCode);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        //[HttpGet("{orderCode}/check-payment-status")]
        //public async Task<IActionResult> CheckPaymentStatus([FromRoute] long orderCode)
        //{
        //    var response = await _orderService.ActiveCheckPaymentStatus(orderCode);
        //    return response.Success ? Ok(response) : BadRequest(response);
        //}

    }
}

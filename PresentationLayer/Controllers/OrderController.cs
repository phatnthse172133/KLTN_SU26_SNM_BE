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
        //TEST, khi nào chạy thật lấy dòng trên, còn khi test thì dùng dòng dưới
        //private Guid CurrentUserId => Guid.Parse("22222222-2222-2222-2222-222222222222");

        // POST api/<OrderController>
        [HttpPost]
        [Authorize(Roles = "Customer,BoothOwner")]
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
        [Authorize(Roles = "BoothOwner")]
        public async Task<IActionResult> UpdateOrderStatus([FromRoute] long orderCode, [FromBody] UpdateOrderStatusDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ!" });
            }

            dto.OrderCode = orderCode;
            var response = await _orderService.UpdateOrderStatusByBoothOwnerAsync(CurrentUserId, dto);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPut("orders/{orderCode}/cancel")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CancelOrder([FromRoute] long orderCode)
        {
            var response = await _orderService.CancelOrder(orderCode);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpGet("orders/{orderCode}/check-payment-status")]
        [Authorize(Roles = "Customer,BoothOwner")]
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

        [HttpGet("booth-owner")]
        [Authorize(Roles = "BoothOwner")]
        public async Task<IActionResult> GetBoothOwnerOrders(
            [FromQuery] BoothOwnerOrderQuery query,
            CancellationToken cancellationToken)
            => Ok(await _orderService.GetBoothOwnerOrdersAsync(
                CurrentUserId, query, cancellationToken));

        [HttpGet("booth-owner/{orderCode:long}")]
        [Authorize(Roles = "BoothOwner")]
        public async Task<IActionResult> GetBoothOwnerOrder(
            [FromRoute] long orderCode,
            CancellationToken cancellationToken)
            => Ok(await _orderService.GetBoothOwnerOrderAsync(
                CurrentUserId, orderCode, cancellationToken));

        [HttpPost("booth-owner")]
        [Authorize(Roles = "BoothOwner")]
        public async Task<IActionResult> CreateWalkInOrder(
            [FromBody] CreateWalkInOrderRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _orderService.CreateWalkInOrderAsync(
                CurrentUserId, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }

        [HttpPatch("booth-owner/{orderCode:long}/status")]
        [Authorize(Roles = "BoothOwner")]
        public async Task<IActionResult> UpdateBoothOwnerOrderStatus(
            [FromRoute] long orderCode,
            [FromBody] UpdateBoothOwnerOrderStatusRequest request,
            CancellationToken cancellationToken)
            => Ok(await _orderService.UpdateBoothOwnerOrderStatusAsync(
                CurrentUserId, orderCode, request, cancellationToken));

        [HttpPost("booth-owner/{orderCode:long}/confirm-cash-payment")]
        [Authorize(Roles = "BoothOwner")]
        public async Task<IActionResult> ConfirmCashPayment(
            [FromRoute] long orderCode,
            CancellationToken cancellationToken)
            => Ok(await _orderService.ConfirmCashPaymentAsync(
                CurrentUserId, orderCode, cancellationToken));

    }
}

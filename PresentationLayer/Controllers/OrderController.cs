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
        //TEST, khi nào chạy thật lấy dòng trên, còn khi test thì dùng dòng dưới
        //private Guid CurrentUserId => Guid.Parse("22222222-2222-2222-2222-222222222222");

        // POST api/<OrderController>
        //[NonAction]
        [HttpPost]
        [Authorize(Roles = "Customer,BoothOwner")]
        [EnableRateLimiting("OrderApiPolicy")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            if (dto == null || dto.Items.Count == 0)
            {
                return BadRequest(new { message = "The cart does not contain any items." });
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

        //[NonAction]
        [HttpPut("update-status/{orderCode}")]
        [Authorize(Roles = "BoothOwner")]
        public async Task<IActionResult> UpdateOrderStatus([FromRoute] long orderCode, [FromBody] UpdateOrderStatusDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new { message = "The request data is invalid." });
            }

            if (dto.OrderCode != 0 && dto.OrderCode != orderCode)
                return BadRequest(new { message = "Order code in route and body must match." });

            dto.OrderCode = orderCode;
            var response = await _orderService.UpdateOrderStatusByBoothOwnerAsync(CurrentUserId, dto);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [NonAction]
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

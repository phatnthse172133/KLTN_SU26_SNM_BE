using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.PaymentMethods;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PresentationLayer.Controllers
{
    [ApiController]
    [Route("api/payment-methods")]
    public class PaymentMethodController : ControllerBase
    {
        private readonly IPaymentMethodService _service;

        public PaymentMethodController(IPaymentMethodService service)
        {
            _service = service;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPut("SetUpPaymentMethod")]
        public async Task<IActionResult> SetupPaymentMethod([FromBody] SetupPaymentDto dto)
        {
            var response = await _service.SetupPaymentMethodAsync(dto, CurrentUserId);
            return response.Success ? Ok(response) : BadRequest(response);
        }
    }
}

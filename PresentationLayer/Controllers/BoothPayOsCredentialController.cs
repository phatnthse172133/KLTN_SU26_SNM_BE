using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.BoothPayOsCredentials;
using DomainLayer.InterfaceRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PresentationLayer.Controllers
{
    [Route("api/booth-owner/payos-credentials")]
    [ApiController]
    [Authorize(Roles = "BoothOwner")]
    public class BoothPayOsCredentialController : ControllerBase
    {
        private readonly IBoothPayOsCredentialService _service;
        private readonly IBoothRepository _booths;

        public BoothPayOsCredentialController(IBoothPayOsCredentialService service, IBoothRepository booths)
        {
            _service = service;
            _booths = booths;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPut("mine")]
        public async Task<IActionResult> UpsertCredential(
            [FromBody] UpsertMyBoothPayOsCredentialRequest request,
            CancellationToken cancellationToken)
        {
            var booth = await _booths.GetByOwnerIdAsync(CurrentUserId, cancellationToken)
                ?? throw AppException.NotFound("You do not have a booth yet.", "BOOTH_NOT_ASSIGNED");

            var result = await _service.UpsertCredentialAsync(new UpsertPayOsCredentialRequest
            {
                BoothId = booth.Id,
                ClientId = request.ClientId,
                ApiKey = request.ApiKey,
                ChecksumKey = request.ChecksumKey,
                PayoutClientId = request.PayoutClientId,
                PayoutApiKey = request.PayoutApiKey,
                PayoutChecksumKey = request.PayoutChecksumKey,
            }, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Kiểm tra quầy đã cài đặt PayOS chưa
        /// </summary>
        [HttpGet("mine")]
        public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
        {
            var booth = await _booths.GetByOwnerIdAsync(CurrentUserId, cancellationToken)
                ?? throw AppException.NotFound("You do not have a booth yet.", "BOOTH_NOT_ASSIGNED");
            var result = await _service.GetStatusAsync(booth.Id, cancellationToken);
            return Ok(result);
        }

        
    }
}

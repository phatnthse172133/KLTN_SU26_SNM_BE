using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.BoothPayOsCredentials;
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

        public BoothPayOsCredentialController(IBoothPayOsCredentialService service)
        {
            _service = service;
        }

        //private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost]
        public async Task<IActionResult> UpsertCredential(
            [FromBody] UpsertPayOsCredentialRequest request,
            CancellationToken cancellationToken)
        {
            //var boothId = GetBoothIdFromClaims();
            var result = await _service.UpsertCredentialAsync(request, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Kiểm tra quầy đã cài đặt PayOS chưa
        /// </summary>
        [HttpGet("status/{boothId:guid}")]
        public async Task<IActionResult> GetStatus(Guid boothId, CancellationToken cancellationToken)
        {
            //var boothId = GetBoothIdFromClaims();
            var result = await _service.GetStatusAsync(boothId, cancellationToken);
            return Ok(result);
        }

        
    }
}

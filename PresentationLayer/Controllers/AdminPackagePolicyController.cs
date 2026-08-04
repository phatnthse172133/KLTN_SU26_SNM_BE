using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PresentationLayer.Controllers
{
    [Route("api/admin/packages/{packageId:guid}/policies")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminPackagePolicyController : ControllerBase
    {
        private readonly IPackagePolicyService _service;

        public AdminPackagePolicyController(IPackagePolicyService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetPolicyVersions(Guid packageId, CancellationToken cancellationToken)
            => Ok(await _service.GetPolicyVersionsAsync(packageId, cancellationToken));

        [HttpGet("active")]
        public async Task<IActionResult> GetActivePolicy(Guid packageId, CancellationToken cancellationToken)
            => Ok(await _service.GetActivePolicyAsync(packageId, cancellationToken));

        [HttpPost]
        public async Task<IActionResult> CreatePolicy(Guid packageId, [FromBody] CreatePackagePolicyRequest request, CancellationToken cancellationToken)
            => Ok(await _service.CreatePolicyAsync(packageId, request, cancellationToken));

        [HttpPut("{policyId:guid}/activate")]
        public async Task<IActionResult> ActivatePolicy(Guid packageId, Guid policyId, CancellationToken cancellationToken)
            => Ok(await _service.ActivatePolicyAsync(packageId, policyId, cancellationToken));
    }
}

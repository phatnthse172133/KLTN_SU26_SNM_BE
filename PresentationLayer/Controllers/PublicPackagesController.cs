using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Packages;
using ApplicationLayer.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/packages")]
[Authorize(Roles = "MarketOwner,BoothOwner,Admin")]
public class PublicPackagesController : ControllerBase
{
    private readonly IPackageService _service;
    private readonly IPackagePolicyService _policyService;

    public PublicPackagesController(IPackageService service, IPackagePolicyService policyService)
    {
        _service = service;
        _policyService = policyService;
    }

    [HttpGet]
    public async Task<IActionResult> GetActivePackages([FromQuery] PackageType type, CancellationToken cancellationToken)
        => Ok(await _service.GetActiveByTypeAsync(type, cancellationToken));

    [HttpGet("{packageId:guid}")]
    public async Task<IActionResult> GetById(Guid packageId, CancellationToken cancellationToken)
        => Ok(await _service.GetActiveByIdAsync(packageId, cancellationToken));

    [HttpGet("{packageId:guid}/policy")]
    public async Task<IActionResult> GetActivePolicy(Guid packageId, CancellationToken cancellationToken)
        => Ok(await _policyService.GetActivePolicyAsync(packageId, cancellationToken));
}

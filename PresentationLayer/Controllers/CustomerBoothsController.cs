using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.CustomerDiscovery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/customer/booths")]
public sealed class CustomerBoothsController : ControllerBase
{
    private readonly ICustomerDiscoveryService _service;

    public CustomerBoothsController(ICustomerDiscoveryService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] CustomerBoothQueryRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetBoothsAsync(request, cancellationToken));

    [HttpGet("{boothId:guid}")]
    public async Task<IActionResult> Get(Guid boothId, CancellationToken cancellationToken)
        => Ok(await _service.GetBoothAsync(boothId, cancellationToken));

    [HttpGet("{boothId:guid}/foods")]
    public async Task<IActionResult> GetFoods(
        Guid boothId,
        [FromQuery] CustomerFoodQueryRequest request,
        CancellationToken cancellationToken)
    {
        request.BoothId = boothId;
        return Ok(await _service.GetFoodsAsync(request, cancellationToken));
    }
}

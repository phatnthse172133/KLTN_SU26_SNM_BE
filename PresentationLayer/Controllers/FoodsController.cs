using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.CustomerDiscovery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/foods")]
public sealed class FoodsController : ControllerBase
{
    private readonly ICustomerDiscoveryService _service;

    public FoodsController(ICustomerDiscoveryService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] CustomerFoodQueryRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetFoodsAsync(request, cancellationToken));

    [HttpGet("{foodItemId:guid}")]
    public async Task<IActionResult> Get(Guid foodItemId, CancellationToken cancellationToken)
        => Ok(await _service.GetFoodAsync(foodItemId, cancellationToken));
}

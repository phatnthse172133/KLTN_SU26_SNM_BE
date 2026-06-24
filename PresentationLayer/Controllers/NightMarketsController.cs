using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.NightMarkets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/night-markets")]
public class NightMarketsController : ControllerBase
{
    private readonly INightMarketService _service;
    public NightMarketsController(INightMarketService service) => _service = service;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetAllAsync(pagination, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var response = await _service.GetAsync(id, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateNightMarketRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.CreateAsync(request, cancellationToken);
        return response.Success ? CreatedAtAction(nameof(Get), new { id = response.Data!.Id }, response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateNightMarketRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpdateAsync(id, request, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }
}

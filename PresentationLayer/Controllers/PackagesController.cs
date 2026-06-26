using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Packages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/admin/packages")]
[Authorize(Roles = "Admin")]
public class PackagesController : ControllerBase
{
    private readonly IPackageService _service;

    public PackagesController(IPackageService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationReq pagination, CancellationToken cancellationToken = default)
        => Ok(await _service.GetAllAsync(pagination, cancellationToken));

    [HttpGet("{packageId:guid}")]
    public async Task<IActionResult> Get(Guid packageId, CancellationToken cancellationToken)
        => Ok(await _service.GetByIdAsync(packageId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreatePackageRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { packageId = response.Data!.Id }, response);
    }

    [HttpPut("{packageId:guid}")]
    public async Task<IActionResult> Update(Guid packageId, UpdatePackageRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(packageId, request, cancellationToken));

    [HttpDelete("{packageId:guid}")]
    public async Task<IActionResult> Delete(Guid packageId, CancellationToken cancellationToken)
        => Ok(await _service.DeleteAsync(packageId, cancellationToken));
}

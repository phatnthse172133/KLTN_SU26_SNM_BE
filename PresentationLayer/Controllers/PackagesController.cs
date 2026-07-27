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

    public PackagesController(IPackageService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationReq pagination, CancellationToken cancellationToken = default)
        => Ok(await _service.GetAllAsync(pagination, cancellationToken));

    [HttpGet("templates")]
    public IActionResult GetTemplates()
        => Ok(_service.GetTemplates());

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

    [HttpPost("{packageId:guid}/image")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(Guid packageId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Failure("Image file is required.", "IMAGE_FILE_REQUIRED"));

        await using var stream = file.OpenReadStream();
        return Ok(await _service.UploadImageAsync(packageId, stream, file.FileName, file.ContentType, file.Length, cancellationToken));
    }

    [HttpDelete("{packageId:guid}/image")]
    public async Task<IActionResult> DeleteImage(Guid packageId, CancellationToken cancellationToken)
        => Ok(await _service.DeleteImageAsync(packageId, cancellationToken));
}

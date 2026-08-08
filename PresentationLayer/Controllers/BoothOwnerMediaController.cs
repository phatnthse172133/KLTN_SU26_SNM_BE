using System.Security.Claims;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.BoothMedia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize(Roles = "BoothOwner")]
[Route("api/booth-owner/booth")]
public class BoothOwnerMediaController : ControllerBase
{
    private readonly IBoothMediaService _service;

    public BoothOwnerMediaController(IBoothMediaService service)
    {
        _service = service;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments(CancellationToken cancellationToken)
        => Ok(await _service.GetDocumentsAsync(CurrentUserId, cancellationToken));

    [HttpPost("documents")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument([FromForm] string? documentType, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Failure("Document file is required.", "DOCUMENT_FILE_REQUIRED"));

        await using var stream = file.OpenReadStream();
        return Ok(await _service.UploadDocumentAsync(
            CurrentUserId, documentType, stream, file.FileName, file.ContentType, file.Length, cancellationToken));
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId, CancellationToken cancellationToken)
        => Ok(await _service.DeleteDocumentAsync(CurrentUserId, documentId, cancellationToken));

    [HttpGet("images")]
    public async Task<IActionResult> GetImages(CancellationToken cancellationToken)
        => Ok(await _service.GetImagesAsync(CurrentUserId, cancellationToken));

    [HttpPost("images")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Failure("Image file is required.", "IMAGE_FILE_REQUIRED"));

        await using var stream = file.OpenReadStream();
        return Ok(await _service.UploadImageAsync(
            CurrentUserId, stream, file.FileName, file.ContentType, file.Length, cancellationToken));
    }

    [HttpDelete("images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid imageId, CancellationToken cancellationToken)
        => Ok(await _service.DeleteImageAsync(CurrentUserId, imageId, cancellationToken));

    [HttpPost("images/{imageId:guid}/cover")]
    public async Task<IActionResult> SetCoverImage(Guid imageId, CancellationToken cancellationToken)
        => Ok(await _service.SetCoverImageAsync(CurrentUserId, imageId, cancellationToken));

    [HttpPut("logo")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Failure("Image file is required.", "IMAGE_FILE_REQUIRED"));

        await using var stream = file.OpenReadStream();
        return Ok(await _service.UploadLogoAsync(
            CurrentUserId, stream, file.FileName, file.ContentType, file.Length, cancellationToken));
    }
}

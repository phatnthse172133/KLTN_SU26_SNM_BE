using System.Security.Claims;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize]
public class ImageUploadController : ControllerBase
{
    private readonly IImageUploadService _uploadService;

    public ImageUploadController(IImageUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("api/market-owner/booths/{boothId}/thumbnail")]
    [Authorize(Roles = "MarketOwner")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadBoothThumbnailByMarketOwner(Guid boothId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Failure("Image file is required.", "IMAGE_FILE_REQUIRED"));

        await using var stream = file.OpenReadStream();
        return Ok(await _uploadService.UploadBoothThumbnailByMarketOwnerAsync(
            CurrentUserId, boothId, stream, file.FileName, file.ContentType, file.Length, cancellationToken));
    }

    [HttpPost("api/booth-owner/booths/{boothId}/thumbnail")]
    [Authorize(Roles = "BoothOwner")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadBoothThumbnailByBoothOwner(Guid boothId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Failure("Image file is required.", "IMAGE_FILE_REQUIRED"));

        await using var stream = file.OpenReadStream();
        return Ok(await _uploadService.UploadBoothThumbnailByBoothOwnerAsync(
            CurrentUserId, boothId, stream, file.FileName, file.ContentType, file.Length, cancellationToken));
    }

    [HttpPost("api/booth-owner/booths/{boothId}/food-items/{foodItemId}/images")]
    [Authorize(Roles = "BoothOwner")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadFoodItemThumbnail(Guid boothId, Guid foodItemId, IFormFile file, CancellationToken cancellationToken)
    {
        // the path contains boothId, which boothOwner can use. The service checks booth ownership implicitly via food item.
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Failure("Image file is required.", "IMAGE_FILE_REQUIRED"));

        await using var stream = file.OpenReadStream();
        return Ok(await _uploadService.UploadFoodItemThumbnailAsync(
            CurrentUserId, foodItemId, stream, file.FileName, file.ContentType, file.Length, cancellationToken));
    }

    [HttpPost("api/market-owner/night-markets/{marketId}/layouts/{layoutId}/images")]
    [Authorize(Roles = "MarketOwner")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadLayoutImage(Guid marketId, Guid layoutId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Failure("Image file is required.", "IMAGE_FILE_REQUIRED"));

        await using var stream = file.OpenReadStream();
        return Ok(await _uploadService.UploadLayoutImageAsync(
            CurrentUserId, layoutId, stream, file.FileName, file.ContentType, file.Length, cancellationToken));
    }
}

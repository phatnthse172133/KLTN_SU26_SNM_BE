using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Prices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
public class PricesController : ControllerBase
{
    private readonly IPriceService _service;

    public PricesController(IPriceService service)
    {
        _service = service;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [Authorize(Roles = "BoothOwner")]
    [HttpGet("api/booths/mine/{boothId:guid}/menu/{foodItemId:guid}/prices")]
    public async Task<IActionResult> GetFoodPrices(Guid boothId, Guid foodItemId, [FromQuery] PaginationReq pagination, CancellationToken cancellationToken)
        => Ok(await _service.GetFoodPricesAsync(CurrentUserId, boothId, foodItemId, pagination, cancellationToken));

    [Authorize(Roles = "BoothOwner")]
    [HttpPost("api/booths/mine/{boothId:guid}/menu/{foodItemId:guid}/prices")]
    public async Task<IActionResult> CreateFoodPrice(Guid boothId, Guid foodItemId, CreatePriceRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateFoodPriceAsync(CurrentUserId, boothId, foodItemId, request, cancellationToken));

    [Authorize(Roles = "BoothOwner")]
    [HttpPut("api/booths/mine/{boothId:guid}/menu/{foodItemId:guid}/prices/{priceId:guid}")]
    public async Task<IActionResult> UpdateFoodPrice(Guid boothId, Guid foodItemId, Guid priceId, UpdatePriceRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateFoodPriceAsync(CurrentUserId, boothId, foodItemId, priceId, request, cancellationToken));

    [Authorize(Roles = "BoothOwner")]
    [HttpDelete("api/booths/mine/{boothId:guid}/menu/{foodItemId:guid}/prices/{priceId:guid}")]
    public async Task<IActionResult> DeleteFoodPrice(Guid boothId, Guid foodItemId, Guid priceId, CancellationToken cancellationToken)
        => Ok(await _service.DeleteFoodPriceAsync(CurrentUserId, boothId, foodItemId, priceId, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpGet("api/admin/packages/{packageId:guid}/prices")]
    public async Task<IActionResult> GetPackagePrices(Guid packageId, [FromQuery] PaginationReq pagination, CancellationToken cancellationToken)
        => Ok(await _service.GetPackagePricesAsync(packageId, pagination, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPost("api/admin/packages/{packageId:guid}/prices")]
    public async Task<IActionResult> CreatePackagePrice(Guid packageId, CreatePriceRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreatePackagePriceAsync(packageId, request, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPut("api/admin/packages/{packageId:guid}/prices/{priceId:guid}")]
    public async Task<IActionResult> UpdatePackagePrice(Guid packageId, Guid priceId, UpdatePriceRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdatePackagePriceAsync(packageId, priceId, request, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpDelete("api/admin/packages/{packageId:guid}/prices/{priceId:guid}")]
    public async Task<IActionResult> DeletePackagePrice(Guid packageId, Guid priceId, CancellationToken cancellationToken)
        => Ok(await _service.DeletePackagePriceAsync(packageId, priceId, cancellationToken));
}

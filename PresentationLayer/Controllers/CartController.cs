using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Carts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize(Roles = "Customer")]
public class CartController : ControllerBase
{
    private readonly ICartService _service;

    public CartController(ICartService service)
    {
        _service = service;
    }

    private Guid CurrentUserId
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetCurrent([FromQuery] PaginationReq pagination, CancellationToken cancellationToken)
        => Ok(await _service.GetCurrentAsync(
            CurrentUserId,
            pagination,
            cancellationToken));

    [HttpPost("items")]
    public async Task<IActionResult> AddItem(AddCartItemRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.AddItemAsync(CurrentUserId, request, cancellationToken);
        return CreatedAtAction(
            nameof(GetCurrent),
            new { page = 1, pageSize = 10 },
            response);
    }

    [HttpPatch("items/{cartItemId:guid}")]
    public async Task<IActionResult> UpdateQuantity(Guid cartItemId, UpdateCartItemQuantityRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateQuantityAsync(CurrentUserId, cartItemId, request, cancellationToken));

    [HttpDelete("items/{cartItemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid cartItemId, CancellationToken cancellationToken)
        => Ok(await _service.RemoveItemAsync(CurrentUserId, cartItemId, cancellationToken));

    [HttpDelete("booths/{boothId:guid}")]
    public async Task<IActionResult> RemoveBoothItems(Guid boothId, CancellationToken cancellationToken)
        => Ok(await _service.RemoveBoothItemsAsync(CurrentUserId, boothId, cancellationToken));

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
        => Ok(await _service.ClearAsync(CurrentUserId, cancellationToken));
}

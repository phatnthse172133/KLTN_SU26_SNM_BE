using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/market-owner/booth-owner-accounts")]
[Authorize(Roles = "MarketOwner")]
public sealed class MarketOwnerBoothOwnerAccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public MarketOwnerBoothOwnerAccountsController(IAccountService accountService)
        => _accountService = accountService;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] BoothOwnerAccountListRequest request, CancellationToken cancellationToken)
        => Ok(await _accountService.GetCreatedBoothOwnerAccountsAsync(CurrentUserId, request, cancellationToken));

    [HttpPost]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> Create(CreateBoothOwnerAccountRequest request, CancellationToken cancellationToken)
        => Ok(await _accountService.CreateBoothOwnerAccountAsync(CurrentUserId, request, cancellationToken));

    [HttpPost("{boothOwnerId:guid}/resend-invitation")]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> Resend(Guid boothOwnerId, CancellationToken cancellationToken)
        => Ok(await _accountService.ResendBoothOwnerInvitationAsync(CurrentUserId, boothOwnerId, cancellationToken));

    [HttpPost("{boothOwnerId:guid}/cancel-invitation")]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> Cancel(Guid boothOwnerId, CancellationToken cancellationToken)
        => Ok(await _accountService.CancelBoothOwnerInvitationAsync(CurrentUserId, boothOwnerId, cancellationToken));
}

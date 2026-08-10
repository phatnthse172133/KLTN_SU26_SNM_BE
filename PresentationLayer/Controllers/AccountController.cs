using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Account;
using ApplicationLayer.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _service;
    private readonly IAuthService _authService;
    public AccountController(IAccountService service, IAuthService authService)
    {
        _service = service;
        _authService = authService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetMyAccount(CancellationToken cancellationToken)
    {
        var response = await _service.GetMyAccountAsync(CurrentUserId, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateMyAccount(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpdateMyAccountAsync(CurrentUserId, request, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpPost("avatar")]
    [EnableRateLimiting("AvatarUploadPolicy")]
    [Consumes("multipart/form-data")]
    // Allow multipart framing overhead; the storage service enforces a 5 MB file cap.
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UpdateAvatar(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
            throw AppException.BadRequest("Avatar file is required.", "AVATAR_FILE_REQUIRED");

        await using var stream = file.OpenReadStream();
        var response = await _service.UpdateAvatarAsync(
            CurrentUserId,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpDelete("avatar")]
    public async Task<IActionResult> RemoveAvatar(CancellationToken cancellationToken)
    {
        var response = await _service.RemoveAvatarAsync(CurrentUserId, cancellationToken);
        return Ok(response);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.ChangePasswordAsync(CurrentUserId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] UserListQuery query, CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetUsersAsync(query, cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("market-owner-accounts")]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> CreateMarketOwnerAccount(
        CreateMarketOwnerAccountRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.CreateMarketOwnerAccountAsync(CurrentUserId, request, cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("market-owner-accounts/{marketOwnerId:guid}/resend-invitation")]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> ResendMarketOwnerInvitation(
        Guid marketOwnerId,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.ResendMarketOwnerInvitationAsync(CurrentUserId, marketOwnerId, cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("users/{userId:guid}")]
    public async Task<IActionResult> GetUser(Guid userId, CancellationToken cancellationToken)
    {
        var response = await _service.GetUserAsync(userId, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("users/{userId:guid}/status")]
    public async Task<IActionResult> ChangeUserStatus(Guid userId, ChangeUserStatusRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.ChangeUserStatusAsync(CurrentUserId, userId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("users/{userId:guid}/status-history")]
    public async Task<IActionResult> GetUserStatusHistory(Guid userId, [FromQuery] PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var response = await _service.GetUserStatusHistoryAsync(userId, pagination, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("users/{userId:guid}/booth-owner-details")]
    public async Task<IActionResult> GetBoothOwnerDetails(Guid userId, CancellationToken cancellationToken)
    {
        return Ok(await _service.GetBoothOwnerDetailsAsync(userId, cancellationToken));
    }
}

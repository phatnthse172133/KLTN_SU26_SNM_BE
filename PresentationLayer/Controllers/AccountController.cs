using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _service;
    public AccountController(IAccountService service)
    {
        _service = service;
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

    [HttpPut("avatar")]
    public async Task<IActionResult> UpdateAvatar(UpdateAvatarRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpdateAvatarAsync(CurrentUserId, request, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.ChangePasswordAsync(CurrentUserId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] UserListQuery query, CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetUsersAsync(query, cancellationToken));
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
}

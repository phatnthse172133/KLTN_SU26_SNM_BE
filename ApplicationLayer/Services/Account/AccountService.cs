using AutoMapper;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceCore.JWT;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;
using ApplicationLayer.Services.Notifications;

namespace ApplicationLayer.Services.Account;

public class AccountService : IAccountService
{
    private readonly IGenericRepository<User> _users;
    private readonly IGenericRepository<Role> _roles;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;
    private readonly INotificationService _notifications;

    public AccountService(IGenericRepository<User> users, IGenericRepository<Role> roles, IPasswordHasher passwordHasher, IMapper mapper, INotificationService notifications)
    {
        _users = users;
        _roles = roles;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
        _notifications = notifications;
    }

    public async Task<ApiResponse<UserResponse>> GetMyAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);
        return user is null ? throw AppException.NotFound("Account was not found.") : await ToMyAccountResponseAsync(user);
    }

    public async Task<ApiResponse<UserResponse>> UpdateMyAccountAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw AppException.NotFound("Account was not found.");

        _mapper.Map(request, user);

        user.FullName = user.FullName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(user.Phone) ? null : user.Phone.Trim();
        user.Address = string.IsNullOrWhiteSpace(user.Address) ? null : user.Address.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        _users.Update(user);
        await _users.SaveChangesAsync();
        return await ToMyAccountResponseAsync(user, "Account updated successfully.");
    }

    public async Task<ApiResponse<UserResponse>> UpdateAvatarAsync(Guid userId, UpdateAvatarRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw AppException.NotFound("Account was not found.");

        user.AvatarUrl = request.AvatarUrl.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        _users.Update(user);

        await _users.SaveChangesAsync();
        return await ToMyAccountResponseAsync(user, "Avatar updated successfully.");
    }

    public async Task<ApiResponse<object>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);

        if (user is null)
            throw AppException.NotFound("Account was not found.");

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            throw AppException.BadRequest("Current password is incorrect.");

        if (_passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
            throw AppException.BadRequest("New password must be different from the current password.");

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        _users.Update(user);

        await _users.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { }, "Password changed successfully. Please sign in again.");
    }

    public async Task<ApiResponse<PaginationResp<ManagedUserResponse>>> GetUsersAsync(PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var page = await _users.GetPagedAsync(
            null, pagination.Page, pagination.PageSize, u => u.CreatedAt, false, cancellationToken);
        var responses = new List<ManagedUserResponse>();
        foreach (var user in page.Items) responses.Add(await ToManagedUserResponseAsync(user));
        return ApiResponse<PaginationResp<ManagedUserResponse>>.SuccessResponse(
            PaginationResp<ManagedUserResponse>.Create(responses, page.TotalCount, pagination));
    }

    public async Task<ApiResponse<ManagedUserResponse>> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);
        return user is null ? throw AppException.NotFound("Account was not found.")
            : ApiResponse<ManagedUserResponse>.SuccessResponse(await ToManagedUserResponseAsync(user));
    }

    public async Task<ApiResponse<ManagedUserResponse>> ChangeUserStatusAsync(Guid adminId, Guid userId, ChangeUserStatusRequest request, CancellationToken cancellationToken = default)
    {
        if (adminId == userId)
            throw AppException.BadRequest("Administrators cannot change the status of their own account.");

        if (request.Status == UserStatus.PendingVerification)
            throw AppException.BadRequest("An account cannot be moved back to pending verification.");

        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw AppException.NotFound("Account was not found.");

        user.Status = request.Status;
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        _users.Update(user);

        await _users.SaveChangesAsync();
        if (request.Status is UserStatus.Suspended or UserStatus.Banned)
        {
            await _notifications.NotifyAsync(new NotificationMessage(
                user.Id,
                NotificationType.AccountSuspended,
                "Account access restricted",
                request.Status == UserStatus.Banned
                    ? "Your account has been banned by an administrator."
                    : "Your account has been suspended by an administrator.",
                ReferenceType: "Account",
                ReferenceId: user.Id), cancellationToken);
        }
        return ApiResponse<ManagedUserResponse>.SuccessResponse(await ToManagedUserResponseAsync(user), "Account status updated successfully.");
    }

    private async Task<ApiResponse<UserResponse>> ToMyAccountResponseAsync(User user, string message = "Success")
    {
        var role = await _roles.GetByIdAsync(user.RoleId);
        return role is null
            ? throw AppException.BadRequest("Account has no valid assigned role.")
            : ApiResponse<UserResponse>.SuccessResponse(new UserResponse(user.Id, user.UserName, user.FullName, user.Email, role.RoleName, user.Status.ToString(), user.AvatarUrl), message);
    }

    private async Task<ManagedUserResponse> ToManagedUserResponseAsync(User user)
    {
        var response = _mapper.Map<ManagedUserResponse>(user);
        response.Role = (await _roles.GetByIdAsync(user.RoleId))?.RoleName ?? "Unknown";
        return response;
    }
}

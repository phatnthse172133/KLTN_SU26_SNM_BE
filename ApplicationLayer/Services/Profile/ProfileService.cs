using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceCore.JWT;
using DomainLayer.InterfaceCore.Auth;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.Profile;

public class ProfileService : IProfileService
{
    private readonly IGenericRepository<User> _users;
    private readonly IGenericRepository<Role> _roles;
    private readonly IAuthTokenStore _tokenStore;
    private readonly IPasswordHasher _passwordHasher;

    public ProfileService(IGenericRepository<User> users, IGenericRepository<Role> roles, IAuthTokenStore tokenStore, IPasswordHasher passwordHasher)
    {
        _users = users;
        _roles = roles;
        _tokenStore = tokenStore;
        _passwordHasher = passwordHasher;
    }

    public async Task<ApiResponse<UserResponse>> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);
        return user is null
            ? ApiResponse<UserResponse>.Failure("Không tìm thấy tài khoản.")
            : await ToResponseAsync(user);
    }

    public async Task<ApiResponse<UserResponse>> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null) return ApiResponse<UserResponse>.Failure("Không tìm thấy tài khoản.");

        user.FullName = request.FullName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        user.DoB = request.DoB;
        user.UpdatedAt = DateTime.UtcNow;

        _users.Update(user);
        await _users.SaveChangesAsync();
        return await ToResponseAsync(user, "Cập nhật hồ sơ thành công.");
    }

    public async Task<ApiResponse<object>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null) return ApiResponse<object>.Failure("Không tìm thấy tài khoản.");
        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return ApiResponse<object>.Failure("Mật khẩu hiện tại không đúng.");
        if (_passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
            return ApiResponse<object>.Failure("Mật khẩu mới phải khác mật khẩu hiện tại.");

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _users.Update(user);
        await RevokeAllSessionsAsync(user.Id);
        await _users.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { }, "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.");
    }

    private async Task<ApiResponse<UserResponse>> ToResponseAsync(User user, string message = "Success")
    {
        var role = await _roles.GetByIdAsync(user.RoleId);
        if (role is null) return ApiResponse<UserResponse>.Failure("Tài khoản chưa được gán role hợp lệ.");
        return ApiResponse<UserResponse>.SuccessResponse(
            new UserResponse(user.Id, user.UserName, user.FullName, user.Email, role.RoleName, user.Status.ToString(), user.AvatarUrl), message);
    }

    private async Task RevokeAllSessionsAsync(Guid userId)
    {
        await _tokenStore.RevokeAllRefreshTokensAsync(userId);
    }
}

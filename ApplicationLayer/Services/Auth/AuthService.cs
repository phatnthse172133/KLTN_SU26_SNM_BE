using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceCore.Email;
using DomainLayer.InterfaceCore.Auth;
using DomainLayer.InterfaceCore.External;
using DomainLayer.InterfaceCore.JWT;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IGenericRepository<User> _userRepository;
    private readonly IGenericRepository<Role> _roleRepository;
    private readonly IAuthTokenStore _tokenStore;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly IGoogleTokenValidator _googleTokenValidator;

    public AuthService(
        IGenericRepository<User> userRepository,
        IGenericRepository<Role> roleRepository,
        IAuthTokenStore tokenStore,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IEmailService emailService,
        IGoogleTokenValidator googleTokenValidator)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _tokenStore = tokenStore;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _emailService = emailService;
        _googleTokenValidator = googleTokenValidator;
    }

    public async Task<ApiResponse<object>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var userName = request.UserName.Trim();

        if (await _userRepository.AnyAsync(user => user.Email == email))
        {
            throw AppException.Conflict("Email is already in use.");
        }

        if (await _userRepository.AnyAsync(user => user.UserName == userName))
        {
            throw AppException.Conflict("Username is already in use.");
        }

        var customerRole = await GetCustomerRoleAsync();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            RoleId = customerRole.Id,
            UserName = userName,
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Status = UserStatus.PendingVerification,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();
        await CreateAndSendVerificationTokenAsync(user, cancellationToken);

        return ApiResponse<object>.SuccessResponse(new { user.Id, user.Email }, "Registration successful. Please check your email to verify your account.");
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var identity = request.EmailOrUserName.Trim().ToLowerInvariant();
        var user = await _userRepository.FirstOrDefaultAsync(item => item.Email == identity || item.UserName.ToLower() == identity);

        if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw AppException.Unauthorized("Email/username or password is incorrect.");
        }

        return await CreateSessionForActiveUserAsync(user, cancellationToken);
    }

    public async Task<ApiResponse<AuthResponse>> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default)
    {
        GoogleUserInfo? googleUser;

        try
        {
            googleUser = await _googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken);
        }
        catch (Exception)
        {
            throw AppException.Unauthorized("Unable to validate the Google token.");
        }

        if (googleUser is null)
        {
            throw AppException.Unauthorized("Google token is invalid or the email has not been verified.");
        }

        var user = await _userRepository.FirstOrDefaultAsync(item => item.Email == googleUser.Email);
        if (user is null)
        {
            var role = await GetCustomerRoleAsync();
            var now = DateTime.UtcNow;
            user = new User
            {
                Id = Guid.NewGuid(),
                RoleId = role.Id,
                Email = googleUser.Email,
                FullName = googleUser.FullName,
                UserName = $"google_{Guid.NewGuid():N}"[..19],
                PasswordHash = _passwordHasher.HashPassword(_jwtService.GenerateSecureToken()),
                AvatarUrl = googleUser.AvatarUrl,
                Status = UserStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();
        }

        return await CreateSessionForActiveUserAsync(user, cancellationToken);
    }

    public async Task<ApiResponse<object>> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw AppException.BadRequest("Verification token is invalid.");
        }

        var tokenHash = _jwtService.HashToken(token);
        var userId = await _tokenStore.ConsumeEmailVerificationAsync(tokenHash);
        if (userId is null)
        {
            throw AppException.BadRequest("Verification token is invalid or has expired.");
        }

        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null)
        {
            throw AppException.NotFound("Account was not found.");
        }

        if (user.Status == UserStatus.PendingVerification)
        {
            user.Status = UserStatus.Active;
            user.UpdatedAt = DateTime.UtcNow;
            _userRepository.Update(user);
        }

        await _userRepository.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { user.Id }, "Email verified successfully.");
    }

    public async Task<ApiResponse<object>> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.FirstOrDefaultAsync(item => item.Email == email);

        if (user is null || user.Status != UserStatus.PendingVerification)
        {
            return ApiResponse<object>.SuccessResponse(new { }, "If the account requires verification, an email has been sent.");
        }

        await CreateAndSendVerificationTokenAsync(user, cancellationToken);
        return ApiResponse<object>.SuccessResponse(new { }, "If the account requires verification, an email has been sent.");
    }

    public async Task<ApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.FirstOrDefaultAsync(item => item.Email == request.Email.Trim().ToLowerInvariant());
        if (user is null || user.Status != UserStatus.Active)
            return ApiResponse<object>.SuccessResponse(new { }, "If the email exists, a password-reset OTP has been sent.");

        var otp = Random.Shared.Next(0, 1_000_000).ToString("D6");
        await _tokenStore.StorePasswordResetOtpAsync(user.Id, _jwtService.HashToken(otp), TimeSpan.FromMinutes(10));
        var rawResetToken = _jwtService.GenerateSecureToken();
        await _tokenStore.StorePasswordResetTokenAsync(_jwtService.HashToken(rawResetToken), user.Id, TimeSpan.FromMinutes(15));
        await _emailService.SendPasswordResetOtpAsync(user.Email, user.FullName, otp, cancellationToken);
        await _emailService.SendPasswordResetLinkAsync(user.Email, user.FullName, rawResetToken, cancellationToken);
        return ApiResponse<object>.SuccessResponse(new { }, "If the email exists, a password-reset OTP has been sent.");
    }

    public async Task<ApiResponse<object>> VerifyPasswordResetOtpAsync(VerifyPasswordResetOtpRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserByEmailAsync(request.Email);
        var valid = user is not null && await _tokenStore.IsPasswordResetOtpValidAsync(user.Id, _jwtService.HashToken(request.Otp));
        return !valid
            ? throw AppException.BadRequest("OTP is invalid, expired, or already used.")
            : ApiResponse<object>.SuccessResponse(new { }, "OTP is valid. You can set a new password.");
    }

    public async Task<ApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserByEmailAsync(request.Email);
        if (user is null)
            throw AppException.BadRequest("This account cannot reset its password.");
        if (_passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
            throw AppException.BadRequest("New password must differ from the current password.");
        if (!await _tokenStore.ConsumePasswordResetOtpAsync(user.Id, _jwtService.HashToken(request.Otp)))
            throw AppException.BadRequest("OTP is invalid, expired, or already used.");

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);
        await _tokenStore.RevokeAllRefreshTokensAsync(user.Id);
        await _userRepository.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { }, "Password reset successfully. Please sign in again.");
    }

    public async Task<ApiResponse<object>> ResetPasswordByTokenAsync(ResetPasswordByTokenRequest request, CancellationToken cancellationToken = default)
    {
        var userId = await _tokenStore.ConsumePasswordResetTokenAsync(_jwtService.HashToken(request.Token));
        if (userId is null) throw AppException.BadRequest("Reset link is invalid, expired, or already used.");
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null || user.Status != UserStatus.Active) throw AppException.BadRequest("This account cannot reset its password.");
        if (_passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash)) throw AppException.BadRequest("New password must differ from the current password.");
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);
        await _tokenStore.RevokeAllRefreshTokensAsync(user.Id);
        await _userRepository.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { }, "Password reset successfully. Please sign in again.");
    }

    public async Task<ApiResponse<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = _jwtService.HashToken(request.RefreshToken);
        var userId = await _tokenStore.ConsumeRefreshTokenAsync(tokenHash);
        if (userId is null)
        {
            throw AppException.Unauthorized("Refresh token is invalid or has expired.");
        }

        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null || user.Status != UserStatus.Active)
        {
            throw AppException.Forbidden("Account is no longer active.");
        }

        var newRawToken = _jwtService.GenerateSecureToken();
        await _tokenStore.StoreRefreshTokenAsync(_jwtService.HashToken(newRawToken), user.Id, GetRefreshTokenTtl());

        return await BuildAuthResponseAsync(user, newRawToken);
    }

    public async Task<ApiResponse<object>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = _jwtService.HashToken(request.RefreshToken);
        await _tokenStore.RevokeRefreshTokenAsync(tokenHash);

        return ApiResponse<object>.SuccessResponse(new { }, "Logged out successfully.");
    }

    private async Task<ApiResponse<AuthResponse>> CreateSessionForActiveUserAsync(User user, CancellationToken cancellationToken)
    {
        if (user.Status == UserStatus.PendingVerification)
        {
            throw AppException.Forbidden("Please verify your email before signing in.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw AppException.Forbidden("Account is not active.");
        }

        var rawToken = _jwtService.GenerateSecureToken();
        await _tokenStore.StoreRefreshTokenAsync(_jwtService.HashToken(rawToken), user.Id, GetRefreshTokenTtl());

        return await BuildAuthResponseAsync(user, rawToken);
    }

    private async Task<ApiResponse<AuthResponse>> BuildAuthResponseAsync(User user, string refreshToken)
    {
        var role = await _roleRepository.GetByIdAsync(user.RoleId);
        if (role is null)
        {
            throw AppException.BadRequest("Account has no valid assigned role.");
        }

        var response = new AuthResponse(
            _jwtService.GenerateAccessToken(user.Id, user.Email, role.RoleName),
            refreshToken,
            _jwtService.GetAccessTokenExpiry(),
            role.RoleName,
            new UserResponse(user.Id, user.UserName, user.FullName, user.Email, role.RoleName, user.Status.ToString(), user.AvatarUrl));

        return ApiResponse<AuthResponse>.SuccessResponse(response, "Signed in successfully.");
    }

    private async Task<Role> GetCustomerRoleAsync()
    {
        var role = await _roleRepository.FirstOrDefaultAsync(item => item.RoleName == "Customer");
        if (role is not null)
        {
            return role;
        }

        var now = DateTime.UtcNow;
        role = new Role
        {
            Id = Guid.NewGuid(),
            RoleName = "Customer",
            Description = "Default role for registered users",
            CreatedAt = now,
            UpdatedAt = now
        };
        await _roleRepository.AddAsync(role);
        await _roleRepository.SaveChangesAsync();
        return role;
    }

    private async Task CreateAndSendVerificationTokenAsync(User user, CancellationToken cancellationToken)
    {
        var rawToken = _jwtService.GenerateSecureToken();
        await _tokenStore.StoreEmailVerificationAsync(
            _jwtService.HashToken(rawToken), user.Id, _jwtService.GetEmailVerificationExpiry() - DateTime.UtcNow);
        await _emailService.SendVerificationEmailAsync(user.Email, user.FullName, rawToken, cancellationToken);
    }

    private Task<User?> FindActiveUserByEmailAsync(string email)
    {
        return _userRepository.FirstOrDefaultAsync(item => item.Email == email.Trim().ToLowerInvariant() && item.Status == UserStatus.Active);
    }

    private TimeSpan GetRefreshTokenTtl() => _jwtService.GetRefreshTokenExpiry() - DateTime.UtcNow;
}

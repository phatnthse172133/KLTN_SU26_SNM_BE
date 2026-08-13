using AutoMapper;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceCore.Email;
using DomainLayer.InterfaceCore.External;
using DomainLayer.InterfaceCore.JWT;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IGenericRepository<User> _userRepository;
    private readonly IGenericRepository<Role> _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IUserDeviceTokenRepository _deviceTokens;
    private readonly IMapper _mapper;

    public AuthService(
        IGenericRepository<User> userRepository,
        IGenericRepository<Role> roleRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IEmailService emailService,
        IGoogleTokenValidator googleTokenValidator,
        IUserDeviceTokenRepository deviceTokens,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _emailService = emailService;
        _googleTokenValidator = googleTokenValidator;
        _deviceTokens = deviceTokens;
        _mapper = mapper;
    }

    public Task<ApiResponse<object>> RegisterCustomerAsync(
        RegisterCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        return RegisterAsync(request, "Customer", cancellationToken);
    }

    public Task<ApiResponse<object>> RegisterBoothOwnerAsync(
        RegisterBoothOwnerRequest request,
        CancellationToken cancellationToken = default)
    {
        return RegisterAsync(request, "BoothOwner", cancellationToken);
    }

    private async Task<ApiResponse<object>> RegisterAsync(
        RegisterRequest request,
        string roleName,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var userName = request.UserName.Trim();

        if (await _userRepository.AnyAsync(user => user.Email == email))
        {
            throw AppException.Conflict("Email is already in use.", AuthErrorCodes.EmailAlreadyExists);
        }

        var normalizedUserName = userName.ToLowerInvariant();
        if (await _userRepository.AnyAsync(user => user.UserName.ToLower() == normalizedUserName))
        {
            throw AppException.Conflict("Username is already in use.", AuthErrorCodes.UserNameAlreadyExists);
        }

        EnsureVerificationEmailConfigured();

        var role = await GetOrCreateRoleAsync(roleName);
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            UserName = normalizedUserName,
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            AuthProvider = AuthProvider.Local,
            Status = UserStatus.PendingVerification,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();
        await CreateAndSendVerificationTokenAsync(user, cancellationToken, throwOnDeliveryFailure: true);

        return ApiResponse<object>.SuccessResponse(
            new { user.Id, user.Email, Role = role.RoleName },
            $"{role.RoleName} registration successful. Please check your email to verify your account.");
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var identity = request.EmailOrUserName.Trim().ToLowerInvariant();
        var user = await _userRepository.FirstOrDefaultAsync(item => item.Email == identity || item.UserName.ToLower() == identity);

        if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw AppException.Unauthorized("Email/username or password is incorrect.", AuthErrorCodes.InvalidCredentials);
        }

        if (user.AuthProvider == AuthProvider.Google)
        {
            throw AppException.BadRequest(
                "This account uses Google sign-in. Please continue with Google.",
                AuthErrorCodes.GooglePasswordLoginNotAllowed);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw AppException.ServiceUnavailable(
                "Google sign-in is temporarily unavailable.",
                AuthErrorCodes.GoogleAuthUnavailable,
                exception);
        }

        if (googleUser is null)
        {
            throw AppException.Unauthorized(
                "Google token is invalid or the email has not been verified.",
                AuthErrorCodes.InvalidGoogleToken);
        }

        var user = await _userRepository.FirstOrDefaultAsync(item => item.GoogleId == googleUser.GoogleId);
        var userWithSameEmail = user is null
            ? await _userRepository.FirstOrDefaultAsync(item => item.Email == googleUser.Email)
            : null;

        // This public endpoint cannot prove that the caller also controls an existing local
        // session, so it must not silently add Google as a sign-in method by matching email.
        if (userWithSameEmail is not null)
        {
            throw AppException.Conflict(
                "An account with this email already exists. Sign in with its existing method before linking Google.",
                AuthErrorCodes.GoogleAccountLinkRequired);
        }

        if (user is null)
        {
            var customerRole = await GetOrCreateRoleAsync("Customer");
            var now = DateTime.UtcNow;
            user = new User
            {
                Id = Guid.NewGuid(),
                RoleId = customerRole.Id,
                Email = googleUser.Email,
                FullName = googleUser.FullName,
                UserName = $"google_{Guid.NewGuid():N}"[..19],
                PasswordHash = _passwordHasher.HashPassword(_jwtService.GenerateSecureToken()),
                AvatarUrl = googleUser.AvatarUrl,
                GoogleId = googleUser.GoogleId,
                AuthProvider = AuthProvider.Google,
                Status = UserStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();
        }

        var role = await _roleRepository.GetByIdAsync(user.RoleId);
        if (role is null || !string.Equals(role.RoleName, "Customer", StringComparison.Ordinal))
        {
            throw AppException.Forbidden(
                "Google sign-in is only available for customer accounts.",
                AuthErrorCodes.GoogleCustomerOnly);
        }

        return await CreateSessionForActiveUserAsync(user, cancellationToken);
    }

    public async Task<ApiResponse<object>> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw AppException.BadRequest(
                "Verification token is invalid.",
                AuthErrorCodes.InvalidOrExpiredVerificationToken);
        }

        var tokenHash = _jwtService.HashToken(token);
        var now = DateTime.UtcNow;
        var user = await _userRepository.FirstOrDefaultAsync(item =>
            item.EmailVerificationTokenHash == tokenHash &&
            item.EmailVerificationTokenExpiresAt > now);
        if (user is null || user.Status != UserStatus.PendingVerification)
        {
            throw AppException.BadRequest(
                "Verification token is invalid or has expired.",
                AuthErrorCodes.InvalidOrExpiredVerificationToken);
        }

        user.Status = UserStatus.Active;

        user.EmailVerificationTokenHash = null;
        user.EmailVerificationTokenExpiresAt = null;
        user.UpdatedAt = now;
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { user.Id }, "Email verified successfully.");
    }

    public async Task<ApiResponse<object>> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default)
    {
        EnsureVerificationEmailConfigured();

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.FirstOrDefaultAsync(item => item.Email == email);

        if (user is null || user.Status != UserStatus.PendingVerification)
        {
            return ApiResponse<object>.SuccessResponse(new { }, "If the account requires verification, an email has been sent.");
        }

        await CreateAndSendVerificationTokenAsync(user, cancellationToken, throwOnDeliveryFailure: false);
        return ApiResponse<object>.SuccessResponse(new { }, "If the account requires verification, an email has been sent.");
    }

    public async Task<ApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePasswordResetEmailConfigured();

        var user = await _userRepository.FirstOrDefaultAsync(item => item.Email == request.Email.Trim().ToLowerInvariant());
        if (user is null || user.Status != UserStatus.Active || user.AuthProvider == AuthProvider.Google)
            return ApiResponse<object>.SuccessResponse(new { }, "If the email exists, a password-reset OTP has been sent.");

        var otp = _jwtService.GenerateNumericCode(6);
        var rawResetToken = _jwtService.GenerateSecureToken();
        var now = DateTime.UtcNow;
        user.PasswordResetOtpHash = _jwtService.HashToken(otp);
        user.PasswordResetOtpExpiresAt = now.AddMinutes(10);
        user.PasswordResetTokenHash = _jwtService.HashToken(rawResetToken);
        user.PasswordResetTokenExpiresAt = now.AddMinutes(15);
        user.UpdatedAt = now;
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync();

        try
        {
            await _emailService.SendPasswordResetOtpAsync(user.Email, user.FullName, otp, cancellationToken);
            await _emailService.SendPasswordResetLinkAsync(user.Email, user.FullName, rawResetToken, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Delivery failure is already logged by EmailService. Keep a generic success
            // so this endpoint cannot be used to confirm that an account exists.
        }

        return ApiResponse<object>.SuccessResponse(new { }, "If the email exists, a password-reset OTP has been sent.");
    }

    public async Task<ApiResponse<object>> VerifyPasswordResetOtpAsync(VerifyPasswordResetOtpRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserByEmailAsync(request.Email);
        var valid = user is not null &&
                    user.PasswordResetOtpHash == _jwtService.HashToken(request.Otp) &&
                    user.PasswordResetOtpExpiresAt > DateTime.UtcNow;
        return !valid
            ? throw AppException.BadRequest(
                "OTP is invalid, expired, or already used.",
                AuthErrorCodes.InvalidOrExpiredResetOtp)
            : ApiResponse<object>.SuccessResponse(new { }, "OTP is valid. You can set a new password.");
    }

    public async Task<ApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserByEmailAsync(request.Email);
        if (user is null || user.AuthProvider == AuthProvider.Google)
            throw AppException.BadRequest(
                "This account cannot reset its password.",
                AuthErrorCodes.PasswordResetNotAllowed);

        if (_passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
            throw AppException.BadRequest(
                "New password must differ from the current password.",
                AuthErrorCodes.PasswordReuseNotAllowed);

        if (user.PasswordResetOtpHash != _jwtService.HashToken(request.Otp) ||
            user.PasswordResetOtpExpiresAt <= DateTime.UtcNow)
            throw AppException.BadRequest(
                "OTP is invalid, expired, or already used.",
                AuthErrorCodes.InvalidOrExpiredResetOtp);

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        ClearPasswordResetTokens(user);
        ClearRefreshToken(user);
        user.UpdatedAt = DateTime.UtcNow;

        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { }, "Password reset successfully. Please sign in again.");
    }

    public async Task<ApiResponse<object>> ResetPasswordByTokenAsync(ResetPasswordByTokenRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = _jwtService.HashToken(request.Token);
        var now = DateTime.UtcNow;
        var user = await _userRepository.FirstOrDefaultAsync(item =>
            item.PasswordResetTokenHash == tokenHash &&
            item.PasswordResetTokenExpiresAt > now);
        if (user is null)
            throw AppException.BadRequest(
                "Reset link is invalid, expired, or already used.",
                AuthErrorCodes.InvalidOrExpiredResetToken);

        if (user.Status != UserStatus.Active || user.AuthProvider == AuthProvider.Google)
            throw AppException.BadRequest(
                "This account cannot reset its password.",
                AuthErrorCodes.PasswordResetNotAllowed);

        if (_passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash)) 
            throw AppException.BadRequest(
                "New password must differ from the current password.",
                AuthErrorCodes.PasswordReuseNotAllowed);

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        ClearPasswordResetTokens(user);
        ClearRefreshToken(user);
        user.UpdatedAt = now;
        _userRepository.Update(user);

        await _userRepository.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { }, "Password reset successfully. Please sign in again.");
    }

    public async Task<ApiResponse<object>> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
            throw AppException.NotFound("Account was not found.", AuthErrorCodes.AccountNotFound);

        if (user.AuthProvider == AuthProvider.Google)
            throw AppException.BadRequest(
                "This account uses Google sign-in and does not have a local password.",
                AuthErrorCodes.PasswordResetNotAllowed);

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            throw AppException.BadRequest(
                "Current password is incorrect.",
                AuthErrorCodes.CurrentPasswordInvalid);

        if (_passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
            throw AppException.BadRequest(
                "New password must be different from the current password.",
                AuthErrorCodes.PasswordReuseNotAllowed);

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        ClearRefreshToken(user);
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(
            new { },
            "Password changed successfully. Please sign in again.");
    }

    public async Task<ApiResponse<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = _jwtService.HashToken(request.RefreshToken);
        var now = DateTime.UtcNow;
        var user = await _userRepository.FirstOrDefaultAsync(item =>
            item.RefreshTokenHash == tokenHash &&
            item.RefreshTokenExpiresAt > now);
        if (user is null)
        {
            throw AppException.Unauthorized(
                "Refresh token is invalid or has expired.",
                AuthErrorCodes.InvalidOrExpiredRefreshToken);
        }

        if (user.Status != UserStatus.Active)
        {
            throw AppException.Forbidden("Account is no longer active.", AuthErrorCodes.AccountNotActive);
        }

        var newRawToken = _jwtService.GenerateSecureToken();
        SetRefreshToken(user, newRawToken);
        user.UpdatedAt = now;
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync();

        return await BuildAuthResponseAsync(user, newRawToken);
    }

    public async Task<ApiResponse<object>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = _jwtService.HashToken(request.RefreshToken);
        var user = await _userRepository.FirstOrDefaultAsync(item => item.RefreshTokenHash == tokenHash);
        if (user is not null)
        {
            ClearRefreshToken(user);
            user.UpdatedAt = DateTime.UtcNow;
            _userRepository.Update(user);
            if (!string.IsNullOrWhiteSpace(request.DeviceToken))
            {
                var tokens = await _deviceTokens.GetByUserAndSelectionAsync(
                    user.Id,
                    request.DeviceToken.Trim(),
                    null,
                    cancellationToken);
                foreach (var deviceToken in tokens)
                {
                    deviceToken.IsActive = false;
                    deviceToken.UpdatedAt = DateTime.UtcNow;
                    _deviceTokens.Delete(deviceToken);
                }
            }
            await _userRepository.SaveChangesAsync();
        }

        return ApiResponse<object>.SuccessResponse(new { }, "Logged out successfully.");
    }

    private async Task<ApiResponse<AuthResponse>> CreateSessionForActiveUserAsync(User user, CancellationToken cancellationToken)
    {
        if (user.Status == UserStatus.PendingVerification)
        {
            throw AppException.Forbidden(
                "Please verify your email before signing in.",
                AuthErrorCodes.EmailNotVerified);
        }

        if (user.Status != UserStatus.Active)
        {
            throw AppException.Forbidden("Account is not active.", AuthErrorCodes.AccountNotActive);
        }

        var rawToken = _jwtService.GenerateSecureToken();
        SetRefreshToken(user, rawToken);
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync();

        return await BuildAuthResponseAsync(user, rawToken);
    }

    private async Task<ApiResponse<AuthResponse>> BuildAuthResponseAsync(User user, string refreshToken)
    {
        var role = await _roleRepository.GetByIdAsync(user.RoleId);
        if (role is null)
        {
            throw AppException.BadRequest(
                "Account has no valid assigned role.",
                AuthErrorCodes.InvalidAccountRole);
        }

        var userResponse = _mapper.Map<UserResponse>(user);
        userResponse.Role = role.RoleName;

        var response = new AuthResponse(
            _jwtService.GenerateAccessToken(user.Id, user.Email, role.RoleName),
            refreshToken,
            _jwtService.GetAccessTokenExpiry(),
            role.RoleName,
            userResponse);

        return ApiResponse<AuthResponse>.SuccessResponse(response, "Signed in successfully.");
    }

    private async Task<Role> GetOrCreateRoleAsync(string roleName)
    {
        var role = await _roleRepository.FirstOrDefaultAsync(item => item.RoleName == roleName);
        if (role is not null)
        {
            return role;
        }

        var now = DateTime.UtcNow;
        role = new Role
        {
            Id = Guid.NewGuid(),
            RoleName = roleName,
            Description = roleName == "BoothOwner"
                ? "Account registered to own and manage booths"
                : "Default role for customer accounts",
            CreatedAt = now,
            UpdatedAt = now
        };
        await _roleRepository.AddAsync(role);
        await _roleRepository.SaveChangesAsync();
        return role;
    }

    private async Task CreateAndSendVerificationTokenAsync(
        User user,
        CancellationToken cancellationToken,
        bool throwOnDeliveryFailure)
    {
        var rawToken = _jwtService.GenerateSecureToken();
        user.EmailVerificationTokenHash = _jwtService.HashToken(rawToken);
        user.EmailVerificationTokenExpiresAt = _jwtService.GetEmailVerificationExpiry();
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync();

        try
        {
            await _emailService.SendVerificationEmailAsync(user.Email, user.FullName, rawToken, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (throwOnDeliveryFailure)
            {
                throw EmailDeliveryFailed(exception);
            }
        }
    }

    private void EnsureVerificationEmailConfigured()
    {
        if (!_emailService.CanSendVerificationEmail())
        {
            throw EmailDeliveryFailed();
        }
    }

    private void EnsurePasswordResetEmailConfigured()
    {
        if (!_emailService.CanSendPasswordResetEmail())
        {
            throw EmailDeliveryFailed();
        }
    }

    private static AppException EmailDeliveryFailed(Exception? innerException = null)
        => AppException.ServiceUnavailable(
            "Unable to send email right now. Please try again later.",
            AuthErrorCodes.EmailDeliveryFailed,
            innerException);

    private Task<User?> FindActiveUserByEmailAsync(string email)
    {
        return _userRepository.FirstOrDefaultAsync(item =>
            item.Email == email.Trim().ToLowerInvariant() &&
            item.Status == UserStatus.Active &&
            item.AuthProvider != AuthProvider.Google);
    }

    private void SetRefreshToken(User user, string rawToken)
    {
        user.RefreshTokenHash = _jwtService.HashToken(rawToken);
        user.RefreshTokenExpiresAt = _jwtService.GetRefreshTokenExpiry();
    }

    private static void ClearRefreshToken(User user)
    {
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
    }

    private static void ClearPasswordResetTokens(User user)
    {
        user.PasswordResetOtpHash = null;
        user.PasswordResetOtpExpiresAt = null;
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
    }
}

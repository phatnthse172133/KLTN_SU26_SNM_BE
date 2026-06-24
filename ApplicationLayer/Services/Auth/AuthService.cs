using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
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
    private readonly IGenericRepository<RefreshToken> _refreshTokenRepository;
    private readonly IGenericRepository<EmailVerificationToken> _verificationTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly IGoogleTokenValidator _googleTokenValidator;

    public AuthService(
        IGenericRepository<User> userRepository,
        IGenericRepository<Role> roleRepository,
        IGenericRepository<RefreshToken> refreshTokenRepository,
        IGenericRepository<EmailVerificationToken> verificationTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IEmailService emailService,
        IGoogleTokenValidator googleTokenValidator)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _verificationTokenRepository = verificationTokenRepository;
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
            return ApiResponse<object>.Failure("Email đã được sử dụng.");
        }

        if (await _userRepository.AnyAsync(user => user.UserName == userName))
        {
            return ApiResponse<object>.Failure("Tên đăng nhập đã được sử dụng.");
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

        return ApiResponse<object>.SuccessResponse(new { user.Id, user.Email }, "Đăng ký thành công. Vui lòng kiểm tra email để xác thực tài khoản.");
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var identity = request.EmailOrUserName.Trim().ToLowerInvariant();
        var user = await _userRepository.FirstOrDefaultAsync(item => item.Email == identity || item.UserName.ToLower() == identity);

        if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return ApiResponse<AuthResponse>.Failure("Email/tên đăng nhập hoặc mật khẩu không đúng.");
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
            return ApiResponse<AuthResponse>.Failure("Không thể xác thực Google token.");
        }

        if (googleUser is null)
        {
            return ApiResponse<AuthResponse>.Failure("Google token không hợp lệ hoặc email chưa được xác thực.");
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
            return ApiResponse<object>.Failure("Verification token không hợp lệ.");
        }

        var tokenHash = _jwtService.HashToken(token);
        var verification = await _verificationTokenRepository.FirstOrDefaultAsync(item =>
            item.TokenHash == tokenHash && item.UsedAt == null && item.ExpiresAt > DateTime.UtcNow);

        if (verification is null)
        {
            return ApiResponse<object>.Failure("Verification token không hợp lệ hoặc đã hết hạn.");
        }

        var user = await _userRepository.GetByIdAsync(verification.UserId);
        if (user is null)
        {
            return ApiResponse<object>.Failure("Không tìm thấy tài khoản.");
        }

        verification.UsedAt = DateTime.UtcNow;
        _verificationTokenRepository.Update(verification);

        if (user.Status == UserStatus.PendingVerification)
        {
            user.Status = UserStatus.Active;
            user.UpdatedAt = DateTime.UtcNow;
            _userRepository.Update(user);
        }

        await _verificationTokenRepository.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { user.Id }, "Xác thực email thành công.");
    }

    public async Task<ApiResponse<object>> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.FirstOrDefaultAsync(item => item.Email == email);

        if (user is null || user.Status != UserStatus.PendingVerification)
        {
            return ApiResponse<object>.SuccessResponse(new { }, "Nếu tài khoản cần xác thực, email đã được gửi.");
        }

        var oldTokens = await _verificationTokenRepository.FindAsync(item => item.UserId == user.Id && item.UsedAt == null);
        foreach (var oldToken in oldTokens)
        {
            oldToken.UsedAt = DateTime.UtcNow;
            _verificationTokenRepository.Update(oldToken);
        }

        await _verificationTokenRepository.SaveChangesAsync();
        await CreateAndSendVerificationTokenAsync(user, cancellationToken);
        return ApiResponse<object>.SuccessResponse(new { }, "Nếu tài khoản cần xác thực, email đã được gửi.");
    }

    public async Task<ApiResponse<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = _jwtService.HashToken(request.RefreshToken);
        var refreshToken = await _refreshTokenRepository.FirstOrDefaultAsync(item =>
            item.TokenHash == tokenHash && item.RevokedAt == null && item.ExpiresAt > DateTime.UtcNow);

        if (refreshToken is null)
        {
            return ApiResponse<AuthResponse>.Failure("Refresh token không hợp lệ hoặc đã hết hạn.");
        }

        var user = await _userRepository.GetByIdAsync(refreshToken.UserId);
        if (user is null || user.Status != UserStatus.Active)
        {
            return ApiResponse<AuthResponse>.Failure("Tài khoản không còn hoạt động.");
        }

        var newRawToken = _jwtService.GenerateSecureToken();
        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.ReplacedByTokenHash = _jwtService.HashToken(newRawToken);
        _refreshTokenRepository.Update(refreshToken);

        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshToken.ReplacedByTokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = _jwtService.GetRefreshTokenExpiry()
        });
        await _refreshTokenRepository.SaveChangesAsync();

        return await BuildAuthResponseAsync(user, newRawToken);
    }

    public async Task<ApiResponse<object>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = _jwtService.HashToken(request.RefreshToken);
        var token = await _refreshTokenRepository.FirstOrDefaultAsync(item => item.TokenHash == tokenHash && item.RevokedAt == null);

        if (token is not null)
        {
            token.RevokedAt = DateTime.UtcNow;
            _refreshTokenRepository.Update(token);
            await _refreshTokenRepository.SaveChangesAsync();
        }

        return ApiResponse<object>.SuccessResponse(new { }, "Đăng xuất thành công.");
    }

    private async Task<ApiResponse<AuthResponse>> CreateSessionForActiveUserAsync(User user, CancellationToken cancellationToken)
    {
        if (user.Status == UserStatus.PendingVerification)
        {
            return ApiResponse<AuthResponse>.Failure("Vui lòng xác thực email trước khi đăng nhập.");
        }

        if (user.Status != UserStatus.Active)
        {
            return ApiResponse<AuthResponse>.Failure("Tài khoản đang không hoạt động.");
        }

        var rawToken = _jwtService.GenerateSecureToken();
        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = _jwtService.HashToken(rawToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = _jwtService.GetRefreshTokenExpiry()
        });
        await _refreshTokenRepository.SaveChangesAsync();

        return await BuildAuthResponseAsync(user, rawToken);
    }

    private async Task<ApiResponse<AuthResponse>> BuildAuthResponseAsync(User user, string refreshToken)
    {
        var role = await _roleRepository.GetByIdAsync(user.RoleId);
        if (role is null)
        {
            return ApiResponse<AuthResponse>.Failure("Tài khoản chưa được gán role hợp lệ.");
        }

        var response = new AuthResponse(
            _jwtService.GenerateAccessToken(user.Id, user.Email, role.RoleName),
            refreshToken,
            _jwtService.GetAccessTokenExpiry(),
            role.RoleName,
            new UserResponse(user.Id, user.UserName, user.FullName, user.Email, role.RoleName, user.Status.ToString(), user.AvatarUrl));

        return ApiResponse<AuthResponse>.SuccessResponse(response, "Đăng nhập thành công.");
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
        await _verificationTokenRepository.AddAsync(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = _jwtService.HashToken(rawToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = _jwtService.GetEmailVerificationExpiry()
        });
        await _verificationTokenRepository.SaveChangesAsync();
        await _emailService.SendVerificationEmailAsync(user.Email, user.FullName, rawToken, cancellationToken);
    }
}

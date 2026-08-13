using System.Linq.Expressions;
using System.ComponentModel.DataAnnotations;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Auth;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceCore.Email;
using DomainLayer.InterfaceCore.External;
using DomainLayer.InterfaceCore.JWT;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Cores.JWTs;
using Microsoft.Extensions.Options;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class AuthenticationServiceTests
{
    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_DoesNotUpdateAccount()
    {
        var user = CreateUser(AuthProvider.Local, "customer@example.com");
        var fixture = CreateFixture(new[] { user });
        fixture.PasswordHasher
            .Setup(hasher => hasher.VerifyPassword("wrong-password", user.PasswordHash))
            .Returns(false);

        var exception = await Assert.ThrowsAsync<AppException>(() => fixture.Service.ChangePasswordAsync(
            user.Id,
            new ChangePasswordRequest
            {
                CurrentPassword = "wrong-password",
                NewPassword = "new-password",
                ConfirmNewPassword = "new-password"
            }));

        Assert.Equal(AuthErrorCodes.CurrentPasswordInvalid, exception.ErrorCode);
        fixture.Users.Verify(repository => repository.Update(It.IsAny<User>()), Times.Never);
        fixture.Users.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_ValidLocalAccount_RevokesRefreshToken()
    {
        var user = CreateUser(AuthProvider.Local, "customer@example.com");
        user.RefreshTokenHash = "refresh-hash";
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(1);
        var fixture = CreateFixture(new[] { user });
        fixture.PasswordHasher
            .Setup(hasher => hasher.VerifyPassword("current-password", user.PasswordHash))
            .Returns(true);
        fixture.PasswordHasher
            .Setup(hasher => hasher.VerifyPassword("new-password", user.PasswordHash))
            .Returns(false);
        fixture.PasswordHasher
            .Setup(hasher => hasher.HashPassword("new-password"))
            .Returns("new-hash");

        var response = await fixture.Service.ChangePasswordAsync(
            user.Id,
            new ChangePasswordRequest
            {
                CurrentPassword = "current-password",
                NewPassword = "new-password",
                ConfirmNewPassword = "new-password"
            });

        Assert.True(response.Success);
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Null(user.RefreshTokenHash);
        Assert.Null(user.RefreshTokenExpiresAt);
        fixture.Users.Verify(repository => repository.Update(user), Times.Once);
        fixture.Users.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflictWithoutCreatingUser()
    {
        var existingUser = CreateUser(AuthProvider.Local, "customer@example.com");
        var fixture = CreateFixture(new[] { existingUser });

        var exception = await Assert.ThrowsAsync<AppException>(() => fixture.Service.RegisterCustomerAsync(
            new RegisterCustomerRequest
            {
                Email = " Customer@Example.com ",
                UserName = "new-customer",
                FullName = "New Customer",
                Password = "password-123",
                ConfirmPassword = "password-123"
            }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.EmailAlreadyExists, exception.ErrorCode);
        fixture.Users.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsInvalidCredentials()
    {
        var user = CreateUser(AuthProvider.Local, "customer@example.com");
        var fixture = CreateFixture(new[] { user });
        fixture.PasswordHasher.Setup(hasher => hasher.VerifyPassword("wrong-password", user.PasswordHash))
            .Returns(false);

        var exception = await Assert.ThrowsAsync<AppException>(() => fixture.Service.LoginAsync(
            new LoginRequest { EmailOrUserName = user.Email, Password = "wrong-password" }));

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, exception.ErrorCode);
    }

    [Theory]
    [InlineData(UserStatus.Inactive, AuthErrorCodes.AccountNotActive)]
    [InlineData(UserStatus.Banned, AuthErrorCodes.AccountNotActive)]
    [InlineData(UserStatus.PendingVerification, AuthErrorCodes.EmailNotVerified)]
    public async Task Login_NonActiveAccount_IsRejected(UserStatus status, string expectedErrorCode)
    {
        var user = CreateUser(AuthProvider.Local, "customer@example.com");
        user.Status = status;
        var fixture = CreateFixture(new[] { user });
        fixture.PasswordHasher.Setup(hasher => hasher.VerifyPassword("correct-password", user.PasswordHash))
            .Returns(true);

        var exception = await Assert.ThrowsAsync<AppException>(() => fixture.Service.LoginAsync(
            new LoginRequest { EmailOrUserName = user.Email, Password = "correct-password" }));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal(expectedErrorCode, exception.ErrorCode);
    }

    [Fact]
    public async Task VerifyEmail_PendingAccount_ActivatesAndConsumesToken()
    {
        var user = CreateUser(AuthProvider.Local, "customer@example.com");
        user.Status = UserStatus.PendingVerification;
        user.EmailVerificationTokenHash = "verification-hash";
        user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(5);
        var fixture = CreateFixture(new[] { user });
        fixture.Jwt.Setup(service => service.HashToken("verification-token")).Returns("verification-hash");

        var response = await fixture.Service.VerifyEmailAsync("verification-token");

        Assert.True(response.Success);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Null(user.EmailVerificationTokenHash);
        Assert.Null(user.EmailVerificationTokenExpiresAt);
        fixture.Users.Verify(repository => repository.Update(user), Times.Once);
        fixture.Users.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task VerifyEmail_InactiveAccount_DoesNotReactivateAccount()
    {
        var user = CreateUser(AuthProvider.Local, "customer@example.com");
        user.Status = UserStatus.Inactive;
        user.EmailVerificationTokenHash = "verification-hash";
        user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(5);
        var fixture = CreateFixture(new[] { user });
        fixture.Jwt.Setup(service => service.HashToken("verification-token")).Returns("verification-hash");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.VerifyEmailAsync("verification-token"));

        Assert.Equal(AuthErrorCodes.InvalidOrExpiredVerificationToken, exception.ErrorCode);
        Assert.Equal(UserStatus.Inactive, user.Status);
        fixture.Users.Verify(repository => repository.Update(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task GoogleLogin_ExistingLocalEmail_DoesNotAutoLinkAccount()
    {
        var localUser = CreateUser(AuthProvider.Local, "customer@example.com");
        var fixture = CreateFixture(new[] { localUser });
        fixture.Google.Setup(service => service.ValidateAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleUserInfo("google-subject", localUser.Email, localUser.FullName, null));

        var exception = await Assert.ThrowsAsync<AppException>(() => fixture.Service.GoogleLoginAsync(
            new GoogleLoginRequest { IdToken = "valid-token" }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.GoogleAccountLinkRequired, exception.ErrorCode);
        Assert.Null(localUser.GoogleId);
        fixture.Users.Verify(repository => repository.Update(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task GoogleLogin_NonCustomerGoogleAccount_IsForbidden()
    {
        var googleUser = CreateUser(AuthProvider.Google, "admin@example.com", "google-subject");
        var fixture = CreateFixture(new[] { googleUser });
        fixture.Google.Setup(service => service.ValidateAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleUserInfo("google-subject", googleUser.Email, googleUser.FullName, null));
        fixture.Roles.Setup(repository => repository.GetByIdAsync(googleUser.RoleId))
            .ReturnsAsync(new Role { Id = googleUser.RoleId, RoleName = "Admin" });

        var exception = await Assert.ThrowsAsync<AppException>(() => fixture.Service.GoogleLoginAsync(
            new GoogleLoginRequest { IdToken = "valid-token" }));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.GoogleCustomerOnly, exception.ErrorCode);
    }

    [Fact]
    public async Task GoogleLogin_InvalidCredential_IsUnauthorized()
    {
        var fixture = CreateFixture(Array.Empty<User>());
        fixture.Google.Setup(service => service.ValidateAsync("invalid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GoogleUserInfo?)null);

        var exception = await Assert.ThrowsAsync<AppException>(() => fixture.Service.GoogleLoginAsync(
            new GoogleLoginRequest { IdToken = "invalid-token" }));

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.InvalidGoogleToken, exception.ErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResetPassword_InvalidExpiredOrUsedOtp_IsRejected(bool expired)
    {
        var user = CreateUser(AuthProvider.Local, "customer@example.com");
        user.PasswordResetOtpHash = expired ? "otp-hash" : null;
        user.PasswordResetOtpExpiresAt = expired ? DateTime.UtcNow.AddMinutes(-1) : null;
        var fixture = CreateFixture(new[] { user });
        fixture.Jwt.Setup(service => service.HashToken("123456")).Returns("otp-hash");
        fixture.PasswordHasher.Setup(hasher => hasher.VerifyPassword("new-password", user.PasswordHash))
            .Returns(false);

        var exception = await Assert.ThrowsAsync<AppException>(() => fixture.Service.ResetPasswordAsync(
            new ResetPasswordRequest
            {
                Email = user.Email,
                Otp = "123456",
                NewPassword = "new-password",
                ConfirmNewPassword = "new-password"
            }));

        Assert.Equal(AuthErrorCodes.InvalidOrExpiredResetOtp, exception.ErrorCode);
        fixture.Users.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefreshToken_RevokedOrExpired_IsUnauthorized(bool expired)
    {
        var user = CreateUser(AuthProvider.Local, "customer@example.com");
        user.RefreshTokenHash = expired ? "refresh-hash" : null;
        user.RefreshTokenExpiresAt = expired ? DateTime.UtcNow.AddMinutes(-1) : null;
        var fixture = CreateFixture(new[] { user });
        fixture.Jwt.Setup(service => service.HashToken("refresh-token")).Returns("refresh-hash");

        var exception = await Assert.ThrowsAsync<AppException>(() => fixture.Service.RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = "refresh-token" }));

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.InvalidOrExpiredRefreshToken, exception.ErrorCode);
    }

    [Fact]
    public void GenerateNumericCode_ReturnsRequestedCryptographicCodeShape()
    {
        var service = new JWTService(Options.Create(new JwtSettings()));

        for (var index = 0; index < 100; index++)
        {
            var code = service.GenerateNumericCode(6);
            Assert.Matches("^[0-9]{6}$", code);
        }
    }

    [Fact]
    public void PasswordRequests_UseOneConsistentLengthPolicy()
    {
        var passwordProperties = new[]
        {
            typeof(RegisterRequest).GetProperty(nameof(RegisterRequest.Password)),
            typeof(ResetPasswordRequest).GetProperty(nameof(ResetPasswordRequest.NewPassword)),
            typeof(ResetPasswordByTokenRequest).GetProperty(nameof(ResetPasswordByTokenRequest.NewPassword)),
            typeof(ChangePasswordRequest).GetProperty(nameof(ChangePasswordRequest.NewPassword))
        };

        foreach (var property in passwordProperties)
        {
            var policy = Assert.Single(property!.GetCustomAttributes(typeof(StringLengthAttribute), inherit: true))
                as StringLengthAttribute;
            Assert.NotNull(policy);
            Assert.Equal(8, policy.MinimumLength);
            Assert.Equal(128, policy.MaximumLength);
        }
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsGenericSuccessWithoutSending()
    {
        var fixture = CreateFixture(Array.Empty<User>());

        var response = await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = " missing@example.com " });

        Assert.True(response.Success);
        Assert.Contains("If the email exists", response.Message, StringComparison.Ordinal);
        fixture.Email.Verify(
            service => service.SendPasswordResetOtpAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.Email.Verify(
            service => service.SendPasswordResetLinkAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_EmailNotConfigured_FailsBeforeUserLookup()
    {
        var user = CreateUser(AuthProvider.Local, "customer@example.com");
        var fixture = CreateFixture(new[] { user });
        fixture.Email.Setup(service => service.CanSendPasswordResetEmail()).Returns(false);

        var exception = await Assert.ThrowsAsync<AppException>(() => fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = user.Email }));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.EmailDeliveryFailed, exception.ErrorCode);
        fixture.Users.Verify(
            repository => repository.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()),
            Times.Never);
        fixture.Email.Verify(
            service => service.SendPasswordResetOtpAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_ActiveLocalUser_SendsOtpAndOptionalLink()
    {
        var user = CreateUser(AuthProvider.Local, "customer@example.com");
        var fixture = CreateFixture(new[] { user });
        fixture.Jwt.Setup(service => service.GenerateNumericCode(6)).Returns("123456");
        fixture.Jwt.Setup(service => service.GenerateSecureToken()).Returns("reset-token");
        fixture.Jwt.Setup(service => service.HashToken("123456")).Returns("otp-hash");
        fixture.Jwt.Setup(service => service.HashToken("reset-token")).Returns("reset-hash");

        var response = await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = " Customer@Example.com " });

        Assert.True(response.Success);
        Assert.Equal("otp-hash", user.PasswordResetOtpHash);
        Assert.Equal("reset-hash", user.PasswordResetTokenHash);
        fixture.Users.Verify(repository => repository.SaveChangesAsync(), Times.Once);
        fixture.Email.Verify(
            service => service.SendPasswordResetOtpAsync(
                user.Email,
                user.FullName,
                "123456",
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Email.Verify(
            service => service.SendPasswordResetLinkAsync(
                user.Email,
                user.FullName,
                "reset-token",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_SendFailure_StillReturnsGenericSuccess()
    {
        var user = CreateUser(AuthProvider.Local, "customer@example.com");
        var fixture = CreateFixture(new[] { user });
        fixture.Jwt.Setup(service => service.GenerateNumericCode(6)).Returns("123456");
        fixture.Jwt.Setup(service => service.GenerateSecureToken()).Returns("reset-token");
        fixture.Jwt.Setup(service => service.HashToken(It.IsAny<string>())).Returns("hash");
        fixture.Email
            .Setup(service => service.SendPasswordResetOtpAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

        var response = await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = user.Email });

        Assert.True(response.Success);
        Assert.Contains("If the email exists", response.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResendVerification_UnknownOrActiveAccount_DoesNotSend()
    {
        var active = CreateUser(AuthProvider.Local, "active@example.com");
        var fixture = CreateFixture(new[] { active });

        var unknown = await fixture.Service.ResendVerificationAsync(
            new ResendVerificationRequest { Email = "missing@example.com" });
        var alreadyActive = await fixture.Service.ResendVerificationAsync(
            new ResendVerificationRequest { Email = active.Email });

        Assert.True(unknown.Success);
        Assert.True(alreadyActive.Success);
        fixture.Email.Verify(
            service => service.SendVerificationEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResendVerification_PendingAccount_SendsNewToken()
    {
        var user = CreateUser(AuthProvider.Local, "pending@example.com");
        user.Status = UserStatus.PendingVerification;
        var fixture = CreateFixture(new[] { user });
        fixture.Jwt.Setup(service => service.GenerateSecureToken()).Returns("verify-token");
        fixture.Jwt.Setup(service => service.HashToken("verify-token")).Returns("verify-hash");
        fixture.Jwt.Setup(service => service.GetEmailVerificationExpiry()).Returns(DateTime.UtcNow.AddHours(24));

        var response = await fixture.Service.ResendVerificationAsync(
            new ResendVerificationRequest { Email = " Pending@Example.com " });

        Assert.True(response.Success);
        Assert.Equal("verify-hash", user.EmailVerificationTokenHash);
        fixture.Email.Verify(
            service => service.SendVerificationEmailAsync(
                user.Email,
                user.FullName,
                "verify-token",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResendVerification_EmailNotConfigured_FailsBeforeUserLookup()
    {
        var fixture = CreateFixture(Array.Empty<User>());
        fixture.Email.Setup(service => service.CanSendVerificationEmail()).Returns(false);

        var exception = await Assert.ThrowsAsync<AppException>(() => fixture.Service.ResendVerificationAsync(
            new ResendVerificationRequest { Email = "pending@example.com" }));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.EmailDeliveryFailed, exception.ErrorCode);
        fixture.Users.Verify(
            repository => repository.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()),
            Times.Never);
    }

    [Fact]
    public async Task Register_EmailNotConfigured_DoesNotCreateUser()
    {
        var fixture = CreateFixture(Array.Empty<User>());
        fixture.Email.Setup(service => service.CanSendVerificationEmail()).Returns(false);

        var exception = await Assert.ThrowsAsync<AppException>(() => fixture.Service.RegisterCustomerAsync(
            new RegisterCustomerRequest
            {
                Email = "new@example.com",
                UserName = "new-customer",
                FullName = "New Customer",
                Password = "password-123",
                ConfirmPassword = "password-123"
            }));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.EmailDeliveryFailed, exception.ErrorCode);
        fixture.Users.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Register_EmailSendFailure_ReturnsEmailDeliveryFailedAfterCreatingPendingUser()
    {
        var fixture = CreateFixture(Array.Empty<User>());
        fixture.Roles
            .Setup(repository => repository.FirstOrDefaultAsync(It.IsAny<Expression<Func<Role, bool>>>()))
            .ReturnsAsync(new Role { Id = Guid.NewGuid(), RoleName = "Customer" });
        fixture.Jwt.Setup(service => service.GenerateSecureToken()).Returns("verify-token");
        fixture.Jwt.Setup(service => service.HashToken("verify-token")).Returns("verify-hash");
        fixture.Jwt.Setup(service => service.GetEmailVerificationExpiry()).Returns(DateTime.UtcNow.AddHours(24));
        fixture.Email
            .Setup(service => service.SendVerificationEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

        var exception = await Assert.ThrowsAsync<AppException>(() => fixture.Service.RegisterCustomerAsync(
            new RegisterCustomerRequest
            {
                Email = "new@example.com",
                UserName = "new-customer",
                FullName = "New Customer",
                Password = "password-123",
                ConfirmPassword = "password-123"
            }));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.EmailDeliveryFailed, exception.ErrorCode);
        fixture.Users.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Once);
        fixture.Users.Verify(repository => repository.SaveChangesAsync(), Times.AtLeastOnce);
    }

    private static AuthFixture CreateFixture(IReadOnlyCollection<User> users)
    {
        var userRepository = new Mock<IGenericRepository<User>>();
        userRepository
            .Setup(repository => repository.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>() ))
            .ReturnsAsync((Expression<Func<User, bool>> predicate) => users.SingleOrDefault(predicate.Compile()));
        userRepository
            .Setup(repository => repository.AnyAsync(It.IsAny<Expression<Func<User, bool>>>() ))
            .ReturnsAsync((Expression<Func<User, bool>> predicate) => users.Any(predicate.Compile()));
        userRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => users.SingleOrDefault(user => user.Id == id));

        var roleRepository = new Mock<IGenericRepository<Role>>();
        var passwordHasher = new Mock<IPasswordHasher>();
        var jwtService = new Mock<IJwtService>();
        var emailService = new Mock<IEmailService>();
        emailService.Setup(service => service.CanSendVerificationEmail()).Returns(true);
        emailService.Setup(service => service.CanSendPasswordResetEmail()).Returns(true);
        var googleValidator = new Mock<IGoogleTokenValidator>();
        var deviceTokens = new Mock<IUserDeviceTokenRepository>();
        var mapper = new Mock<IMapper>();

        var service = new AuthService(
            userRepository.Object,
            roleRepository.Object,
            passwordHasher.Object,
            jwtService.Object,
            emailService.Object,
            googleValidator.Object,
            deviceTokens.Object,
            mapper.Object);

        return new AuthFixture(
            service,
            userRepository,
            roleRepository,
            passwordHasher,
            jwtService,
            emailService,
            googleValidator);
    }

    private static User CreateUser(AuthProvider provider, string email, string? googleId = null)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            RoleId = Guid.NewGuid(),
            UserName = "test-user",
            FullName = "Test User",
            Email = email,
            PasswordHash = "hash",
            GoogleId = googleId,
            AuthProvider = provider,
            Status = UserStatus.Active
        };
    }

    private sealed record AuthFixture(
        AuthService Service,
        Mock<IGenericRepository<User>> Users,
        Mock<IGenericRepository<Role>> Roles,
        Mock<IPasswordHasher> PasswordHasher,
        Mock<IJwtService> Jwt,
        Mock<IEmailService> Email,
        Mock<IGoogleTokenValidator> Google);
}

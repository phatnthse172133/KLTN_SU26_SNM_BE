using System.Linq.Expressions;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Auth;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceCore.Email;
using DomainLayer.InterfaceCore.External;
using DomainLayer.InterfaceCore.JWT;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static DomainLayer.Enums.GeneralEnum;

namespace AuthenticationTests;

public sealed class GoogleLoginTests
{
    private static readonly Guid CustomerRoleId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminRoleId = Guid.Parse("10000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task ExistingVerifiedCustomerWithSameEmail_LinksAndLogsIn()
    {
        var user = LocalCustomer("Customer@Example.com");
        var passwordHash = user.PasswordHash;
        var fixture = CreateFixture([user], Google("google-subject", "CUSTOMER@example.com"));

        var response = await fixture.Service.GoogleLoginAsync(new() { IdToken = "valid-token" });

        Assert.True(response.Success);
        Assert.Equal("google-subject", user.GoogleId);
        Assert.Equal(AuthProvider.LocalGoogle, user.AuthProvider);
        Assert.Equal(passwordHash, user.PasswordHash);
        Assert.Single(fixture.Users);
    }

    [Fact]
    public async Task ExistingLinkedCustomerWithSameSubject_LogsInWithoutCreatingUser()
    {
        var user = LocalCustomer("customer@example.com");
        user.GoogleId = "google-subject";
        user.AuthProvider = AuthProvider.LocalGoogle;
        var fixture = CreateFixture([user], Google("google-subject", user.Email));

        var response = await fixture.Service.GoogleLoginAsync(new() { IdToken = "valid-token" });

        Assert.True(response.Success);
        Assert.Single(fixture.Users);
        fixture.UserRepository.Verify(repository => repository.TryAddGoogleUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NewVerifiedGoogleCustomer_IsCreatedExactlyOnce()
    {
        var fixture = CreateFixture([], Google("new-subject", "NEW@example.com"));

        var response = await fixture.Service.GoogleLoginAsync(new() { IdToken = "valid-token" });

        var user = Assert.Single(fixture.Users);
        Assert.True(response.Success);
        Assert.Equal("new@example.com", user.Email);
        Assert.Equal("new-subject", user.GoogleId);
        Assert.Equal(AuthProvider.Google, user.AuthProvider);
        Assert.Null(user.PasswordHash);
        Assert.Equal(UserStatus.Active, user.Status);
        fixture.UserRepository.Verify(repository => repository.TryAddGoogleUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConcurrentSameGoogleCustomer_ResolvesWinnerWithoutDuplicate()
    {
        var fixture = CreateFixture([], Google("new-subject", "new@example.com"));
        fixture.UserRepository
            .Setup(repository => repository.TryAddGoogleUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User _, CancellationToken _) =>
            {
                var winner = LocalCustomer("new@example.com");
                winner.GoogleId = "new-subject";
                winner.AuthProvider = AuthProvider.Google;
                winner.PasswordHash = null;
                fixture.Users.Add(winner);
                return false;
            });

        var response = await fixture.Service.GoogleLoginAsync(new() { IdToken = "valid-token" });

        Assert.True(response.Success);
        Assert.Single(fixture.Users);
        Assert.Equal("new-subject", fixture.Users[0].GoogleId);
    }

    [Fact]
    public async Task ExistingNonCustomerEmail_IsRejected()
    {
        var user = LocalCustomer("admin@example.com");
        user.RoleId = AdminRoleId;
        var fixture = CreateFixture([user], Google("new-subject", user.Email));

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GoogleLoginAsync(new() { IdToken = "valid-token" }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.GoogleCustomerOnly, exception.ErrorCode);
        Assert.Null(user.GoogleId);
    }

    [Theory]
    [InlineData(UserStatus.Inactive)]
    [InlineData(UserStatus.Banned)]
    public async Task DisabledCustomer_IsRejectedWithoutLinking(UserStatus status)
    {
        var user = LocalCustomer("inactive@example.com");
        user.Status = status;
        var fixture = CreateFixture([user], Google("new-subject", user.Email));

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GoogleLoginAsync(new() { IdToken = "valid-token" }));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.AccountNotActive, exception.ErrorCode);
        Assert.Null(user.GoogleId);
    }

    [Fact]
    public async Task PendingCustomer_IsRejectedWithoutActivatingOrLinking()
    {
        var user = LocalCustomer("pending@example.com");
        user.Status = UserStatus.PendingVerification;
        var fixture = CreateFixture([user], Google("new-subject", user.Email));

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GoogleLoginAsync(new() { IdToken = "valid-token" }));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.EmailNotVerified, exception.ErrorCode);
        Assert.Equal(UserStatus.PendingVerification, user.Status);
        Assert.Null(user.GoogleId);
    }

    [Fact]
    public async Task InvalidGoogleToken_IsRejected()
    {
        var fixture = CreateFixture([], googleUser: null);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GoogleLoginAsync(new() { IdToken = "invalid-token" }));

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.InvalidGoogleToken, exception.ErrorCode);
        Assert.Empty(fixture.Users);
    }

    [Fact]
    public async Task UnverifiedGoogleEmail_IsRejected()
    {
        var fixture = CreateFixture([], Google("new-subject", "customer@example.com", emailVerified: false));

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GoogleLoginAsync(new() { IdToken = "valid-token" }));

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.InvalidGoogleToken, exception.ErrorCode);
        Assert.Empty(fixture.Users);
    }

    [Fact]
    public async Task ExistingEmailLinkedToDifferentSubject_IsRejectedWithoutRelinking()
    {
        var user = LocalCustomer("customer@example.com");
        user.GoogleId = "original-subject";
        user.AuthProvider = AuthProvider.LocalGoogle;
        var fixture = CreateFixture([user], Google("attacker-subject", user.Email));

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GoogleLoginAsync(new() { IdToken = "valid-token" }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.GoogleSubjectConflict, exception.ErrorCode);
        Assert.Equal("original-subject", user.GoogleId);
    }

    [Fact]
    public async Task ConcurrentDifferentSubjectLink_IsRejectedWithoutOverwrite()
    {
        var user = LocalCustomer("customer@example.com");
        var fixture = CreateFixture([user], Google("requested-subject", user.Email));
        fixture.UserRepository
            .Setup(repository => repository.TryLinkGoogleIdentityAsync(
                user.Id,
                "requested-subject",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                user.GoogleId = "winning-subject";
                user.AuthProvider = AuthProvider.LocalGoogle;
                return false;
            });

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GoogleLoginAsync(new() { IdToken = "valid-token" }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.GoogleSubjectConflict, exception.ErrorCode);
        Assert.Equal("winning-subject", user.GoogleId);
    }

    [Fact]
    public async Task GoogleSubjectAndVerifiedEmailResolveToDifferentUsers_IsRejected()
    {
        var subjectOwner = LocalCustomer("subject-owner@example.com");
        subjectOwner.GoogleId = "shared-subject";
        subjectOwner.AuthProvider = AuthProvider.LocalGoogle;
        var emailOwner = LocalCustomer("email-owner@example.com");
        var fixture = CreateFixture(
            [subjectOwner, emailOwner],
            Google("shared-subject", emailOwner.Email));

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GoogleLoginAsync(new() { IdToken = "valid-token" }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(AuthErrorCodes.GoogleSubjectConflict, exception.ErrorCode);
        Assert.Null(emailOwner.GoogleId);
        Assert.Equal("shared-subject", subjectOwner.GoogleId);
    }

    [Fact]
    public async Task LinkingGoogle_PreservesPasswordHashAndPasswordProvider()
    {
        var user = LocalCustomer("customer@example.com");
        user.PasswordHash = "important-existing-password-hash";
        var fixture = CreateFixture([user], Google("google-subject", user.Email));
        fixture.PasswordHasher
            .Setup(hasher => hasher.VerifyPassword("valid-password", "important-existing-password-hash"))
            .Returns(true);

        await fixture.Service.GoogleLoginAsync(new() { IdToken = "valid-token" });
        var passwordSession = await fixture.Service.LoginAsync(new()
        {
            EmailOrUserName = user.Email,
            Password = "valid-password"
        });

        Assert.Equal("important-existing-password-hash", user.PasswordHash);
        Assert.Equal(AuthProvider.LocalGoogle, user.AuthProvider);
        Assert.True(passwordSession.Success);
        fixture.PasswordHasher.Verify(
            hasher => hasher.VerifyPassword("valid-password", "important-existing-password-hash"),
            Times.Once);
        fixture.PasswordHasher.Verify(
            hasher => hasher.HashPassword(It.IsAny<string>()),
            Times.Never);
    }

    private static Fixture CreateFixture(List<User> users, GoogleUserInfo? googleUser)
    {
        var customerRole = new Role { Id = CustomerRoleId, RoleName = "Customer" };
        var adminRole = new Role { Id = AdminRoleId, RoleName = "Admin" };

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>() ))
            .ReturnsAsync((Expression<Func<User, bool>> predicate) => users.FirstOrDefault(predicate.Compile()));
        userRepository
            .Setup(repository => repository.TryAddGoogleUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User user, CancellationToken _) =>
            {
                if (users.Any(existing =>
                        existing.GoogleId == user.GoogleId ||
                        string.Equals(existing.Email.Trim(), user.Email.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                users.Add(user);
                return true;
            });
        userRepository.Setup(repository => repository.Update(It.IsAny<User>()));
        userRepository.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);
        userRepository
            .Setup(repository => repository.TryLinkGoogleIdentityAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, string googleId, DateTime updatedAt, CancellationToken _) =>
            {
                var user = users.First(candidate => candidate.Id == userId);
                if (user.Status != UserStatus.Active || user.GoogleId is not null) return false;
                user.GoogleId = googleId;
                user.UpdatedAt = updatedAt;
                return true;
            });
        userRepository
            .Setup(repository => repository.ReloadAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        var roleRepository = new Mock<IGenericRepository<Role>>(MockBehavior.Strict);
        roleRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => id == CustomerRoleId ? customerRole : id == AdminRoleId ? adminRole : null);
        roleRepository
            .Setup(repository => repository.FirstOrDefaultAsync(It.IsAny<Expression<Func<Role, bool>>>() ))
            .ReturnsAsync((Expression<Func<Role, bool>> predicate) => new[] { customerRole, adminRole }.FirstOrDefault(predicate.Compile()));

        var google = new Mock<IGoogleTokenValidator>(MockBehavior.Strict);
        google
            .Setup(validator => validator.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(googleUser);

        var jwt = new Mock<IJwtService>(MockBehavior.Strict);
        jwt.Setup(service => service.GenerateSecureToken()).Returns("refresh-token");
        jwt.Setup(service => service.HashToken("refresh-token")).Returns("refresh-token-hash");
        jwt.Setup(service => service.GetRefreshTokenExpiry()).Returns(DateTime.UtcNow.AddDays(30));
        jwt.Setup(service => service.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>())).Returns("access-token");
        jwt.Setup(service => service.GetAccessTokenExpiry()).Returns(DateTime.UtcNow.AddMinutes(15));

        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        mapper
            .Setup(value => value.Map<UserResponse>(It.IsAny<User>()))
            .Returns((User user) => new UserResponse
            {
                Id = user.Id,
                UserName = user.UserName,
                FullName = user.FullName,
                Email = user.Email,
                Status = user.Status.ToString()
            });

        var passwordHasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        var deviceTokens = new Mock<IUserDeviceTokenRepository>(MockBehavior.Strict);
        var logger = new Mock<ILogger<AuthService>>();

        var service = new AuthService(
            userRepository.Object,
            roleRepository.Object,
            passwordHasher.Object,
            jwt.Object,
            email.Object,
            google.Object,
            deviceTokens.Object,
            mapper.Object,
            logger.Object);

        return new Fixture(service, users, userRepository, passwordHasher);
    }

    private static User LocalCustomer(string email) => new()
    {
        Id = Guid.NewGuid(),
        RoleId = CustomerRoleId,
        UserName = $"customer_{Guid.NewGuid():N}"[..20],
        FullName = "Customer",
        Email = email,
        PasswordHash = "existing-password-hash",
        AuthProvider = AuthProvider.Local,
        Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static GoogleUserInfo Google(string subject, string email, bool emailVerified = true) =>
        new(subject, email, "Google Customer", null, emailVerified);

    private sealed record Fixture(
        AuthService Service,
        List<User> Users,
        Mock<IUserRepository> UserRepository,
        Mock<IPasswordHasher> PasswordHasher);
}

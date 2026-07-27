using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using ApplicationLayer.Services.Account;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;
using ApplicationLayer.Exceptions;
using Microsoft.Extensions.Logging;
using ApplicationLayer.Services.Storage;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

namespace TestingLayer
{
    public class AccountServiceTests
    {
        private readonly Mock<IUserRepository> _mockUsers;
        private readonly Mock<IGenericRepository<Role>> _mockRoles;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<INotificationService> _mockNotifications;
        private readonly Mock<IGenericRepository<UserStatusHistory>> _mockHistory;
        private readonly Mock<IGenericRepository<EmailOutbox>> _mockOutbox;
        private readonly Mock<ILogger<AccountService>> _mockLogger;
        private readonly Mock<IFileStorageService> _mockFileStorage;
        private readonly AccountService _accountService;

        public AccountServiceTests()
        {
            _mockUsers = new Mock<IUserRepository>();
            _mockRoles = new Mock<IGenericRepository<Role>>();
            _mockMapper = new Mock<IMapper>();
            _mockNotifications = new Mock<INotificationService>();
            _mockHistory = new Mock<IGenericRepository<UserStatusHistory>>();
            _mockOutbox = new Mock<IGenericRepository<EmailOutbox>>();
            _mockLogger = new Mock<ILogger<AccountService>>();
            _mockFileStorage = new Mock<IFileStorageService>();

            _accountService = new AccountService(
                _mockUsers.Object,
                _mockRoles.Object,
                _mockMapper.Object,
                _mockNotifications.Object,
                _mockHistory.Object,
                _mockOutbox.Object,
                _mockLogger.Object,
                _mockFileStorage.Object
            );
        }

        [Fact]
        public async Task ChangeUserStatusAsync_AdminCannotBanSelf()
        {
            var adminId = Guid.NewGuid();
            var request = new ChangeUserStatusRequest { Status = UserStatus.Inactive, Reason = "Validation should fail" };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _accountService.ChangeUserStatusAsync(adminId, adminId, request));

            Assert.Contains("cannot change the status of their own account", ex.Message);
        }

        [Fact]
        public async Task ChangeUserStatusAsync_ReasonTooShort_ThrowsException()
        {
            var adminId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var request = new ChangeUserStatusRequest { Status = UserStatus.Inactive, Reason = "short" };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _accountService.ChangeUserStatusAsync(adminId, targetId, request));

            Assert.Contains("must be at least 10 characters", ex.Message);
        }

        [Fact]
        public async Task ChangeUserStatusAsync_InvalidStatusTransition_ThrowsException()
        {
            var adminId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var user = new User { Id = targetId, Status = UserStatus.Inactive };
            var request = new ChangeUserStatusRequest { Status = UserStatus.Inactive, Reason = "Valid reason length here" };

            _mockUsers.Setup(repo => repo.GetByIdAsync(targetId)).ReturnsAsync(user);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _accountService.ChangeUserStatusAsync(adminId, targetId, request));

            Assert.Contains("Account is already Inactive.", ex.Message);
        }

        [Fact]
        public async Task ChangeUserStatusAsync_SameStatus_ThrowsException()
        {
            var adminId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var user = new User { Id = targetId, Status = UserStatus.Active };
            var request = new ChangeUserStatusRequest { Status = UserStatus.Active, Reason = "Valid reason length here" };

            _mockUsers.Setup(repo => repo.GetByIdAsync(targetId)).ReturnsAsync(user);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _accountService.ChangeUserStatusAsync(adminId, targetId, request));

            Assert.Contains("Account is already Active", ex.Message);
        }

        [Fact]
        public async Task ChangeUserStatusAsync_ValidBan_RevokesTokensAndCreatesOutbox()
        {
            var adminId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var user = new User { Id = targetId, Status = UserStatus.Active, RefreshTokenHash = "hash", RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(1) };
            var request = new ChangeUserStatusRequest { Status = UserStatus.Inactive, Reason = "Valid reason length here for ban" };

            _mockUsers.Setup(repo => repo.GetByIdAsync(targetId)).ReturnsAsync(user);
            _mockUsers.Setup(repo => repo.UpdateStatusWithConcurrencyAsync(targetId, UserStatus.Active, UserStatus.Inactive, It.IsAny<DateTime>()))
                .Callback<Guid, UserStatus, UserStatus, DateTime>((id, oldS, newS, date) => {
                    user.Status = newS;
                    user.RefreshTokenHash = null;
                    user.RefreshTokenExpiresAt = null;
                })
                .ReturnsAsync(1);
            _mockHistory.Setup(repo => repo.AddAsync(It.IsAny<UserStatusHistory>())).Returns(Task.CompletedTask);
            _mockOutbox.Setup(repo => repo.AddAsync(It.IsAny<EmailOutbox>())).Returns(Task.CompletedTask);
            _mockUsers.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);
            _mockMapper.Setup(m => m.Map<ManagedUserResponse>(It.IsAny<User>())).Returns(new ManagedUserResponse());

            await _accountService.ChangeUserStatusAsync(adminId, targetId, request);

            Assert.Null(user.RefreshTokenHash);
            Assert.Null(user.RefreshTokenExpiresAt);
            Assert.Equal(UserStatus.Inactive, user.Status);

            _mockHistory.Verify(r => r.AddAsync(It.Is<UserStatusHistory>(h => h.NewStatus == UserStatus.Inactive && h.PreviousStatus == UserStatus.Active)), Times.Once);
            _mockOutbox.Verify(r => r.AddAsync(It.Is<EmailOutbox>(e => e.EmailType == "AccountBanned" && e.Status == "Pending")), Times.Once);
            _mockUsers.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ChangeUserStatusAsync_NotificationFails_DoesNotThrow()
        {
            var adminId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var user = new User { Id = targetId, Status = UserStatus.Active };
            var request = new ChangeUserStatusRequest { Status = UserStatus.Inactive, Reason = "Valid reason length here for ban" };

            _mockUsers.Setup(repo => repo.GetByIdAsync(targetId)).ReturnsAsync(user);
            _mockUsers.Setup(repo => repo.UpdateStatusWithConcurrencyAsync(targetId, UserStatus.Active, UserStatus.Inactive, It.IsAny<DateTime>()))
                .Callback<Guid, UserStatus, UserStatus, DateTime>((id, oldS, newS, date) => {
                    user.Status = newS;
                })
                .ReturnsAsync(1);
            _mockUsers.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);
            _mockNotifications.Setup(n => n.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Notification service down"));
            _mockMapper.Setup(m => m.Map<ManagedUserResponse>(It.IsAny<User>())).Returns(new ManagedUserResponse());

            var response = await _accountService.ChangeUserStatusAsync(adminId, targetId, request);

            Assert.NotNull(response);
            Assert.Equal("Account status updated successfully.", response.Message);
        }
        [Fact]
        public async Task ChangeUserStatusAsync_ValidUnban_CreatesOutbox()
        {
            var adminId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var user = new User { Id = targetId, Status = UserStatus.Inactive };
            var request = new ChangeUserStatusRequest { Status = UserStatus.Active, Reason = "Valid reason length here for unban" };

            _mockUsers.Setup(repo => repo.GetByIdAsync(targetId)).ReturnsAsync(user);
            _mockUsers.Setup(repo => repo.UpdateStatusWithConcurrencyAsync(targetId, UserStatus.Inactive, UserStatus.Active, It.IsAny<DateTime>()))
                .Callback<Guid, UserStatus, UserStatus, DateTime>((id, oldS, newS, date) => {
                    user.Status = newS;
                })
                .ReturnsAsync(1);
            _mockHistory.Setup(repo => repo.AddAsync(It.IsAny<UserStatusHistory>())).Returns(Task.CompletedTask);
            _mockOutbox.Setup(repo => repo.AddAsync(It.IsAny<EmailOutbox>())).Returns(Task.CompletedTask);
            _mockUsers.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);
            _mockMapper.Setup(m => m.Map<ManagedUserResponse>(It.IsAny<User>())).Returns(new ManagedUserResponse());

            await _accountService.ChangeUserStatusAsync(adminId, targetId, request);

            Assert.Equal(UserStatus.Active, user.Status);
            _mockHistory.Verify(r => r.AddAsync(It.Is<UserStatusHistory>(h => h.NewStatus == UserStatus.Active && h.PreviousStatus == UserStatus.Inactive)), Times.Once);
            _mockOutbox.Verify(r => r.AddAsync(It.Is<EmailOutbox>(e => e.EmailType == "AccountUnbanned" && e.Status == "Pending")), Times.Once);
        }

        [Fact]
        public async Task ChangeUserStatusAsync_UserNotFound_ThrowsException()
        {
            var adminId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var request = new ChangeUserStatusRequest { Status = UserStatus.Inactive, Reason = "Valid reason length here" };

            _mockUsers.Setup(repo => repo.GetByIdAsync(targetId)).ReturnsAsync((User)null!);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _accountService.ChangeUserStatusAsync(adminId, targetId, request));

            Assert.Contains("Account was not found.", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("short")]
        public async Task ChangeUserStatusAsync_ReasonInvalid_ThrowsException(string? reason)
        {
            var adminId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var request = new ChangeUserStatusRequest { Status = UserStatus.Inactive, Reason = reason! };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _accountService.ChangeUserStatusAsync(adminId, targetId, request));

            Assert.Contains("Reason is required and must be at least 10 characters", ex.Message);
        }

        [Fact]
        public async Task ChangeUserStatusAsync_ReasonTooLong_ThrowsException()
        {
            var adminId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var reason = new string('A', 1001);
            var request = new ChangeUserStatusRequest { Status = UserStatus.Inactive, Reason = reason };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _accountService.ChangeUserStatusAsync(adminId, targetId, request));

            Assert.Contains("Reason must not exceed 1000 characters", ex.Message);
        }

        [Fact]
        public async Task GetUserStatusHistoryAsync_ReturnsPagedResult()
        {
            var userId = Guid.NewGuid();
            var req = new PaginationReq { Page = 1, PageSize = 10 };

            var histories = new List<UserStatusHistory>
            {
                new UserStatusHistory { Id = Guid.NewGuid(), UserId = userId, ChangedByAdminId = Guid.NewGuid(), PreviousStatus = UserStatus.Active, NewStatus = UserStatus.Inactive, Reason = "Reason for ban" }
            };

            var pagedResult = new DomainLayer.Common.PagedResult<UserStatusHistory>(histories, 1);

            _mockHistory.Setup(r => r.GetPagedAsync(
                It.IsAny<Expression<Func<UserStatusHistory, bool>>>(),
                req.Page,
                req.PageSize,
                It.IsAny<Expression<Func<UserStatusHistory, object>>>(),
                false,
                It.IsAny<CancellationToken>())).ReturnsAsync(pagedResult);

            _mockUsers.Setup(r => r.GetByIdAsync(userId))
                      .ReturnsAsync(new User { Id = userId });

            var result = await _accountService.GetUserStatusHistoryAsync(userId, req);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Single(result.Data!.Items);
        }

        [Fact]
        public async Task UpdateMyAccountAsync_UpdatesOnlyProfileAllowList()
        {
            var roleId = Guid.NewGuid();
            var user = new User
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                UserName = "immutable-user",
                Email = "immutable@example.com",
                PasswordHash = "immutable-hash",
                FullName = "Old Name",
                Status = UserStatus.Active,
                AuthProvider = AuthProvider.Local,
                GoogleId = "immutable-google-id",
                RefreshTokenHash = "immutable-token",
                RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(1)
            };
            var request = new UpdateProfileRequest
            {
                FullName = "  New Name  ",
                Phone = "  0901234567  ",
                Address = "  District 1  ",
                DoB = new DateOnly(2000, 1, 2)
            };

            _mockUsers.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
            _mockUsers.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);
            _mockRoles.Setup(repository => repository.GetByIdAsync(roleId))
                .ReturnsAsync(new Role { Id = roleId, RoleName = "Customer" });
            _mockMapper.Setup(mapper => mapper.Map<UserResponse>(user))
                .Returns(() => new UserResponse
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Phone = user.Phone,
                    Address = user.Address,
                    DoB = user.DoB
                });

            var response = await _accountService.UpdateMyAccountAsync(user.Id, request);

            Assert.Equal("New Name", user.FullName);
            Assert.Equal("0901234567", user.Phone);
            Assert.Equal("District 1", user.Address);
            Assert.Equal(request.DoB, user.DoB);
            Assert.Equal("immutable-user", user.UserName);
            Assert.Equal("immutable@example.com", user.Email);
            Assert.Equal("immutable-hash", user.PasswordHash);
            Assert.Equal(AuthProvider.Local, user.AuthProvider);
            Assert.Equal("immutable-google-id", user.GoogleId);
            Assert.Equal("immutable-token", user.RefreshTokenHash);
            Assert.NotNull(user.RefreshTokenExpiresAt);
            Assert.Equal(UserStatus.Active, user.Status);
            Assert.Equal(roleId, user.RoleId);
            Assert.Equal("Customer", response.Data!.Role);
            _mockUsers.Verify(repository => repository.Update(It.IsAny<User>()), Times.Never);
            _mockUsers.Verify(repository => repository.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateMyAccountAsync_WhitespaceFullName_IsRejectedBeforePersistence()
        {
            var user = new User { Id = Guid.NewGuid(), FullName = "Existing Name" };
            _mockUsers.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var exception = await Assert.ThrowsAsync<AppException>(() =>
                _accountService.UpdateMyAccountAsync(
                    user.Id,
                    new UpdateProfileRequest { FullName = "   " }));

            Assert.Equal("FULL_NAME_REQUIRED", exception.ErrorCode);
            Assert.Equal("Existing Name", user.FullName);
            _mockUsers.Verify(repository => repository.Update(It.IsAny<User>()), Times.Never);
            _mockUsers.Verify(repository => repository.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdateMyAccountAsync_FutureDateOfBirth_IsRejected()
        {
            var user = new User { Id = Guid.NewGuid(), FullName = "Existing Name" };
            _mockUsers.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
            var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

            var exception = await Assert.ThrowsAsync<AppException>(() =>
                _accountService.UpdateMyAccountAsync(
                    user.Id,
                    new UpdateProfileRequest { FullName = "Valid Name", DoB = futureDate }));

            Assert.Equal("DATE_OF_BIRTH_INVALID", exception.ErrorCode);
            _mockUsers.Verify(repository => repository.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdateMyAccountAsync_InvalidPhone_IsRejectedBeforePersistence()
        {
            var user = new User { Id = Guid.NewGuid(), FullName = "Existing Name" };
            _mockUsers.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var exception = await Assert.ThrowsAsync<AppException>(() =>
                _accountService.UpdateMyAccountAsync(
                    user.Id,
                    new UpdateProfileRequest
                    {
                        FullName = "Valid Name",
                        Phone = "not-a-phone"
                    }));

            Assert.Equal("PHONE_INVALID", exception.ErrorCode);
            _mockUsers.Verify(repository => repository.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdateAvatarAsync_PersistsManagedUrlThenDeletesOldAvatar()
        {
            var roleId = Guid.NewGuid();
            var user = new User
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                FullName = "Customer",
                AvatarUrl = "/uploads/avatars/old.jpg"
            };
            const string newUrl = "/uploads/avatars/new.png";
            _mockUsers.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
            _mockUsers.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);
            _mockFileStorage.Setup(storage => storage.SaveAvatarAsync(
                    It.IsAny<Stream>(), "avatar.png", "image/png", 4, It.IsAny<CancellationToken>()))
                .ReturnsAsync(newUrl);
            _mockRoles.Setup(repository => repository.GetByIdAsync(roleId))
                .ReturnsAsync(new Role { Id = roleId, RoleName = "Customer" });
            _mockMapper.Setup(mapper => mapper.Map<UserResponse>(user))
                .Returns(() => new UserResponse { Id = user.Id, AvatarUrl = user.AvatarUrl });

            var response = await _accountService.UpdateAvatarAsync(
                user.Id, new MemoryStream(new byte[4]), "avatar.png", "image/png", 4);

            Assert.Equal(newUrl, user.AvatarUrl);
            Assert.Equal(newUrl, response.Data!.AvatarUrl);
            _mockUsers.Verify(repository => repository.SaveChangesAsync(), Times.Once);
            _mockFileStorage.Verify(storage => storage.DeleteAvatarIfManagedAsync(
                "/uploads/avatars/old.jpg", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAvatarAsync_DatabaseFailure_CleansUpNewAvatar()
        {
            var user = new User { Id = Guid.NewGuid(), AvatarUrl = "/uploads/avatars/old.jpg" };
            const string newUrl = "/uploads/avatars/new.png";
            _mockUsers.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
            _mockUsers.Setup(repository => repository.SaveChangesAsync())
                .ThrowsAsync(new InvalidOperationException("database unavailable"));
            _mockFileStorage.Setup(storage => storage.SaveAvatarAsync(
                    It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(newUrl);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _accountService.UpdateAvatarAsync(
                user.Id, new MemoryStream(new byte[4]), "avatar.png", "image/png", 4));

            _mockFileStorage.Verify(storage => storage.DeleteAvatarIfManagedAsync(
                newUrl, It.IsAny<CancellationToken>()), Times.Once);
            _mockFileStorage.Verify(storage => storage.DeleteAvatarIfManagedAsync(
                "/uploads/avatars/old.jpg", It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAvatarAsync_OldAvatarCleanupFailure_DoesNotFailCommittedUpdate()
        {
            var roleId = Guid.NewGuid();
            var user = new User
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                AvatarUrl = "/uploads/avatars/old.jpg"
            };
            _mockUsers.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
            _mockUsers.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);
            _mockFileStorage.Setup(storage => storage.SaveAvatarAsync(
                    It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("/uploads/avatars/new.png");
            _mockFileStorage.Setup(storage => storage.DeleteAvatarIfManagedAsync(
                    "/uploads/avatars/old.jpg", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("file is locked"));
            _mockRoles.Setup(repository => repository.GetByIdAsync(roleId))
                .ReturnsAsync(new Role { Id = roleId, RoleName = "Customer" });
            _mockMapper.Setup(mapper => mapper.Map<UserResponse>(user))
                .Returns(new UserResponse());

            var response = await _accountService.UpdateAvatarAsync(
                user.Id, new MemoryStream(new byte[4]), "avatar.png", "image/png", 4);

            Assert.True(response.Success);
            Assert.Equal("/uploads/avatars/new.png", user.AvatarUrl);
        }

        [Fact]
        public async Task UpdateAvatarAsync_ConcurrentStaleSnapshots_DeleteOnlyTheirPreviousAvatar()
        {
            var roleId = Guid.NewGuid();
            var oldUrl = $"/uploads/avatars/{Guid.NewGuid():N}.png";
            var firstUrl = $"/uploads/avatars/{Guid.NewGuid():N}.png";
            var secondUrl = $"/uploads/avatars/{Guid.NewGuid():N}.png";
            var snapshots = new ConcurrentQueue<User>(new[]
            {
                CreateSnapshot(),
                CreateSnapshot()
            });
            var bothUploadsStarted = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var uploadCount = 0;

            _mockUsers.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(() => snapshots.TryDequeue(out var user) ? user : null);
            _mockUsers.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);
            _mockRoles.Setup(repository => repository.GetByIdAsync(roleId))
                .ReturnsAsync(new Role { Id = roleId, RoleName = "Customer" });
            _mockMapper.Setup(mapper => mapper.Map<UserResponse>(It.IsAny<User>()))
                .Returns((User user) => new UserResponse { Id = user.Id, AvatarUrl = user.AvatarUrl });
            _mockFileStorage.Setup(storage => storage.SaveAvatarAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    "image/png",
                    4,
                    It.IsAny<CancellationToken>()))
                .Returns(async (Stream stream, string fileName, string contentType, long length, CancellationToken token) =>
                {
                    if (Interlocked.Increment(ref uploadCount) == 2)
                        bothUploadsStarted.TrySetResult();

                    await bothUploadsStarted.Task.WaitAsync(token);
                    return fileName == "first.png" ? firstUrl : secondUrl;
                });

            var first = _accountService.UpdateAvatarAsync(
                Guid.NewGuid(), new MemoryStream(new byte[4]), "first.png", "image/png", 4);
            var second = _accountService.UpdateAvatarAsync(
                Guid.NewGuid(), new MemoryStream(new byte[4]), "second.png", "image/png", 4);

            var responses = await Task.WhenAll(first, second);

            Assert.All(responses, response => Assert.True(response.Success));
            _mockFileStorage.Verify(storage => storage.DeleteAvatarIfManagedAsync(
                oldUrl, It.IsAny<CancellationToken>()), Times.Exactly(2));
            _mockFileStorage.Verify(storage => storage.DeleteAvatarIfManagedAsync(
                firstUrl, It.IsAny<CancellationToken>()), Times.Never);
            _mockFileStorage.Verify(storage => storage.DeleteAvatarIfManagedAsync(
                secondUrl, It.IsAny<CancellationToken>()), Times.Never);
            _mockUsers.Verify(repository => repository.Update(It.IsAny<User>()), Times.Never);

            User CreateSnapshot() => new()
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                Email = "immutable@example.com",
                UserName = "immutable-user",
                PasswordHash = "immutable-password-hash",
                AuthProvider = AuthProvider.Local,
                GoogleId = "immutable-google-id",
                RefreshTokenHash = "immutable-refresh-token",
                Status = UserStatus.Active,
                AvatarUrl = oldUrl
            };
        }
    }
}

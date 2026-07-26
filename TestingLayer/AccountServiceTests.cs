using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceCore.JWT;
using DomainLayer.InterfaceRepository;
using ApplicationLayer.Services.Account;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;
using ApplicationLayer.Exceptions;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Linq;

namespace TestingLayer
{
    public class AccountServiceTests
    {
        private readonly Mock<IUserRepository> _mockUsers;
        private readonly Mock<IGenericRepository<Role>> _mockRoles;
        private readonly Mock<IPasswordHasher> _mockPasswordHasher;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<INotificationService> _mockNotifications;
        private readonly Mock<IGenericRepository<UserStatusHistory>> _mockHistory;
        private readonly Mock<IGenericRepository<EmailOutbox>> _mockOutbox;
        private readonly Mock<ILogger<AccountService>> _mockLogger;
        private readonly AccountService _accountService;

        public AccountServiceTests()
        {
            _mockUsers = new Mock<IUserRepository>();
            _mockRoles = new Mock<IGenericRepository<Role>>();
            _mockPasswordHasher = new Mock<IPasswordHasher>();
            _mockMapper = new Mock<IMapper>();
            _mockNotifications = new Mock<INotificationService>();
            _mockHistory = new Mock<IGenericRepository<UserStatusHistory>>();
            _mockOutbox = new Mock<IGenericRepository<EmailOutbox>>();
            _mockLogger = new Mock<ILogger<AccountService>>();

            _accountService = new AccountService(
                _mockUsers.Object,
                _mockRoles.Object,
                _mockPasswordHasher.Object,
                _mockMapper.Object,
                _mockNotifications.Object,
                _mockHistory.Object,
                _mockOutbox.Object,
                _mockLogger.Object
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
    }
}

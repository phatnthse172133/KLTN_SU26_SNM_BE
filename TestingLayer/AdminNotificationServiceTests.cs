using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Notifications;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer
{
    public class AdminNotificationServiceTests
    {
        private readonly Mock<INotificationRepository> _mockNotifications;
        private readonly Mock<IUserDeviceTokenRepository> _mockDeviceTokens;
        private readonly Mock<IUserRepository> _mockUsers;
        private readonly Mock<IPushNotificationService> _mockPush;
        private readonly Mock<IRealtimeNotificationPublisher> _mockRealtime;
        private readonly Mock<IOnlinePresenceService> _mockPresence;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ILogger<NotificationService>> _mockLogger;
        private readonly NotificationService _service;

        public AdminNotificationServiceTests()
        {
            _mockNotifications = new Mock<INotificationRepository>();
            _mockDeviceTokens = new Mock<IUserDeviceTokenRepository>();
            _mockUsers = new Mock<IUserRepository>();
            _mockPush = new Mock<IPushNotificationService>();
            _mockRealtime = new Mock<IRealtimeNotificationPublisher>();
            _mockPresence = new Mock<IOnlinePresenceService>();
            _mockMapper = new Mock<IMapper>();
            _mockLogger = new Mock<ILogger<NotificationService>>();

            _service = new NotificationService(
                _mockNotifications.Object,
                _mockDeviceTokens.Object,
                _mockUsers.Object,
                _mockPush.Object,
                _mockRealtime.Object,
                Mock.Of<ApplicationLayer.Services.Realtime.IRealtimeEventPublisher>(),
                _mockPresence.Object,
                _mockMapper.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task CreateByAdminAsync_TargetAllUsers_CreatesBatch()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var req = new AdminCreateNotificationRequest
            {
                Title = "Test All",
                Content = "Content All",
                Target = NotificationTarget.AllUsers
            };

            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), Status = UserStatus.Active },
                new User { Id = Guid.NewGuid(), Status = UserStatus.Active }
            };

            _mockUsers.Setup(x => x.GetActiveRecipientIdsAsync(null, null, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(users.Select(u => u.Id).ToList().AsReadOnly());

            // Act
            var result = await _service.CreateByAdminAsync(adminId, req, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.Data!.RecipientCount);
            _mockNotifications.Verify(x => x.AddRangeAsync(It.Is<IEnumerable<Notification>>(n => n.Count() == 2)), Times.Once);
            _mockNotifications.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateByAdminAsync_TargetSpecificUser_NotFound_Throws()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var req = new AdminCreateNotificationRequest
            {
                Title = "Test Specific",
                Content = "Content Specific",
                Target = NotificationTarget.SpecificUser,
                UserId = Guid.NewGuid()
            };

            _mockUsers.Setup(x => x.GetActiveRecipientIdsAsync(req.UserId, null, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<Guid>().AsReadOnly());

            // Act & Assert
            await Assert.ThrowsAsync<AppException>(() => _service.CreateByAdminAsync(adminId, req, CancellationToken.None));
        }
        [Fact]
        public async Task CreateByAdminAsync_TargetRole_CreatesBatch()
        {
            var adminId = Guid.NewGuid();
            var req = new AdminCreateNotificationRequest
            {
                Title = "Role Test",
                Content = "Role Content",
                Target = NotificationTarget.Role,
                Role = "marketowner"
            };
            var users = new List<User> { new User { Id = Guid.NewGuid(), Status = UserStatus.Active } };
            _mockUsers.Setup(x => x.GetActiveRecipientIdsAsync(null, "MarketOwner", It.IsAny<CancellationToken>()))
                      .ReturnsAsync(users.Select(u => u.Id).ToList().AsReadOnly());

            var result = await _service.CreateByAdminAsync(adminId, req, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(1, result.Data!.RecipientCount);
            _mockNotifications.Verify(x => x.AddRangeAsync(It.Is<IEnumerable<Notification>>(n =>
                n.Count() == 1 &&
                n.First().TargetRole == "MarketOwner" &&
                n.First().Type == NotificationType.SystemAnnouncement &&
                n.First().Target == NotificationTarget.Role &&
                n.First().CreatedByUserId == adminId &&
                n.First().BatchId.HasValue)), Times.Once);
        }

        [Fact]
        public async Task CreateByAdminAsync_TargetSpecificUser_Success()
        {
            var adminId = Guid.NewGuid();
            var req = new AdminCreateNotificationRequest
            {
                Title = "Specific Test",
                Content = "Specific Content",
                Target = NotificationTarget.SpecificUser,
                UserId = Guid.NewGuid()
            };
            var users = new List<User> { new User { Id = req.UserId.Value, Status = UserStatus.Active } };
            _mockUsers.Setup(x => x.GetActiveRecipientIdsAsync(req.UserId, null, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(users.Select(u => u.Id).ToList().AsReadOnly());

            var result = await _service.CreateByAdminAsync(adminId, req, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(1, result.Data!.RecipientCount);
            _mockNotifications.Verify(x => x.AddRangeAsync(It.Is<IEnumerable<Notification>>(n =>
                n.Count() == 1 && n.First().UserId == req.UserId)), Times.Once);
        }

        [Fact]
        public async Task CreateByAdminAsync_MissingRoleOrUserId_ThrowsBadRequest()
        {
            var adminId = Guid.NewGuid();
            var reqRole = new AdminCreateNotificationRequest { Target = NotificationTarget.Role };
            await Assert.ThrowsAsync<AppException>(() => _service.CreateByAdminAsync(adminId, reqRole, CancellationToken.None));

            var reqUser = new AdminCreateNotificationRequest { Target = NotificationTarget.SpecificUser };
            await Assert.ThrowsAsync<AppException>(() => _service.CreateByAdminAsync(adminId, reqUser, CancellationToken.None));
        }

        [Fact]
        public async Task CreateByAdminAsync_NoRecipients_ThrowsNotFound()
        {
            var adminId = Guid.NewGuid();
            var req = new AdminCreateNotificationRequest { Target = NotificationTarget.AllUsers };
            _mockUsers.Setup(x => x.GetActiveRecipientIdsAsync(null, null, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<Guid>().AsReadOnly());

            await Assert.ThrowsAsync<AppException>(() => _service.CreateByAdminAsync(adminId, req, CancellationToken.None));
        }

        [Fact]
        public async Task GetAdminNotificationsAsync_InvalidDateRange_ThrowsBadRequest()
        {
            var req = new AdminNotificationListRequest
            {
                FromDate = DateTime.UtcNow,
                ToDate = DateTime.UtcNow.AddDays(-1)
            };
            await Assert.ThrowsAsync<AppException>(() => _service.GetAdminNotificationsAsync(req, CancellationToken.None));
        }

        [Fact]
        public async Task GetAdminNotificationDetailAsync_NotFound_ThrowsNotFound()
        {
            var batchId = Guid.NewGuid();
            _mockNotifications.Setup(x => x.GetAdminBatchDetailAsync(batchId, It.IsAny<CancellationToken>()))
                              .ReturnsAsync((Notification?)null);

            await Assert.ThrowsAsync<AppException>(() => _service.GetAdminNotificationDetailAsync(batchId, CancellationToken.None));
        }

        [Fact]
        public async Task GetAdminNotificationsAsync_ReturnsCorrectRecipientCount()
        {
            var req = new AdminNotificationListRequest { Page = 1, PageSize = 10 };
            var batchId = Guid.NewGuid();
            var notif = new Notification { BatchId = batchId, Title = "Title", Content = "Content", Type = NotificationType.SystemAnnouncement };
            var pagedResult = new DomainLayer.Common.PagedResult<Notification>(new List<Notification> { notif }, 1);

            _mockNotifications.Setup(x => x.GetAdminPagedBatchesAsync(null, null, null, null, null, null, 1, 10, It.IsAny<CancellationToken>()))
                              .ReturnsAsync(pagedResult);
            _mockNotifications.Setup(x => x.GetBatchRecipientCountsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
                              .ReturnsAsync(new Dictionary<Guid, int> { { batchId, 5 } });

            var result = await _service.GetAdminNotificationsAsync(req, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(result.Data!.Items);
            Assert.Equal(5, result.Data.Items.First().RecipientCount);
        }
    }
}

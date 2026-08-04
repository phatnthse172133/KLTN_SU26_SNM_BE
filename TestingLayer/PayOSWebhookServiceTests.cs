using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using ApplicationLayer.Services.Subscriptions;
using ApplicationLayer.Services.PayOS;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Exceptions;
using Microsoft.Extensions.Logging;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer
{
    public class PayOSWebhookServiceTests
    {
        private readonly Mock<ISubscriptionRepository> _mockRepo;
        private readonly Mock<INotificationService> _mockNotifications;
        private readonly Mock<ILogger<PayOSWebhookService>> _mockLogger;
        private readonly PayOSWebhookService _service;

        private readonly Guid _ownerId = Guid.NewGuid();
        private readonly Guid _boothId = Guid.NewGuid();
        private readonly Guid _subscriptionId = Guid.NewGuid();
        private readonly long _orderCode = 1234567890;
        private readonly decimal _amount = 100000;

        public PayOSWebhookServiceTests()
        {
            _mockRepo = new Mock<ISubscriptionRepository>();
            _mockNotifications = new Mock<INotificationService>();
            _mockLogger = new Mock<ILogger<PayOSWebhookService>>();
            _service = new PayOSWebhookService(_mockRepo.Object, _mockNotifications.Object, _mockLogger.Object);
        }

        private PayOSWebhookData CreateWebhookData(string code = "00")
        {
            return new PayOSWebhookData
            {
                OrderCode = _orderCode,
                Amount = (int)_amount,
                Code = code,
                IsSuccessful = code == "00",
                TransactionDateTime = DateTime.UtcNow.ToString(),
                PaymentLinkId = "link-123",
            };
        }

        [Fact]
        public async Task Webhook_NonSuccessCode_ReturnsNotSuccessful()
        {
            var data = CreateWebhookData("99");

            var result = await _service.HandleWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.NotSuccessful, result);
            _mockRepo.Verify(r => r.ActivateBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockRepo.Verify(r => r.ActivateMarketSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Webhook_ValidBoothSubscription_Activates()
        {
            var data = CreateWebhookData("00");

            _mockRepo.Setup(r => r.GetBoothSubscriptionByOrderCodeAsync(_orderCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BoothSubscription
                {
                    Id = _subscriptionId,
                    BoothId = _boothId,
                    Booth = new Booth { BoothOwnerId = _ownerId },
                    Status = SubscriptionStatus.PendingPayment,
                    PaidAmount = _amount,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(30),
                    Package = new Package { PackageName = "Booth Growth", DurationDays = 30 },
                });

            _mockRepo.Setup(r => r.ActivateBoothSubscriptionAsync(_subscriptionId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var result = await _service.HandleWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.SubscriptionHandled, result);
            _mockRepo.Verify(r => r.ActivateBoothSubscriptionAsync(_subscriptionId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockRepo.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Webhook_AmountMismatch_Throws400()
        {
            var data = new PayOSWebhookData
            {
                OrderCode = _orderCode,
                Amount = 999,
                Code = "00",
                IsSuccessful = true,
            };

            _mockRepo.Setup(r => r.GetBoothSubscriptionByOrderCodeAsync(_orderCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BoothSubscription
                {
                    Id = _subscriptionId,
                    Booth = new Booth { BoothOwnerId = _ownerId },
                    Status = SubscriptionStatus.PendingPayment,
                    PaidAmount = _amount,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(30),
                });

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.HandleWebhookAsync(data));

            Assert.Equal(400, ex.StatusCode);
            Assert.Equal("PAYOS_AMOUNT_MISMATCH", ex.ErrorCode);
        }

        [Fact]
        public async Task Webhook_AlreadyActive_ReturnsAlreadyProcessed()
        {
            var data = CreateWebhookData("00");

            _mockRepo.Setup(r => r.GetBoothSubscriptionByOrderCodeAsync(_orderCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BoothSubscription
                {
                    Id = _subscriptionId,
                    Booth = new Booth { BoothOwnerId = _ownerId },
                    Status = SubscriptionStatus.Active,
                    PaidAmount = _amount,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(30),
                });

            var result = await _service.HandleWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.AlreadyProcessed, result);
            _mockRepo.Verify(r => r.ActivateBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Webhook_DuplicateWebhook_ReturnsAlreadyProcessed()
        {
            var data = CreateWebhookData("00");

            _mockRepo.Setup(r => r.GetBoothSubscriptionByOrderCodeAsync(_orderCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BoothSubscription
                {
                    Id = _subscriptionId,
                    Booth = new Booth { BoothOwnerId = _ownerId },
                    Status = SubscriptionStatus.PendingPayment,
                    PaidAmount = _amount,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(30),
                    Package = new Package { PackageName = "Booth Growth", DurationDays = 30 },
                });

            _mockRepo.Setup(r => r.ActivateBoothSubscriptionAsync(_subscriptionId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            var result = await _service.HandleWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.AlreadyProcessed, result);
            _mockRepo.Verify(r => r.ActivateBoothSubscriptionAsync(_subscriptionId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Webhook_SubscriptionNotFound_ReturnsNotFound()
        {
            var data = CreateWebhookData("00");

            _mockRepo.Setup(r => r.GetBoothSubscriptionByOrderCodeAsync(_orderCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BoothSubscription?)null);
            _mockRepo.Setup(r => r.GetMarketSubscriptionByOrderCodeAsync(_orderCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync((MarketSubscription?)null);

            var result = await _service.HandleWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.NotFound, result);
        }

        [Fact]
        public async Task Webhook_RenewWhileActive_StacksEndDate()
        {
            var now = DateTime.UtcNow;
            var existingEndDate = now.AddDays(10);

            var data = CreateWebhookData("00");

            _mockRepo.Setup(r => r.GetBoothSubscriptionByOrderCodeAsync(_orderCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BoothSubscription
                {
                    Id = _subscriptionId,
                    BoothId = _boothId,
                    Booth = new Booth { BoothOwnerId = _ownerId },
                    Status = SubscriptionStatus.PendingPayment,
                    PaidAmount = _amount,
                    StartDate = now,
                    EndDate = now.AddDays(30),
                    Package = new Package { PackageName = "Booth Growth", DurationDays = 30 },
                });

            _mockRepo.Setup(r => r.GetLatestApprovedBoothSubscriptionAsync(_boothId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BoothSubscription
                {
                    Id = Guid.NewGuid(),
                    BoothId = _boothId,
                    EndDate = existingEndDate,
                    Status = SubscriptionStatus.Active,
                });

            DateTime capturedStart = default;
            DateTime capturedEnd = default;
            _mockRepo.Setup(r => r.ActivateBoothSubscriptionAsync(_subscriptionId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, DateTime, DateTime, DateTime, CancellationToken>((_, start, end, _, _) =>
                {
                    capturedStart = start;
                    capturedEnd = end;
                })
                .ReturnsAsync(1);

            var result = await _service.HandleWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.SubscriptionHandled, result);
            Assert.Equal(existingEndDate, capturedStart);
            Assert.Equal(existingEndDate.AddDays(30), capturedEnd);
        }

        [Fact]
        public async Task Webhook_RenewWhenExpired_StartsFromNow()
        {
            var now = DateTime.UtcNow;
            var pastEndDate = now.AddDays(-5);

            var data = CreateWebhookData("00");

            _mockRepo.Setup(r => r.GetBoothSubscriptionByOrderCodeAsync(_orderCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BoothSubscription
                {
                    Id = _subscriptionId,
                    BoothId = _boothId,
                    Booth = new Booth { BoothOwnerId = _ownerId },
                    Status = SubscriptionStatus.PendingPayment,
                    PaidAmount = _amount,
                    StartDate = now,
                    EndDate = now.AddDays(30),
                    Package = new Package { PackageName = "Booth Growth", DurationDays = 30 },
                });

            _mockRepo.Setup(r => r.GetLatestApprovedBoothSubscriptionAsync(_boothId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BoothSubscription
                {
                    Id = Guid.NewGuid(),
                    BoothId = _boothId,
                    EndDate = pastEndDate,
                    Status = SubscriptionStatus.Active,
                });

            DateTime capturedStart = default;
            DateTime capturedEnd = default;
            _mockRepo.Setup(r => r.ActivateBoothSubscriptionAsync(_subscriptionId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, DateTime, DateTime, DateTime, CancellationToken>((_, start, end, _, _) =>
                {
                    capturedStart = start;
                    capturedEnd = end;
                })
                .ReturnsAsync(1);

            var result = await _service.HandleWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.SubscriptionHandled, result);
            Assert.True(capturedStart > pastEndDate);
            Assert.Equal(capturedStart.AddDays(30), capturedEnd);
        }

        [Fact]
        public async Task Webhook_Upgrade_ExpiresCurrentPlanAndStartsReplacementImmediately()
        {
            var now = DateTime.UtcNow;
            var existing = new BoothSubscription
            {
                Id = Guid.NewGuid(),
                BoothId = _boothId,
                StartDate = now.AddDays(-10),
                EndDate = now.AddDays(20),
                Status = SubscriptionStatus.Active
            };
            var pending = new BoothSubscription
            {
                Id = _subscriptionId,
                BoothId = _boothId,
                Booth = new Booth { BoothOwnerId = _ownerId },
                Status = SubscriptionStatus.PendingPayment,
                PaidAmount = _amount,
                StartDate = now,
                EndDate = now.AddDays(30),
                ChangeType = "Upgrade",
                PreviousSubscriptionId = existing.Id,
                Package = new Package { PackageName = "Booth Featured", DurationDays = 30 }
            };
            _mockRepo.Setup(r => r.GetBoothSubscriptionByOrderCodeAsync(_orderCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pending);
            _mockRepo.Setup(r => r.GetLatestApprovedBoothSubscriptionAsync(_boothId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            _mockRepo.Setup(r => r.UpdateBoothSubscriptionStatusAsync(
                    existing.Id,
                    SubscriptionStatus.Active,
                    SubscriptionStatus.Expired,
                    existing.StartDate,
                    It.IsAny<DateTime>(),
                    "Replaced by an upgrade.",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            DateTime capturedStart = default;
            _mockRepo.Setup(r => r.ActivateBoothSubscriptionAsync(_subscriptionId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, DateTime, DateTime, DateTime, CancellationToken>((_, start, _, _, _) => capturedStart = start)
                .ReturnsAsync(1);

            var result = await _service.HandleWebhookAsync(CreateWebhookData("00"));

            Assert.Equal(WebhookDispatchResult.SubscriptionHandled, result);
            Assert.True(capturedStart < existing.EndDate);
            _mockRepo.Verify(r => r.UpdateBoothSubscriptionStatusAsync(
                existing.Id,
                SubscriptionStatus.Active,
                SubscriptionStatus.Expired,
                existing.StartDate,
                It.IsAny<DateTime>(),
                "Replaced by an upgrade.",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Webhook_CommitBeforeNotification_NotificationAfterCommit()
        {
            var data = CreateWebhookData("00");

            _mockRepo.Setup(r => r.GetBoothSubscriptionByOrderCodeAsync(_orderCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BoothSubscription
                {
                    Id = _subscriptionId,
                    BoothId = _boothId,
                    Booth = new Booth { BoothOwnerId = _ownerId },
                    Status = SubscriptionStatus.PendingPayment,
                    PaidAmount = _amount,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(30),
                    Package = new Package { PackageName = "Booth Growth", DurationDays = 30 },
                });

            _mockRepo.Setup(r => r.ActivateBoothSubscriptionAsync(_subscriptionId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var callOrder = new System.Collections.Generic.List<string>();
            _mockRepo.Setup(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()))
                .Callback(() => callOrder.Add("commit"))
                .Returns(Task.CompletedTask);
            _mockNotifications.Setup(n => n.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
                .Callback<NotificationMessage, CancellationToken>((_, _) => callOrder.Add("notify"))
                .Returns(Task.CompletedTask);

            await _service.HandleWebhookAsync(data);

            Assert.Equal(new[] { "commit", "notify" }, callOrder);
        }
    }
}

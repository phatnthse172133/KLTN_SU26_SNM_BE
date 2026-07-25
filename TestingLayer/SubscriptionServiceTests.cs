using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using DomainLayer.Common;
using ApplicationLayer.Services.Subscriptions;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.Exceptions;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer
{
    public class SubscriptionServiceTests
    {
        private readonly Mock<ISubscriptionRepository> _mockRepo;
        private readonly Mock<INotificationService> _mockNotifications;
        private readonly SubscriptionService _service;

        public SubscriptionServiceTests()
        {
            _mockRepo = new Mock<ISubscriptionRepository>();
            _mockNotifications = new Mock<INotificationService>();
            _service = new SubscriptionService(_mockRepo.Object, _mockNotifications.Object);
        }

        private BoothSubscription CreatePendingBoothSubscription()
        {
            return new BoothSubscription
            {
                Id = Guid.NewGuid(),
                BoothId = Guid.NewGuid(),
                PackageId = Guid.NewGuid(),
                Status = SubscriptionStatus.PendingPayment,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(30),
                PaidAmount = 1000000
            };
        }

        private MarketSubscription CreatePendingMarketSubscription()
        {
            return new MarketSubscription
            {
                Id = Guid.NewGuid(),
                MarketOwnerId = Guid.NewGuid(),
                PackageId = Guid.NewGuid(),
                Status = SubscriptionStatus.PendingPayment,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(30),
                PaidAmount = 2000000
            };
        }

        private Package CreateActiveBoothPackage()
        {
            return new Package
            {
                Id = Guid.NewGuid(),
                PackageName = "Booth Growth",
                Code = "BOOTH_GROWTH",
                Price = 1000000,
                DurationDays = 30,
                Type = PackageType.Booth,
                Status = PackageStatus.Active
            };
        }

        private Package CreateActiveMarketPackage()
        {
            return new Package
            {
                Id = Guid.NewGuid(),
                PackageName = "Market Pro",
                Code = "MARKET_PRO",
                Price = 2000000,
                DurationDays = 30,
                Type = PackageType.Market,
                Status = PackageStatus.Active
            };
        }

        [Fact]
        public async Task VerifySubscription_RejectWithoutReason_ThrowsBadRequest()
        {
            var request = new VerifySubscriptionRequest { IsApproved = false, AdminNotes = null };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.VerifySubscriptionAsync(Guid.NewGuid(), PackageType.Booth, request));

            Assert.Equal(400, ex.StatusCode);
            Assert.Equal("REJECT_REASON_REQUIRED", ex.ErrorCode);
        }

        [Fact]
        public async Task VerifySubscription_RejectWithEmptyReason_ThrowsBadRequest()
        {
            var request = new VerifySubscriptionRequest { IsApproved = false, AdminNotes = "   " };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.VerifySubscriptionAsync(Guid.NewGuid(), PackageType.Booth, request));

            Assert.Equal(400, ex.StatusCode);
            Assert.Equal("REJECT_REASON_REQUIRED", ex.ErrorCode);
        }

        [Fact]
        public async Task VerifySubscription_BoothNotFound_ThrowsNotFound()
        {
            var subId = Guid.NewGuid();
            var request = new VerifySubscriptionRequest { IsApproved = true };

            _mockRepo.Setup(r => r.GetBoothSubscriptionByIdAsync(subId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BoothSubscription?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.VerifySubscriptionAsync(subId, PackageType.Booth, request));

            Assert.Contains("not found", ex.Message);
            _mockRepo.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task VerifySubscription_BoothAlreadyProcessed_ThrowsConflict()
        {
            var sub = CreatePendingBoothSubscription();
            sub.Status = SubscriptionStatus.Active;
            var request = new VerifySubscriptionRequest { IsApproved = true };

            _mockRepo.Setup(r => r.GetBoothSubscriptionByIdAsync(sub.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sub);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.VerifySubscriptionAsync(sub.Id, PackageType.Booth, request));

            Assert.Equal(409, ex.StatusCode);
            Assert.Equal("SUBSCRIPTION_ALREADY_PROCESSED", ex.ErrorCode);
            _mockRepo.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task VerifySubscription_BoothPackageInactive_ThrowsBadRequest()
        {
            var sub = CreatePendingBoothSubscription();
            var pkg = CreateActiveBoothPackage();
            pkg.Status = PackageStatus.Inactive;
            var request = new VerifySubscriptionRequest { IsApproved = true };

            _mockRepo.Setup(r => r.GetBoothSubscriptionByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
            _mockRepo.Setup(r => r.GetPackageByIdAsync(sub.PackageId, It.IsAny<CancellationToken>())).ReturnsAsync(pkg);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.VerifySubscriptionAsync(sub.Id, PackageType.Booth, request));

            Assert.Contains("no longer available", ex.Message);
            _mockRepo.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task VerifySubscription_BoothPackageTypeMismatch_ThrowsBadRequest()
        {
            var sub = CreatePendingBoothSubscription();
            var pkg = CreateActiveMarketPackage();
            var request = new VerifySubscriptionRequest { IsApproved = true };

            _mockRepo.Setup(r => r.GetBoothSubscriptionByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
            _mockRepo.Setup(r => r.GetPackageByIdAsync(sub.PackageId, It.IsAny<CancellationToken>())).ReturnsAsync(pkg);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.VerifySubscriptionAsync(sub.Id, PackageType.Booth, request));

            Assert.Contains("type mismatch", ex.Message);
            _mockRepo.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task VerifySubscription_ApproveBooth_CancelsExistingAndActivates()
        {
            var sub = CreatePendingBoothSubscription();
            var pkg = CreateActiveBoothPackage();
            var request = new VerifySubscriptionRequest { IsApproved = true, AdminNotes = "Approved" };

            _mockRepo.Setup(r => r.GetBoothSubscriptionByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
            _mockRepo.Setup(r => r.GetPackageByIdAsync(sub.PackageId, It.IsAny<CancellationToken>())).ReturnsAsync(pkg);
            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(sub.BoothId, It.IsAny<CancellationToken>())).ReturnsAsync((BoothSubscription?)null);
            _mockRepo.Setup(r => r.CancelActiveBoothSubscriptionsAsync(sub.BoothId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockRepo.Setup(r => r.UpdateBoothSubscriptionStatusAsync(
                sub.Id, SubscriptionStatus.PendingPayment, SubscriptionStatus.Active,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), "Approved", It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var result = await _service.VerifySubscriptionAsync(sub.Id, PackageType.Booth, request);

            Assert.True(result);
            _mockRepo.Verify(r => r.CancelActiveBoothSubscriptionsAsync(sub.BoothId, It.IsAny<CancellationToken>()), Times.Never);
            _mockRepo.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockRepo.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task VerifySubscription_ApproveBooth_WithExistingSubscription_StacksDates()
        {
            var sub = CreatePendingBoothSubscription();
            var pkg = CreateActiveBoothPackage();
            var request = new VerifySubscriptionRequest { IsApproved = true, AdminNotes = "Approved" };

            var existingSub = new BoothSubscription
            {
                Id = Guid.NewGuid(),
                BoothId = sub.BoothId,
                EndDate = DateTime.UtcNow.AddDays(15),
                Status = SubscriptionStatus.Active
            };

            _mockRepo.Setup(r => r.GetBoothSubscriptionByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
            _mockRepo.Setup(r => r.GetPackageByIdAsync(sub.PackageId, It.IsAny<CancellationToken>())).ReturnsAsync(pkg);
            _mockRepo.Setup(r => r.GetLatestApprovedBoothSubscriptionAsync(sub.BoothId, It.IsAny<CancellationToken>())).ReturnsAsync(existingSub);
            _mockRepo.Setup(r => r.UpdateBoothSubscriptionStatusAsync(
                sub.Id, SubscriptionStatus.PendingPayment, SubscriptionStatus.Active,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), "Approved", It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var result = await _service.VerifySubscriptionAsync(sub.Id, PackageType.Booth, request);

            Assert.True(result);
            _mockRepo.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task VerifySubscription_ApproveBooth_ConcurrencyConflict_ThrowsConflict()
        {
            var sub = CreatePendingBoothSubscription();
            var pkg = CreateActiveBoothPackage();
            var request = new VerifySubscriptionRequest { IsApproved = true, AdminNotes = "Approved" };

            _mockRepo.Setup(r => r.GetBoothSubscriptionByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
            _mockRepo.Setup(r => r.GetPackageByIdAsync(sub.PackageId, It.IsAny<CancellationToken>())).ReturnsAsync(pkg);
            _mockRepo.Setup(r => r.GetLatestApprovedBoothSubscriptionAsync(sub.BoothId, It.IsAny<CancellationToken>())).ReturnsAsync((BoothSubscription?)null);
            _mockRepo.Setup(r => r.UpdateBoothSubscriptionStatusAsync(
                sub.Id, SubscriptionStatus.PendingPayment, SubscriptionStatus.Active,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), "Approved", It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.VerifySubscriptionAsync(sub.Id, PackageType.Booth, request));

            Assert.Equal(409, ex.StatusCode);
            Assert.Equal("SUBSCRIPTION_ALREADY_PROCESSED", ex.ErrorCode);
            _mockRepo.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockRepo.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task VerifySubscription_RejectBooth_CancelsSubscription()
        {
            var sub = CreatePendingBoothSubscription();
            var pkg = CreateActiveBoothPackage();
            var request = new VerifySubscriptionRequest { IsApproved = false, AdminNotes = "Invalid payment evidence" };

            _mockRepo.Setup(r => r.GetBoothSubscriptionByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
            _mockRepo.Setup(r => r.GetPackageByIdAsync(sub.PackageId, It.IsAny<CancellationToken>())).ReturnsAsync(pkg);
            _mockRepo.Setup(r => r.UpdateBoothSubscriptionStatusAsync(
                sub.Id, SubscriptionStatus.PendingPayment, SubscriptionStatus.Cancelled,
                sub.StartDate, sub.EndDate, "Invalid payment evidence", It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var result = await _service.VerifySubscriptionAsync(sub.Id, PackageType.Booth, request);

            Assert.True(result);
            _mockRepo.Verify(r => r.CancelActiveBoothSubscriptionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockRepo.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task VerifySubscription_ApproveMarket_CancelsExistingAndActivates()
        {
            var sub = CreatePendingMarketSubscription();
            var pkg = CreateActiveMarketPackage();
            var request = new VerifySubscriptionRequest { IsApproved = true, AdminNotes = "Approved" };

            _mockRepo.Setup(r => r.GetMarketSubscriptionByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
            _mockRepo.Setup(r => r.GetPackageByIdAsync(sub.PackageId, It.IsAny<CancellationToken>())).ReturnsAsync(pkg);
            _mockRepo.Setup(r => r.GetActiveMarketSubscriptionAsync(sub.MarketOwnerId, It.IsAny<CancellationToken>())).ReturnsAsync((MarketSubscription?)null);
            _mockRepo.Setup(r => r.CancelActiveMarketSubscriptionsAsync(sub.MarketOwnerId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockRepo.Setup(r => r.UpdateMarketSubscriptionStatusAsync(
                sub.Id, SubscriptionStatus.PendingPayment, SubscriptionStatus.Active,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), "Approved", It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var result = await _service.VerifySubscriptionAsync(sub.Id, PackageType.Market, request);

            Assert.True(result);
            _mockRepo.Verify(r => r.CancelActiveMarketSubscriptionsAsync(sub.MarketOwnerId, It.IsAny<CancellationToken>()), Times.Never);
            _mockRepo.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task VerifySubscription_MarketNotFound_ThrowsNotFound()
        {
            var subId = Guid.NewGuid();
            var request = new VerifySubscriptionRequest { IsApproved = true };

            _mockRepo.Setup(r => r.GetMarketSubscriptionByIdAsync(subId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((MarketSubscription?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.VerifySubscriptionAsync(subId, PackageType.Market, request));

            Assert.Contains("not found", ex.Message);
            _mockRepo.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task VerifySubscription_MarketAlreadyProcessed_ThrowsConflict()
        {
            var sub = CreatePendingMarketSubscription();
            sub.Status = SubscriptionStatus.Cancelled;
            var request = new VerifySubscriptionRequest { IsApproved = true };

            _mockRepo.Setup(r => r.GetMarketSubscriptionByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.VerifySubscriptionAsync(sub.Id, PackageType.Market, request));

            Assert.Equal(409, ex.StatusCode);
            Assert.Equal("SUBSCRIPTION_ALREADY_PROCESSED", ex.ErrorCode);
        }

        [Fact]
        public async Task VerifySubscription_MarketPackageTypeMismatch_ThrowsBadRequest()
        {
            var sub = CreatePendingMarketSubscription();
            var pkg = CreateActiveBoothPackage();
            var request = new VerifySubscriptionRequest { IsApproved = true };

            _mockRepo.Setup(r => r.GetMarketSubscriptionByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
            _mockRepo.Setup(r => r.GetPackageByIdAsync(sub.PackageId, It.IsAny<CancellationToken>())).ReturnsAsync(pkg);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.VerifySubscriptionAsync(sub.Id, PackageType.Market, request));

            Assert.Contains("type mismatch", ex.Message);
            _mockRepo.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task VerifySubscription_RejectMarket_WithReason_Succeeds()
        {
            var sub = CreatePendingMarketSubscription();
            var pkg = CreateActiveMarketPackage();
            var request = new VerifySubscriptionRequest { IsApproved = false, AdminNotes = "Payment not verified" };

            _mockRepo.Setup(r => r.GetMarketSubscriptionByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
            _mockRepo.Setup(r => r.GetPackageByIdAsync(sub.PackageId, It.IsAny<CancellationToken>())).ReturnsAsync(pkg);
            _mockRepo.Setup(r => r.UpdateMarketSubscriptionStatusAsync(
                sub.Id, SubscriptionStatus.PendingPayment, SubscriptionStatus.Cancelled,
                sub.StartDate, sub.EndDate, "Payment not verified", It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var result = await _service.VerifySubscriptionAsync(sub.Id, PackageType.Market, request);

            Assert.True(result);
            _mockRepo.Verify(r => r.CancelActiveMarketSubscriptionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockRepo.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task VerifySubscription_ApproveBooth_SetsEndDateBasedOnSnapshotDuration()
        {
            var sub = CreatePendingBoothSubscription();
            sub.StartDate = DateTime.UtcNow;
            sub.EndDate = DateTime.UtcNow.AddDays(365);
            var pkg = CreateActiveBoothPackage();
            pkg.DurationDays = 30;
            var request = new VerifySubscriptionRequest { IsApproved = true, AdminNotes = "Approved" };

            DateTime capturedStart = default;
            DateTime capturedEnd = default;

            _mockRepo.Setup(r => r.GetBoothSubscriptionByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
            _mockRepo.Setup(r => r.GetPackageByIdAsync(sub.PackageId, It.IsAny<CancellationToken>())).ReturnsAsync(pkg);
            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(sub.BoothId, It.IsAny<CancellationToken>())).ReturnsAsync((BoothSubscription?)null);
            _mockRepo.Setup(r => r.CancelActiveBoothSubscriptionsAsync(sub.BoothId, It.IsAny<CancellationToken>())).ReturnsAsync(0);
            _mockRepo.Setup(r => r.UpdateBoothSubscriptionStatusAsync(
                sub.Id, SubscriptionStatus.PendingPayment, SubscriptionStatus.Active,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), "Approved", It.IsAny<CancellationToken>()))
                .Callback<Guid, SubscriptionStatus, SubscriptionStatus, DateTime, DateTime, string?, CancellationToken>(
                    (_, _, _, start, end, _, _) => { capturedStart = start; capturedEnd = end; })
                .ReturnsAsync(1);

            await _service.VerifySubscriptionAsync(sub.Id, PackageType.Booth, request);

            var durationDays = (capturedEnd - capturedStart).TotalDays;
            Assert.Equal(365, durationDays);
        }

        [Fact]
        public async Task VerifySubscription_ApproveBooth_PreservesRemainingDaysFromExistingSubscription()
        {
            var sub = CreatePendingBoothSubscription();
            sub.StartDate = DateTime.UtcNow;
            sub.EndDate = DateTime.UtcNow.AddDays(30);
            var pkg = CreateActiveBoothPackage();
            pkg.DurationDays = 30;
            var request = new VerifySubscriptionRequest { IsApproved = true, AdminNotes = "Approved" };

            var existingSub = new BoothSubscription
            {
                Id = Guid.NewGuid(),
                BoothId = sub.BoothId,
                EndDate = DateTime.UtcNow.AddDays(20),
                Status = SubscriptionStatus.Active
            };

            DateTime capturedStart = default;
            DateTime capturedEnd = default;

            _mockRepo.Setup(r => r.GetBoothSubscriptionByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
            _mockRepo.Setup(r => r.GetPackageByIdAsync(sub.PackageId, It.IsAny<CancellationToken>())).ReturnsAsync(pkg);
            _mockRepo.Setup(r => r.GetLatestApprovedBoothSubscriptionAsync(sub.BoothId, It.IsAny<CancellationToken>())).ReturnsAsync(existingSub);
            _mockRepo.Setup(r => r.UpdateBoothSubscriptionStatusAsync(
                sub.Id, SubscriptionStatus.PendingPayment, SubscriptionStatus.Active,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), "Approved", It.IsAny<CancellationToken>()))
                .Callback<Guid, SubscriptionStatus, SubscriptionStatus, DateTime, DateTime, string?, CancellationToken>(
                    (_, _, _, start, end, _, _) => { capturedStart = start; capturedEnd = end; })
                .ReturnsAsync(1);

            await _service.VerifySubscriptionAsync(sub.Id, PackageType.Booth, request);

            var totalDays = (capturedEnd - capturedStart).TotalDays;
            Assert.True(totalDays >= 29 && totalDays <= 31, $"Expected ~30 days for the new sub length, got {totalDays}");
            Assert.True((capturedStart - DateTime.UtcNow).TotalDays >= 19 && (capturedStart - DateTime.UtcNow).TotalDays <= 21, $"Expected start date to be ~20 days from now");
        }
    }
}

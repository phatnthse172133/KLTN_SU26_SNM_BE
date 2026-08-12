using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.AdminModeration;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Exceptions;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer
{
    public class AdminModerationServiceTests
    {
        private readonly Mock<IModerationRepository> _mockModerationRepo;
        private readonly Mock<INightMarketRepository> _mockMarketRepo;
        private readonly Mock<IBoothRepository> _mockBoothRepo;
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly Mock<INotificationService> _mockNotifications;
        private readonly Mock<ILogger<AdminModerationService>> _mockLogger;
        private readonly AdminModerationService _service;

        private static readonly Guid AdminId = Guid.NewGuid();
        private const string AdminName = "Test Admin";

        public AdminModerationServiceTests()
        {
            _mockModerationRepo = new Mock<IModerationRepository>();
            _mockMarketRepo = new Mock<INightMarketRepository>();
            _mockBoothRepo = new Mock<IBoothRepository>();
            _mockUserRepo = new Mock<IUserRepository>();
            _mockNotifications = new Mock<INotificationService>();
            _mockLogger = new Mock<ILogger<AdminModerationService>>();

            _service = new AdminModerationService(
                _mockModerationRepo.Object,
                _mockMarketRepo.Object,
                _mockBoothRepo.Object,
                _mockUserRepo.Object,
                _mockNotifications.Object,
                _mockLogger.Object
            );
        }

        // ─── Helpers ───────────────────────────────────────────────

        private static NightMarket CreateMarket(ModerationStatus modStatus = ModerationStatus.Active)
        {
            return new NightMarket
            {
                Id = Guid.NewGuid(),
                MarketOwnerId = Guid.NewGuid(),
                Name = "Test Night Market",
                Address = "123 Test Street",
                Status = NightMarketStatus.Active,
                ModerationStatus = modStatus,
                TotalBooth = 10,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                MarketOwner = new User { Id = Guid.NewGuid(), FullName = "Market Owner", Email = "owner@test.com", Phone = "0123456789" }
            };
        }

        private static Booth CreateBooth(BoothStatus status = BoothStatus.Active)
        {
            return new Booth
            {
                Id = Guid.NewGuid(),
                NightMarketId = Guid.NewGuid(),
                BoothOwnerId = Guid.NewGuid(),
                BoothName = "Test Booth",
                BoothCode = "B001",
                Status = status,
                AverageRating = 4.5m,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                BoothOwner = new User { Id = Guid.NewGuid(), FullName = "Booth Owner", Email = "booth@test.com" },
                NightMarket = new NightMarket { Id = Guid.NewGuid(), Name = "Test Market" },
                Zone = new Zone { ZoneName = "Zone A" }
            };
        }

        private static ChangeModerationStatusRequest CreateSuspendRequest()
            => new() { Status = ModerationStatus.Suspended, Reason = "This is a valid suspension reason." };

        private static ChangeModerationStatusRequest CreateRestoreRequest()
            => new() { Status = ModerationStatus.Active, Reason = "This is a valid restore reason." };

        private static BoothModerationActionRequest CreateBanRequest()
            => new() { Reason = "This is a valid ban reason." };

        private static BoothModerationActionRequest CreateBoothRestoreRequest()
            => new() { Reason = "This is a valid restore reason." };

        // ─── Night Market Tests ─────────────────────────────────────

        [Fact]
        public async Task GetMarketsAsync_ReturnsPagedResult()
        {
            var market = CreateMarket();
            var paged = new PagedResult<NightMarket>(new List<NightMarket> { market }, 1);

            _mockModerationRepo.Setup(r => r.GetMarketsPagedAsync(
                It.IsAny<string?>(), It.IsAny<NightMarketStatus?>(), It.IsAny<ModerationStatus?>(),
                It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(paged);

            _mockUserRepo.Setup(r => r.GetUserNamesByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string> { { market.MarketOwnerId!.Value, "Market Owner" } });
            _mockModerationRepo.Setup(r => r.CountActiveBoothsByMarketIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, int> { { market.Id, 5 } });
            _mockModerationRepo.Setup(r => r.CountBoothsByMarketIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, int> { { market.Id, 8 } });
            _mockModerationRepo.Setup(r => r.CountSeriousComplaintsByMarketIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, int> { { market.Id, 2 } });

            var request = new AdminMarketModerationQueryRequest { Page = 1, PageSize = 10 };
            var result = await _service.GetMarketsAsync(request);

            Assert.True(result.Success);
            Assert.Single(result.Data!.Items);
            Assert.Equal("Test Night Market", result.Data.Items.First().MarketName);
            Assert.Equal("Market Owner", result.Data.Items.First().MarketOwnerName);
            Assert.Equal(8, result.Data.Items.First().TotalBooths);
            Assert.Equal(5, result.Data.Items.First().ActiveBooths);
            Assert.Equal(2, result.Data.Items.First().SeriousComplaintCount);
        }

        [Fact]
        public async Task GetMarketDetailAsync_NotFound_Throws()
        {
            _mockModerationRepo.Setup(r => r.GetMarketDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NightMarket?)null);

            await Assert.ThrowsAsync<AppException>(() => _service.GetMarketDetailAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task GetMarketDetailAsync_ReturnsDetail()
        {
            var market = CreateMarket();
            _mockModerationRepo.Setup(r => r.GetMarketDetailAsync(market.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(market);
            _mockModerationRepo.Setup(r => r.CountActiveBoothsByMarketAsync(market.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(5);
            _mockModerationRepo.Setup(r => r.CountBoothsByMarketAsync(market.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(8);
            _mockModerationRepo.Setup(r => r.CountSeriousComplaintsByMarketAsync(market.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);
            _mockModerationRepo.Setup(r => r.GetRecentComplaintsByMarketAsync(market.Id, 5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Complaint>());

            var result = await _service.GetMarketDetailAsync(market.Id);

            Assert.True(result.Success);
            Assert.Equal("Test Night Market", result.Data!.MarketName);
            Assert.Equal(8, result.Data.TotalBooths);
            Assert.Equal(5, result.Data.ActiveBooths);
            Assert.Equal(2, result.Data.SeriousComplaintCount);
        }

        [Fact]
        public async Task ChangeMarketModerationStatus_Suspend_Succeeds()
        {
            var market = CreateMarket(ModerationStatus.Active);
            var request = CreateSuspendRequest();

            _mockModerationRepo.Setup(r => r.GetMarketDetailAsync(market.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(market);
            _mockModerationRepo.Setup(r => r.UpdateMarketModerationStatusAsync(
                market.Id, ModerationStatus.Active, ModerationStatus.Suspended, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var result = await _service.ChangeMarketModerationStatusAsync(AdminId, AdminName, market.Id, request);

            Assert.True(result.Success);
            Assert.True(result.Data!.Success);
            _mockModerationRepo.Verify(r => r.AddAsync(It.Is<ModerationActionHistory>(h =>
                h.NightMarketId == market.Id && h.NewStatus == "Suspended" && h.PreviousStatus == "Active")), Times.Once);
            _mockModerationRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
            _mockNotifications.Verify(n => n.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ChangeMarketModerationStatus_Restore_Succeeds()
        {
            var market = CreateMarket(ModerationStatus.Suspended);
            var request = CreateRestoreRequest();

            _mockModerationRepo.Setup(r => r.GetMarketDetailAsync(market.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(market);
            _mockModerationRepo.Setup(r => r.UpdateMarketModerationStatusAsync(
                market.Id, ModerationStatus.Suspended, ModerationStatus.Active, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var result = await _service.ChangeMarketModerationStatusAsync(AdminId, AdminName, market.Id, request);

            Assert.True(result.Success);
            _mockModerationRepo.Verify(r => r.AddAsync(It.Is<ModerationActionHistory>(h =>
                h.NewStatus == "Active" && h.PreviousStatus == "Suspended")), Times.Once);
        }

        [Fact]
        public async Task ChangeMarketModerationStatus_NotFound_Throws()
        {
            _mockModerationRepo.Setup(r => r.GetMarketDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NightMarket?)null);

            await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangeMarketModerationStatusAsync(AdminId, AdminName, Guid.NewGuid(), CreateSuspendRequest()));
        }

        [Fact]
        public async Task ChangeMarketModerationStatus_AlreadySuspended_Throws()
        {
            var market = CreateMarket(ModerationStatus.Suspended);
            _mockModerationRepo.Setup(r => r.GetMarketDetailAsync(market.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(market);

            await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangeMarketModerationStatusAsync(AdminId, AdminName, market.Id, CreateSuspendRequest()));
        }

        [Fact]
        public async Task ChangeMarketModerationStatus_ConcurrencyConflict_Throws()
        {
            var market = CreateMarket(ModerationStatus.Active);
            _mockModerationRepo.Setup(r => r.GetMarketDetailAsync(market.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(market);
            _mockModerationRepo.Setup(r => r.UpdateMarketModerationStatusAsync(
                It.IsAny<Guid>(), It.IsAny<ModerationStatus>(), It.IsAny<ModerationStatus>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0); // Simulate concurrency conflict

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangeMarketModerationStatusAsync(AdminId, AdminName, market.Id, CreateSuspendRequest()));
            Assert.Equal("MODERATION_CONFLICT", ex.ErrorCode);
        }

        [Fact]
        public async Task ChangeMarketModerationStatus_ExpectedUpdatedAtMismatch_Throws()
        {
            var market = CreateMarket(ModerationStatus.Active);
            market.UpdatedAt = DateTime.UtcNow;
            var request = new ChangeModerationStatusRequest
            {
                Status = ModerationStatus.Suspended,
                Reason = "Valid suspension reason here.",
                ExpectedUpdatedAt = DateTime.UtcNow.AddDays(-5)
            };

            _mockModerationRepo.Setup(r => r.GetMarketDetailAsync(market.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(market);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangeMarketModerationStatusAsync(AdminId, AdminName, market.Id, request));
            Assert.Equal("MODERATION_CONFLICT", ex.ErrorCode);
        }

        [Fact]
        public async Task ChangeMarketModerationStatus_ShortReason_Throws()
        {
            var market = CreateMarket(ModerationStatus.Active);
            _mockModerationRepo.Setup(r => r.GetMarketDetailAsync(market.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(market);

            var request = new ChangeModerationStatusRequest
            {
                Status = ModerationStatus.Suspended,
                Reason = "short"
            };

            await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangeMarketModerationStatusAsync(AdminId, AdminName, market.Id, request));
        }

        [Fact]
        public async Task GetMarketHistoryAsync_ReturnsPagedHistory()
        {
            var history = new ModerationActionHistory
            {
                Id = Guid.NewGuid(),
                NightMarketId = Guid.NewGuid(),
                AdminId = AdminId,
                AdminName = AdminName,
                PreviousStatus = "Active",
                NewStatus = "Suspended",
                Reason = "Test reason for suspension",
                Source = ModerationActionSource.DirectAdmin,
                CreatedAt = DateTime.UtcNow
            };
            var paged = new PagedResult<ModerationActionHistory>(new List<ModerationActionHistory> { history }, 1);

            _mockModerationRepo.Setup(r => r.GetHistoryByMarketAsync(It.IsAny<Guid>(), 1, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(paged);
            _mockModerationRepo.Setup(r => r.GetMarketDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateMarket());

            var result = await _service.GetMarketHistoryAsync(Guid.NewGuid(), 1, 10);

            Assert.True(result.Success);
            Assert.Single(result.Data!.Items);
            Assert.Equal("Suspended", result.Data.Items.First().NewStatus);
        }

        // ─── Booth Tests ────────────────────────────────────────────

        [Fact]
        public async Task GetBoothsAsync_ReturnsPagedResult()
        {
            var booth = CreateBooth();
            var paged = new PagedResult<Booth>(new List<Booth> { booth }, 1);

            _mockModerationRepo.Setup(r => r.GetBoothsPagedAsync(
                It.IsAny<string?>(), It.IsAny<BoothStatus?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(paged);
            _mockModerationRepo.Setup(r => r.CountComplaintsByBoothIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, int> { { booth.Id, 3 } });

            var request = new AdminBoothModerationQueryRequest { Page = 1, PageSize = 10 };
            var result = await _service.GetBoothsAsync(request);

            Assert.True(result.Success);
            Assert.Single(result.Data!.Items);
            Assert.Equal("Test Booth", result.Data.Items.First().BoothName);
            Assert.Equal(3, result.Data.Items.First().ComplaintCount);
        }

        [Theory]
        [InlineData(BoothStatus.Active)]
        [InlineData(BoothStatus.Inactive)]
        [InlineData(BoothStatus.Banned)]
        public async Task GetBoothsAsync_ReturnsActualBoothStatus(BoothStatus status)
        {
            var booth = CreateBooth(status);
            var paged = new PagedResult<Booth>(new List<Booth> { booth }, 1);

            _mockModerationRepo.Setup(r => r.GetBoothsPagedAsync(
                    It.IsAny<string?>(), It.IsAny<BoothStatus?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(paged);
            _mockModerationRepo.Setup(r => r.CountComplaintsByBoothIdsAsync(
                    It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, int>());

            var result = await _service.GetBoothsAsync(
                new AdminBoothModerationQueryRequest { Page = 1, PageSize = 10 });

            Assert.True(result.Success);
            Assert.Equal(status.ToString(), result.Data!.Items.Single().Status);
        }

        [Fact]
        public async Task GetBoothDetailAsync_NotFound_Throws()
        {
            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Booth?)null);

            await Assert.ThrowsAsync<AppException>(() => _service.GetBoothDetailAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task GetBoothDetailAsync_ReturnsDetail()
        {
            var booth = CreateBooth();
            var documentCreatedAt = DateTime.UtcNow.AddDays(-3);
            booth.BoothDocuments = new List<BoothDocument>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    BoothId = booth.Id,
                    DocumentType = BoothDocumentType.BusinessLicense,
                    DocumentUrl = "/uploads/documents/license.pdf",
                    FileUrl = "/uploads/documents/license.pdf",
                    VerificationStatus = BoothDocumentStatus.PendingReview,
                    CreatedAt = documentCreatedAt,
                    UpdatedAt = documentCreatedAt
                }
            };
            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booth);
            _mockModerationRepo.Setup(r => r.CountComplaintsByBoothAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);
            _mockModerationRepo.Setup(r => r.GetRecentComplaintsByBoothAsync(booth.Id, 5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Complaint>());

            var result = await _service.GetBoothDetailAsync(booth.Id);

            Assert.True(result.Success);
            Assert.Equal("Test Booth", result.Data!.BoothName);
            Assert.Equal(2, result.Data.ComplaintCount);
            var document = Assert.Single(result.Data.Documents);
            Assert.Equal("/uploads/documents/license.pdf", document.FileUrl);
            Assert.Equal(documentCreatedAt, document.CreatedAt);
        }

        [Fact]
        public async Task GetBoothDetailAsync_BoothLinkedDocuments_Returned()
        {
            var booth = CreateBooth();
            booth.BoothDocuments = new List<BoothDocument>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    BoothId = booth.Id,
                    DocumentType = BoothDocumentType.FoodSafetyCertificate,
                    DocumentUrl = "/uploads/images/booth-documents/direct.pdf",
                    FileUrl = "/uploads/images/booth-documents/direct.pdf",
                    VerificationStatus = BoothDocumentStatus.PendingReview,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };
            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booth);
            _mockModerationRepo.Setup(r => r.CountComplaintsByBoothAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
            _mockModerationRepo.Setup(r => r.GetRecentComplaintsByBoothAsync(booth.Id, 5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Complaint>());

            var result = await _service.GetBoothDetailAsync(booth.Id);

            var document = Assert.Single(result.Data!.Documents);
            Assert.Equal("/uploads/images/booth-documents/direct.pdf", document.FileUrl);
        }

        [Fact]
        public async Task GetBoothDetailAsync_MultipleBoothDocuments_AllReturned()
        {
            var booth = CreateBooth();
            var licenseDocument = new BoothDocument
            {
                Id = Guid.NewGuid(),
                BoothId = booth.Id,
                DocumentType = BoothDocumentType.BusinessLicense,
                DocumentUrl = "/uploads/images/booth-documents/license.png",
                FileUrl = "/uploads/images/booth-documents/license.png",
                VerificationStatus = BoothDocumentStatus.Verified,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            };
            var idDocument = new BoothDocument
            {
                Id = Guid.NewGuid(),
                BoothId = booth.Id,
                DocumentType = BoothDocumentType.OwnerIdentification,
                DocumentUrl = "/uploads/images/booth-documents/id.png",
                FileUrl = "/uploads/images/booth-documents/id.png",
                VerificationStatus = BoothDocumentStatus.PendingReview,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };
            booth.BoothDocuments = new List<BoothDocument> { licenseDocument, idDocument };
            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booth);
            _mockModerationRepo.Setup(r => r.CountComplaintsByBoothAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
            _mockModerationRepo.Setup(r => r.GetRecentComplaintsByBoothAsync(booth.Id, 5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Complaint>());

            var result = await _service.GetBoothDetailAsync(booth.Id);

            Assert.Equal(2, result.Data!.Documents.Count);
            Assert.Single(result.Data.Documents, d => d.Id == licenseDocument.Id);
            Assert.Single(result.Data.Documents, d => d.Id == idDocument.Id);
        }

        [Theory]
        [InlineData(BoothStatus.Inactive)]
        [InlineData(BoothStatus.Banned)]
        public async Task GetBoothDetailAsync_ReturnsActualStatus(BoothStatus status)
        {
            var booth = CreateBooth(status);
            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booth);
            _mockModerationRepo.Setup(r => r.CountComplaintsByBoothAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
            _mockModerationRepo.Setup(r => r.GetRecentComplaintsByBoothAsync(
                    booth.Id, 5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Complaint>());

            var result = await _service.GetBoothDetailAsync(booth.Id);

            Assert.True(result.Success);
            Assert.Equal(status.ToString(), result.Data!.Status);
        }

        [Theory]
        [InlineData(BoothStatus.Active)]
        [InlineData(BoothStatus.Inactive)]
        public async Task BanBooth_FromActiveOrInactive_SetsBannedAndWritesHistory(BoothStatus previousStatus)
        {
            var booth = CreateBooth(previousStatus);
            var request = CreateBanRequest();

            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booth);
            _mockModerationRepo.Setup(r => r.UpdateBoothStatusAsync(
                booth.Id, previousStatus, BoothStatus.Banned, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var result = await _service.BanBoothAsync(AdminId, AdminName, booth.Id, request);

            Assert.True(result.Success);
            Assert.True(result.Data!.Success);
            _mockModerationRepo.Verify(r => r.AddAsync(It.Is<ModerationActionHistory>(h =>
                h.BoothId == booth.Id && h.NewStatus == "Banned" && h.PreviousStatus == previousStatus.ToString())), Times.Once);
            _mockModerationRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
            _mockNotifications.Verify(n => n.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RestoreBooth_FromBanned_SetsActiveAndWritesHistory()
        {
            var booth = CreateBooth(BoothStatus.Banned);
            var request = CreateBoothRestoreRequest();

            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booth);
            _mockModerationRepo.Setup(r => r.UpdateBoothStatusAsync(
                booth.Id, BoothStatus.Banned, BoothStatus.Active, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var result = await _service.RestoreBoothAsync(AdminId, AdminName, booth.Id, request);

            Assert.True(result.Success);
            _mockModerationRepo.Verify(r => r.AddAsync(It.Is<ModerationActionHistory>(h =>
                h.NewStatus == "Active" && h.PreviousStatus == "Banned")), Times.Once);
            _mockNotifications.Verify(n => n.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BanBooth_NotFound_Throws()
        {
            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Booth?)null);

            await Assert.ThrowsAsync<AppException>(() =>
                _service.BanBoothAsync(AdminId, AdminName, Guid.NewGuid(), CreateBanRequest()));
        }

        [Fact]
        public async Task BanBooth_AlreadyBanned_Throws()
        {
            var booth = CreateBooth(BoothStatus.Banned);
            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booth);

            await Assert.ThrowsAsync<AppException>(() =>
                _service.BanBoothAsync(AdminId, AdminName, booth.Id, CreateBanRequest()));
        }

        [Fact]
        public async Task RestoreBooth_NotBanned_Throws()
        {
            var booth = CreateBooth(BoothStatus.Active);
            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booth);

            await Assert.ThrowsAsync<AppException>(() =>
                _service.RestoreBoothAsync(AdminId, AdminName, booth.Id, CreateBoothRestoreRequest()));
        }

        [Fact]
        public async Task BanBooth_ShortReason_Throws()
        {
            var booth = CreateBooth(BoothStatus.Active);
            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booth);

            await Assert.ThrowsAsync<AppException>(() =>
                _service.BanBoothAsync(AdminId, AdminName, booth.Id, new BoothModerationActionRequest { Reason = "short" }));
        }

        [Fact]
        public async Task BanBooth_TooLongReason_Throws()
        {
            var booth = CreateBooth(BoothStatus.Active);
            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booth);

            await Assert.ThrowsAsync<AppException>(() =>
                _service.BanBoothAsync(AdminId, AdminName, booth.Id, new BoothModerationActionRequest { Reason = new string('x', 1001) }));
        }

        [Fact]
        public async Task BanBooth_ConcurrencyConflict_Throws()
        {
            var booth = CreateBooth(BoothStatus.Active);
            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booth);
            _mockModerationRepo.Setup(r => r.UpdateBoothStatusAsync(
                It.IsAny<Guid>(), It.IsAny<BoothStatus>(), It.IsAny<BoothStatus>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.BanBoothAsync(AdminId, AdminName, booth.Id, CreateBanRequest()));
            Assert.Equal("MODERATION_CONFLICT", ex.ErrorCode);
        }

        [Fact]
        public async Task GetBoothHistoryAsync_ReturnsPagedHistory()
        {
            var history = new ModerationActionHistory
            {
                Id = Guid.NewGuid(),
                BoothId = Guid.NewGuid(),
                AdminId = AdminId,
                AdminName = AdminName,
                PreviousStatus = "Active",
                NewStatus = "Suspended",
                Reason = "Test reason for suspension",
                Source = ModerationActionSource.DirectAdmin,
                CreatedAt = DateTime.UtcNow
            };
            var paged = new PagedResult<ModerationActionHistory>(new List<ModerationActionHistory> { history }, 1);

            _mockModerationRepo.Setup(r => r.GetHistoryByBoothAsync(It.IsAny<Guid>(), 1, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(paged);
            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateBooth());

            var result = await _service.GetBoothHistoryAsync(Guid.NewGuid(), 1, 10);

            Assert.True(result.Success);
            Assert.Single(result.Data!.Items);
            Assert.Equal("Suspended", result.Data.Items.First().NewStatus);
        }

        [Fact]
        public async Task BanBooth_WithComplaintId_SetsComplaintSource()
        {
            var booth = CreateBooth(BoothStatus.Active);
            var complaintId = Guid.NewGuid();
            var request = new BoothModerationActionRequest
            {
                Reason = "Valid ban reason for complaint.",
                ComplaintId = complaintId
            };

            _mockModerationRepo.Setup(r => r.GetBoothDetailAsync(booth.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booth);
            _mockModerationRepo.Setup(r => r.UpdateBoothStatusAsync(
                booth.Id, BoothStatus.Active, BoothStatus.Banned, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            await _service.BanBoothAsync(AdminId, AdminName, booth.Id, request);

            _mockModerationRepo.Verify(r => r.AddAsync(It.Is<ModerationActionHistory>(h =>
                h.Source == ModerationActionSource.Complaint && h.ComplaintId == complaintId)), Times.Once);
        }

        [Fact]
        public async Task ChangeMarketModerationStatus_InvalidStatus_Throws()
        {
            var market = CreateMarket(ModerationStatus.Active);
            _mockModerationRepo.Setup(r => r.GetMarketDetailAsync(market.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(market);

            var request = new ChangeModerationStatusRequest
            {
                Status = (ModerationStatus)99,
                Reason = "This is a valid suspension reason."
            };

            await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangeMarketModerationStatusAsync(AdminId, AdminName, market.Id, request));
        }

    }
}

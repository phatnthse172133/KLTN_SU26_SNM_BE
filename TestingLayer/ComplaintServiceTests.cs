using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using ApplicationLayer.Services.Complaints;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Exceptions;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer
{
    public class ComplaintServiceTests
    {
        private readonly Mock<IComplaintRepository> _mockComplaints;
        private readonly Mock<IBoothRepository> _mockBooths;
        private readonly Mock<IOrderRepository> _mockOrders;
        private readonly Mock<INightMarketRepository> _mockNightMarkets;
        private readonly Mock<ISubscriptionRepository> _mockSubscriptions;
        private readonly Mock<IModerationRepository> _mockModeration;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<INotificationService> _mockNotifications;
        private readonly ComplaintService _service;

        public ComplaintServiceTests()
        {
            _mockComplaints = new Mock<IComplaintRepository>();
            _mockBooths = new Mock<IBoothRepository>();
            _mockOrders = new Mock<IOrderRepository>();
            _mockNightMarkets = new Mock<INightMarketRepository>();
            _mockSubscriptions = new Mock<ISubscriptionRepository>();
            _mockModeration = new Mock<IModerationRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockNotifications = new Mock<INotificationService>();

            _service = new ComplaintService(
                _mockComplaints.Object,
                _mockBooths.Object,
                _mockOrders.Object,
                _mockNightMarkets.Object,
                _mockSubscriptions.Object,
                _mockModeration.Object,
                _mockMapper.Object,
                _mockNotifications.Object
            );
        }

        private Complaint CreatePendingComplaint()
        {
            return new Complaint
            {
                Id = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                BoothId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                Title = "Test complaint",
                Description = "Test description",
                Status = ComplaintStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };
        }

        private UpdateComplaintStatusRequest CreateResolveRequest(ComplaintResolutionAction action = ComplaintResolutionAction.NoViolation, string? policyViolation = null)
        {
            return new UpdateComplaintStatusRequest
            {
                Status = ComplaintStatus.Resolved,
                AdminResponse = "This is a valid admin response for testing.",
                ResolutionAction = action,
                PolicyViolation = policyViolation
            };
        }

        private UpdateComplaintStatusRequest CreateRejectRequest()
        {
            return new UpdateComplaintStatusRequest
            {
                Status = ComplaintStatus.Rejected,
                AdminResponse = "This is a valid rejection reason for testing.",
                ResolutionAction = null,
                PolicyViolation = null
            };
        }

        [Fact]
        public async Task UpdateStatusAsync_ResolveWithNoViolation_Succeeds()
        {
            var complaint = CreatePendingComplaint();
            var request = CreateResolveRequest(ComplaintResolutionAction.NoViolation);

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);
            _mockComplaints.Setup(r => r.UpdateStatusWithConcurrencyAsync(
                complaint.Id, ComplaintStatus.Pending, ComplaintStatus.Resolved,
                It.IsAny<string?>(), ComplaintResolutionAction.NoViolation, null, It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockComplaints.Setup(r => r.GetWithImagesByIdAsync(complaint.Id)).ReturnsAsync(complaint);
            _mockMapper.Setup(m => m.Map<ComplaintResponse>(It.IsAny<Complaint>())).Returns(new ComplaintResponse());

            var result = await _service.UpdateStatusAsync(complaint.Id, request);

            Assert.True(result.Success);
            _mockComplaints.Verify(r => r.CommitTransactionAsync(), Times.Once);
            _mockComplaints.Verify(r => r.RollbackTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdateStatusAsync_Reject_Succeeds_WithNullResolutionAction()
        {
            var complaint = CreatePendingComplaint();
            var request = CreateRejectRequest();

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);
            _mockComplaints.Setup(r => r.UpdateStatusWithConcurrencyAsync(
                complaint.Id, ComplaintStatus.Pending, ComplaintStatus.Rejected,
                It.IsAny<string?>(), null, null, It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockComplaints.Setup(r => r.GetWithImagesByIdAsync(complaint.Id)).ReturnsAsync(complaint);
            _mockMapper.Setup(m => m.Map<ComplaintResponse>(It.IsAny<Complaint>())).Returns(new ComplaintResponse());

            var result = await _service.UpdateStatusAsync(complaint.Id, request);

            Assert.True(result.Success);
            _mockComplaints.Verify(r => r.UpdateStatusWithConcurrencyAsync(
                complaint.Id, ComplaintStatus.Pending, ComplaintStatus.Rejected,
                It.IsAny<string?>(), null, null, It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task UpdateStatusAsync_AlreadyProcessed_Returns409()
        {
            var complaint = CreatePendingComplaint();
            var request = CreateResolveRequest();

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);
            _mockComplaints.Setup(r => r.UpdateStatusWithConcurrencyAsync(
                It.IsAny<Guid>(), It.IsAny<ComplaintStatus>(), It.IsAny<ComplaintStatus>(),
                It.IsAny<string?>(), It.IsAny<ComplaintResolutionAction?>(), It.IsAny<string?>(), It.IsAny<DateTime>()))
                .ReturnsAsync(0);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStatusAsync(complaint.Id, request));

            Assert.Equal(409, ex.StatusCode);
            Assert.Equal("COMPLAINT_ALREADY_PROCESSED", ex.ErrorCode);
            _mockComplaints.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            _mockComplaints.Verify(r => r.CommitTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdateStatusAsync_ResolvedComplaint_Returns409()
        {
            var complaint = CreatePendingComplaint();
            complaint.Status = ComplaintStatus.Resolved;
            var request = CreateResolveRequest();

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStatusAsync(complaint.Id, request));

            Assert.Equal(409, ex.StatusCode);
            Assert.Equal("COMPLAINT_ALREADY_PROCESSED", ex.ErrorCode);
        }

        [Fact]
        public async Task UpdateStatusAsync_RejectedComplaint_Returns409()
        {
            var complaint = CreatePendingComplaint();
            complaint.Status = ComplaintStatus.Rejected;
            var request = CreateRejectRequest();

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStatusAsync(complaint.Id, request));

            Assert.Equal(409, ex.StatusCode);
            Assert.Equal("COMPLAINT_ALREADY_PROCESSED", ex.ErrorCode);
        }

        [Fact]
        public async Task UpdateStatusAsync_TargetStatusPending_Returns400()
        {
            var complaint = CreatePendingComplaint();
            var request = new UpdateComplaintStatusRequest
            {
                Status = ComplaintStatus.Pending,
                AdminResponse = "This is a valid admin response for testing.",
                ResolutionAction = ComplaintResolutionAction.NoViolation
            };

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStatusAsync(complaint.Id, request));

            Assert.Equal(400, ex.StatusCode);
            _mockComplaints.Verify(r => r.UpdateStatusWithConcurrencyAsync(
                It.IsAny<Guid>(), It.IsAny<ComplaintStatus>(), It.IsAny<ComplaintStatus>(),
                It.IsAny<string?>(), It.IsAny<ComplaintResolutionAction?>(), It.IsAny<string?>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task UpdateStatusAsync_InvalidTargetStatus99_Returns400()
        {
            var complaint = CreatePendingComplaint();
            var request = new UpdateComplaintStatusRequest
            {
                Status = (ComplaintStatus)99,
                AdminResponse = "This is a valid admin response for testing.",
                ResolutionAction = ComplaintResolutionAction.NoViolation
            };

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStatusAsync(complaint.Id, request));

            Assert.Equal(400, ex.StatusCode);
            _mockComplaints.Verify(r => r.UpdateStatusWithConcurrencyAsync(
                It.IsAny<Guid>(), It.IsAny<ComplaintStatus>(), It.IsAny<ComplaintStatus>(),
                It.IsAny<string?>(), It.IsAny<ComplaintResolutionAction?>(), It.IsAny<string?>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task UpdateStatusAsync_RejectWithResolutionAction_Throws400()
        {
            var complaint = CreatePendingComplaint();
            var request = new UpdateComplaintStatusRequest
            {
                Status = ComplaintStatus.Rejected,
                AdminResponse = "This is a valid rejection reason for testing.",
                ResolutionAction = ComplaintResolutionAction.Warning,
                PolicyViolation = "some violation"
            };

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStatusAsync(complaint.Id, request));

            Assert.Contains("cannot apply a booth penalty", ex.Message);
        }

        [Fact]
        public async Task UpdateStatusAsync_Reject_DoesNotUpdateBooth()
        {
            var complaint = CreatePendingComplaint();
            var request = CreateRejectRequest();

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);
            _mockComplaints.Setup(r => r.UpdateStatusWithConcurrencyAsync(
                It.IsAny<Guid>(), It.IsAny<ComplaintStatus>(), It.IsAny<ComplaintStatus>(),
                It.IsAny<string?>(), null, null, It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockComplaints.Setup(r => r.GetWithImagesByIdAsync(complaint.Id)).ReturnsAsync(complaint);
            _mockBooths.Setup(r => r.GetByIdAsync(complaint.BoothId)).ReturnsAsync((Booth?)null);
            _mockMapper.Setup(m => m.Map<ComplaintResponse>(It.IsAny<Complaint>())).Returns(new ComplaintResponse());

            await _service.UpdateStatusAsync(complaint.Id, request);

            _mockBooths.Verify(r => r.Update(It.IsAny<Booth>()), Times.Never);
            _mockBooths.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdateStatusAsync_SuspendBooth_UpdatesBoothInTransaction()
        {
            var complaint = CreatePendingComplaint();
            var request = CreateResolveRequest(ComplaintResolutionAction.SuspendBooth, "Health violation detected");

            var booth = new Booth { Id = complaint.BoothId, Status = BoothStatus.Active };

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);
            _mockComplaints.Setup(r => r.UpdateStatusWithConcurrencyAsync(
                It.IsAny<Guid>(), It.IsAny<ComplaintStatus>(), It.IsAny<ComplaintStatus>(),
                It.IsAny<string?>(), ComplaintResolutionAction.SuspendBooth, It.IsAny<string?>(), It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockBooths.Setup(r => r.GetByIdAsync(complaint.BoothId)).ReturnsAsync(booth);
            _mockComplaints.Setup(r => r.GetWithImagesByIdAsync(complaint.Id)).ReturnsAsync(complaint);
            _mockMapper.Setup(m => m.Map<ComplaintResponse>(It.IsAny<Complaint>())).Returns(new ComplaintResponse());

            await _service.UpdateStatusAsync(complaint.Id, request);

            Assert.Equal(BoothStatus.Banned, booth.Status);
            _mockBooths.Verify(r => r.Update(booth), Times.Once);
            _mockBooths.Verify(r => r.SaveChangesAsync(), Times.Once);
            _mockComplaints.Verify(r => r.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateStatusAsync_CloseBoothAction_Throws400()
        {
            var complaint = CreatePendingComplaint();
            var request = new UpdateComplaintStatusRequest
            {
                Status = ComplaintStatus.Resolved,
                AdminResponse = "This is a valid admin response for testing.",
                ResolutionAction = ComplaintResolutionAction.CloseBooth,
                PolicyViolation = "Serious violation"
            };

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStatusAsync(complaint.Id, request));

            Assert.Contains("Invalid resolution action", ex.Message);
        }

        [Fact]
        public async Task UpdateStatusAsync_InvalidResolutionAction99_Throws400()
        {
            var complaint = CreatePendingComplaint();
            var request = new UpdateComplaintStatusRequest
            {
                Status = ComplaintStatus.Resolved,
                AdminResponse = "This is a valid admin response for testing.",
                ResolutionAction = (ComplaintResolutionAction)99,
                PolicyViolation = "some violation"
            };

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStatusAsync(complaint.Id, request));

            Assert.Contains("Invalid resolution action", ex.Message);
        }

        [Fact]
        public async Task UpdateStatusAsync_ShortAdminResponse_Throws400()
        {
            var complaint = CreatePendingComplaint();
            var request = new UpdateComplaintStatusRequest
            {
                Status = ComplaintStatus.Resolved,
                AdminResponse = "short",
                ResolutionAction = ComplaintResolutionAction.NoViolation
            };

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStatusAsync(complaint.Id, request));

            Assert.Contains("at least 10 characters", ex.Message);
        }

        [Fact]
        public async Task UpdateStatusAsync_WarningWithoutPolicyViolation_Throws400()
        {
            var complaint = CreatePendingComplaint();
            var request = CreateResolveRequest(ComplaintResolutionAction.Warning, null);

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStatusAsync(complaint.Id, request));

            Assert.Contains("Policy violation is required", ex.Message);
        }

        [Fact]
        public async Task UpdateStatusAsync_NotFound_Throws404()
        {
            var complaintId = Guid.NewGuid();

            _mockComplaints.Setup(r => r.GetByIdAsync(complaintId)).ReturnsAsync((Complaint?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStatusAsync(complaintId, CreateResolveRequest()));

            Assert.Contains("not found", ex.Message);
        }

        [Fact]
        public async Task UpdateStatusAsync_NotificationError_DoesNotFailComplaintUpdate()
        {
            var complaint = CreatePendingComplaint();
            var request = CreateResolveRequest();

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);
            _mockComplaints.Setup(r => r.UpdateStatusWithConcurrencyAsync(
                It.IsAny<Guid>(), It.IsAny<ComplaintStatus>(), It.IsAny<ComplaintStatus>(),
                It.IsAny<string?>(), It.IsAny<ComplaintResolutionAction?>(), It.IsAny<string?>(), It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockComplaints.Setup(r => r.GetWithImagesByIdAsync(complaint.Id)).ReturnsAsync(complaint);
            _mockMapper.Setup(m => m.Map<ComplaintResponse>(It.IsAny<Complaint>())).Returns(new ComplaintResponse());
            _mockNotifications.Setup(n => n.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Notification service down"));

            var result = await _service.UpdateStatusAsync(complaint.Id, request);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task UpdateStatusAsync_BoothUpdateError_RollsBackTransaction()
        {
            var complaint = CreatePendingComplaint();
            var request = CreateResolveRequest(ComplaintResolutionAction.SuspendBooth, "Health violation");

            var booth = new Booth { Id = complaint.BoothId, Status = BoothStatus.Active };

            _mockComplaints.Setup(r => r.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);
            _mockComplaints.Setup(r => r.UpdateStatusWithConcurrencyAsync(
                It.IsAny<Guid>(), It.IsAny<ComplaintStatus>(), It.IsAny<ComplaintStatus>(),
                It.IsAny<string?>(), ComplaintResolutionAction.SuspendBooth, It.IsAny<string?>(), It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockBooths.Setup(r => r.GetByIdAsync(complaint.BoothId)).ReturnsAsync(booth);
            _mockBooths.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new Exception("DB connection lost"));

            await Assert.ThrowsAsync<Exception>(() =>
                _service.UpdateStatusAsync(complaint.Id, request));

            _mockComplaints.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            _mockComplaints.Verify(r => r.CommitTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task GetCountsAsync_ReturnsCorrectCounts()
        {
            var counts = new Dictionary<ComplaintStatus, int>
            {
                { ComplaintStatus.Pending, 3 },
                { ComplaintStatus.Resolved, 5 },
                { ComplaintStatus.Rejected, 2 }
            };

            _mockComplaints.Setup(r => r.CountByStatusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(counts);

            var result = await _service.GetCountsAsync();

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal(3, result.Data.Pending);
            Assert.Equal(5, result.Data.Resolved);
            Assert.Equal(2, result.Data.Rejected);
            Assert.Equal(10, result.Data.Total);
        }
    }
}

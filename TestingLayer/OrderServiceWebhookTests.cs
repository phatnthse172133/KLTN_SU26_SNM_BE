using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ApplicationLayer.Services.Orders;
using ApplicationLayer.Services.PayOS;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Promotions;
using ApplicationLayer.DTOs.Responses;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PayOS.Models.Webhooks;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer
{
    public class OrderServiceWebhookTests
    {
        private readonly Mock<IOrderRepository> _mockOrderRepo;
        private readonly Mock<IPromotionRepository> _mockPromotionRepo;
        private readonly Mock<IPromotionValidationService> _mockPromotionValidation;
        private readonly Mock<IPayOSService> _mockPayOS;
        private readonly Mock<IRealtimeNotificationPublisher> _mockNotificationPublisher;
        private readonly Mock<IFoodItemRepository> _mockFoodItemRepo;
        private readonly Mock<ILogger<OrderService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<IPayOSOrderCodeGenerator> _mockOrderCodeGenerator;
        private readonly Mock<IBoothRepository> _mockBoothRepo;
        private readonly OrderService _service;

        private readonly long _orderCode = 123456789012345L;
        private readonly Guid _orderId = Guid.NewGuid();
        private readonly Guid _boothOwnerId = Guid.NewGuid();
        private readonly Guid _customerId = Guid.NewGuid();
        private readonly decimal _amount = 100000;

        public OrderServiceWebhookTests()
        {
            _mockOrderRepo = new Mock<IOrderRepository>();
            _mockPromotionRepo = new Mock<IPromotionRepository>();
            _mockPromotionValidation = new Mock<IPromotionValidationService>();
            _mockPayOS = new Mock<IPayOSService>();
            _mockNotificationPublisher = new Mock<IRealtimeNotificationPublisher>();
            _mockFoodItemRepo = new Mock<IFoodItemRepository>();
            _mockLogger = new Mock<ILogger<OrderService>>();
            _mockConfig = new Mock<IConfiguration>();
            _mockOrderCodeGenerator = new Mock<IPayOSOrderCodeGenerator>();
            _mockBoothRepo = new Mock<IBoothRepository>();
            _mockBoothRepo.Setup(b => b.GetByOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Booth { Id = Guid.NewGuid(), BoothOwnerId = _boothOwnerId, NightMarket = new NightMarket { IsDeleted = false } });
            _service = new OrderService(
                _mockOrderRepo.Object, _mockPromotionRepo.Object, _mockPromotionValidation.Object,
                _mockPayOS.Object, _mockNotificationPublisher.Object,
                _mockFoodItemRepo.Object, _mockLogger.Object, _mockConfig.Object, _mockOrderCodeGenerator.Object, _mockBoothRepo.Object);
        }

        private PayOSWebhookData CreateWebhookData(string code = "00", decimal? amount = null) => new PayOSWebhookData
        {
            OrderCode = _orderCode,
            Amount = (int)(amount ?? _amount),
            Code = code,
            IsSuccessful = code == "00",
            TransactionDateTime = DateTime.UtcNow.ToString(),
            PaymentLinkId = "link-123",
            Reference = "ref-123",
        };

        private Order CreateOrder(OrderStatus status = OrderStatus.Placed, decimal? finalAmount = null) => new Order
        {
            Id = _orderId,
            OrderCode = _orderCode,
            CustomerId = _customerId,
            BoothOwnerId = _boothOwnerId,
            Status = status,
            FinalAmount = finalAmount ?? _amount,
        };

        [Fact]
        public async Task Webhook_NullData_ReturnsInvalidSignature()
        {
            var result = await _service.ProcessPaymentWebhookAsync(null!);

            Assert.Equal(WebhookDispatchResult.InvalidSignature, result);
        }

        [Fact]
        public async Task Webhook_NonSuccessCode_ReturnsNotSuccessful()
        {
            var data = CreateWebhookData("99");

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.NotSuccessful, result);
            _mockOrderRepo.Verify(r => r.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task Webhook_OrderNotFound_ReturnsNotFound()
        {
            var data = CreateWebhookData();
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync((Order?)null);

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.NotFound, result);
        }

        [Fact]
        public async Task Webhook_AlreadyProcessed_ReturnsAlreadyProcessed()
        {
            var data = CreateWebhookData();
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Preparing));

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.AlreadyProcessed, result);
            _mockOrderRepo.Verify(r => r.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task Webhook_ExactPayment_CommitsAndPublishesNotification()
        {
            var data = CreateWebhookData();
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Placed));
            _mockOrderRepo.Setup(r => r.UpdateOrderStatusIfPlacedAsync(_orderCode, OrderStatus.Preparing, It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockOrderRepo.Setup(r => r.UpdatePaymentToPaidAsync(_orderCode, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(1);

            var callOrder = new System.Collections.Generic.List<string>();
            _mockOrderRepo.Setup(r => r.CommitTransactionAsync())
                .Callback(() => callOrder.Add("commit"))
                .Returns(Task.CompletedTask);
            _mockNotificationPublisher.Setup(n => n.PublishAsync(It.IsAny<Guid>(), It.IsAny<NotificationListItemResponse>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, NotificationListItemResponse, int, CancellationToken>((_, _, _, _) => callOrder.Add("notify"))
                .Returns(Task.CompletedTask);

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.OrderHandled, result);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
            _mockNotificationPublisher.Verify(n => n.PublishAsync(_boothOwnerId, It.IsAny<NotificationListItemResponse>(), 1, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(new[] { "commit", "notify" }, callOrder);
        }

        [Fact]
        public async Task Webhook_Overpaid_ProceedsAndLogsWarning()
        {
            var data = CreateWebhookData(amount: _amount + 5000);
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Placed));
            _mockOrderRepo.Setup(r => r.UpdateOrderStatusIfPlacedAsync(_orderCode, OrderStatus.Preparing, It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockOrderRepo.Setup(r => r.UpdatePaymentToPaidAsync(_orderCode, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(1);

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.OrderHandled, result);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task Webhook_Underpaid_UpdatesToUnderpaidAndMarksPaymentAndPublishesNotification()
        {
            var data = CreateWebhookData(amount: _amount - 10000);
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Placed));
            _mockOrderRepo.Setup(r => r.UpdateOrderToUnderpaidAsync(_orderCode, It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockOrderRepo.Setup(r => r.UpdatePaymentToPaidWithAmountAsync(_orderCode, _amount - 10000, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(1);

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.OrderHandled, result);
            _mockOrderRepo.Verify(r => r.UpdateOrderToUnderpaidAsync(_orderCode, It.IsAny<DateTime>()), Times.Once);
            _mockOrderRepo.Verify(r => r.UpdatePaymentToPaidWithAmountAsync(_orderCode, _amount - 10000, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
            _mockNotificationPublisher.Verify(n => n.PublishAsync(_boothOwnerId, It.IsAny<NotificationListItemResponse>(), 1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Webhook_PaymentRowsZero_RollsBackAndReturnsNotFound()
        {
            var data = CreateWebhookData();
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Placed));
            _mockOrderRepo.Setup(r => r.UpdateOrderStatusIfPlacedAsync(_orderCode, OrderStatus.Preparing, It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockOrderRepo.Setup(r => r.UpdatePaymentToPaidAsync(_orderCode, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(0);

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.NotFound, result);
            _mockOrderRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Never);
            _mockNotificationPublisher.Verify(n => n.PublishAsync(It.IsAny<Guid>(), It.IsAny<NotificationListItemResponse>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Webhook_OrderRowsZero_RollsBackAndReturnsAlreadyProcessed()
        {
            var data = CreateWebhookData();
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Placed));
            _mockOrderRepo.Setup(r => r.UpdateOrderStatusIfPlacedAsync(_orderCode, OrderStatus.Preparing, It.IsAny<DateTime>()))
                .ReturnsAsync(0);

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.AlreadyProcessed, result);
            _mockOrderRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            _mockOrderRepo.Verify(r => r.UpdatePaymentToPaidAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task Webhook_UnderpaidRowsZero_RollsBackAndReturnsAlreadyProcessed()
        {
            var data = CreateWebhookData(amount: _amount - 10000);
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Placed));
            _mockOrderRepo.Setup(r => r.UpdateOrderToUnderpaidAsync(_orderCode, It.IsAny<DateTime>()))
                .ReturnsAsync(0);

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.AlreadyProcessed, result);
            _mockOrderRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Never);
        }

        // ─── Supplemental payment for Underpaid orders ────────────────

        [Fact]
        public async Task Webhook_SupplementalPayment_FullyCovers_TransitionsToPreparing()
        {
            var data = CreateWebhookData(amount: 10000);
            var order = CreateOrder(OrderStatus.Underpaid, finalAmount: 100000);
            var payment = new Payment { Id = Guid.NewGuid(), OrderId = order.Id, Order = order, PayOSOrderCode = _orderCode };
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync(payment);
            _mockOrderRepo.Setup(r => r.UpdatePaymentToPaidWithAmountAsync(_orderCode, 10000, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockOrderRepo.Setup(r => r.GetTotalPaidAmountAsync(_orderCode))
                .ReturnsAsync(100000); // total now covers
            _mockOrderRepo.Setup(r => r.UpdateOrderFromUnderpaidToPreparingAsync(_orderCode, It.IsAny<DateTime>()))
                .ReturnsAsync(1);

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.OrderHandled, result);
            _mockOrderRepo.Verify(r => r.UpdatePaymentToPaidWithAmountAsync(_orderCode, 10000, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            _mockOrderRepo.Verify(r => r.UpdateOrderFromUnderpaidToPreparingAsync(_orderCode, It.IsAny<DateTime>()), Times.Once);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
            _mockNotificationPublisher.Verify(n => n.PublishAsync(_boothOwnerId, It.IsAny<NotificationListItemResponse>(), 1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Webhook_SupplementalPayment_StillUnderpaid_StaysUnderpaid()
        {
            var data = CreateWebhookData(amount: 10000);
            var order = CreateOrder(OrderStatus.Underpaid, finalAmount: 100000);
            var payment = new Payment { Id = Guid.NewGuid(), OrderId = order.Id, Order = order, PayOSOrderCode = _orderCode };
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync(payment);
            _mockOrderRepo.Setup(r => r.UpdatePaymentToPaidWithAmountAsync(_orderCode, 10000, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockOrderRepo.Setup(r => r.GetTotalPaidAmountAsync(_orderCode))
                .ReturnsAsync(50000); // still not enough

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.OrderHandled, result);
            _mockOrderRepo.Verify(r => r.UpdatePaymentToPaidWithAmountAsync(_orderCode, 10000, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            _mockOrderRepo.Verify(r => r.UpdateOrderFromUnderpaidToPreparingAsync(It.IsAny<long>(), It.IsAny<DateTime>()), Times.Never);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
            _mockNotificationPublisher.Verify(n => n.PublishAsync(_boothOwnerId, It.IsAny<NotificationListItemResponse>(), 1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Webhook_SupplementalPayment_NoPendingPayment_ReturnsAlreadyProcessed()
        {
            var data = CreateWebhookData(amount: 10000);
            var order = CreateOrder(OrderStatus.Underpaid, finalAmount: 100000);
            var payment = new Payment { Id = Guid.NewGuid(), OrderId = order.Id, Order = order, PayOSOrderCode = _orderCode };
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync(payment);
            _mockOrderRepo.Setup(r => r.UpdatePaymentToPaidWithAmountAsync(_orderCode, 10000, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(0); // no pending payment found

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.AlreadyProcessed, result);
            _mockOrderRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Never);
        }

        // ─── Notification isolation (P1-1) ───────────────────────────

        [Fact]
        public async Task Webhook_NotificationException_DoesNotFailWebhook()
        {
            var data = CreateWebhookData();
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Placed));
            _mockOrderRepo.Setup(r => r.UpdateOrderStatusIfPlacedAsync(_orderCode, OrderStatus.Preparing, It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockOrderRepo.Setup(r => r.UpdatePaymentToPaidAsync(_orderCode, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockNotificationPublisher.Setup(n => n.PublishAsync(It.IsAny<Guid>(), It.IsAny<NotificationListItemResponse>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("SignalR hub unavailable"));

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.OrderHandled, result);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task Webhook_UnderpaidNotificationException_DoesNotFailWebhook()
        {
            var data = CreateWebhookData(amount: _amount - 10000);
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Placed));
            _mockOrderRepo.Setup(r => r.UpdateOrderToUnderpaidAsync(_orderCode, It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockOrderRepo.Setup(r => r.UpdatePaymentToPaidWithAmountAsync(_orderCode, _amount - 10000, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockNotificationPublisher.Setup(n => n.PublishAsync(It.IsAny<Guid>(), It.IsAny<NotificationListItemResponse>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("SignalR hub unavailable"));

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.OrderHandled, result);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task Webhook_ExceptionInTransaction_RollsBackAndRethrows()
        {
            var data = CreateWebhookData();
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Placed));
            _mockOrderRepo.Setup(r => r.UpdateOrderStatusIfPlacedAsync(_orderCode, OrderStatus.Preparing, It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("DB connection lost"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ProcessPaymentWebhookAsync(data));

            _mockOrderRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task Webhook_ExceptionInUnderpaidTransaction_RollsBackAndRethrows()
        {
            var data = CreateWebhookData(amount: _amount - 10000);
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Placed));
            _mockOrderRepo.Setup(r => r.UpdateOrderToUnderpaidAsync(_orderCode, It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("DB connection lost"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ProcessPaymentWebhookAsync(data));

            _mockOrderRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task Webhook_CommitBeforeNotification_NotificationAfterCommit()
        {
            var data = CreateWebhookData();
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Placed));
            _mockOrderRepo.Setup(r => r.UpdateOrderStatusIfPlacedAsync(_orderCode, OrderStatus.Preparing, It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockOrderRepo.Setup(r => r.UpdatePaymentToPaidAsync(_orderCode, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(1);

            var callOrder = new System.Collections.Generic.List<string>();
            _mockOrderRepo.Setup(r => r.BeginTransactionAsync())
                .Callback(() => callOrder.Add("begin"))
                .Returns(Task.CompletedTask);
            _mockOrderRepo.Setup(r => r.CommitTransactionAsync())
                .Callback(() => callOrder.Add("commit"))
                .Returns(Task.CompletedTask);
            _mockNotificationPublisher.Setup(n => n.PublishAsync(It.IsAny<Guid>(), It.IsAny<NotificationListItemResponse>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, NotificationListItemResponse, int, CancellationToken>((_, _, _, _) => callOrder.Add("notify"))
                .Returns(Task.CompletedTask);

            await _service.ProcessPaymentWebhookAsync(data);

            Assert.Contains("commit", callOrder);
            Assert.Contains("notify", callOrder);
            Assert.True(callOrder.IndexOf("commit") < callOrder.IndexOf("notify"));
        }

        [Fact]
        public async Task HasOrderWithCodeAsync_OrderExists_ReturnsTrue()
        {
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder());

            var result = await _service.HasOrderWithCodeAsync(_orderCode);

            Assert.True(result);
        }

        [Fact]
        public async Task HasOrderWithCodeAsync_OrderNotFound_ReturnsFalse()
        {
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync((Order?)null);

            var result = await _service.HasOrderWithCodeAsync(_orderCode);

            Assert.False(result);
        }

        [Fact]
        public async Task Webhook_UnderpaidFirstPayment_PaymentRowsZero_RollsBackAndReturnsNotFound()
        {
            var data = CreateWebhookData(amount: _amount - 10000);
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(_orderCode))
                .ReturnsAsync((Payment?)null);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Placed));
            _mockOrderRepo.Setup(r => r.UpdateOrderToUnderpaidAsync(_orderCode, It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockOrderRepo.Setup(r => r.UpdatePaymentToPaidWithAmountAsync(_orderCode, _amount - 10000, "link-123", "ref-123", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(0);

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.NotFound, result);
            _mockOrderRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task Webhook_SupplementalPayment_FoundByPayOSOrderCode_UsesOrderFromPayment()
        {
            var supplementalCode = 999888777666555L;
            var data = new PayOSWebhookData
            {
                OrderCode = supplementalCode,
                Amount = 10000,
                Code = "00",
                IsSuccessful = true,
                PaymentLinkId = "link-supplemental",
                Reference = "ref-supplemental",
            };
            var order = CreateOrder(OrderStatus.Underpaid, finalAmount: 100000);
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Order = order,
                PayOSOrderCode = supplementalCode,
                Status = PaymentStatus.Pending
            };
            _mockOrderRepo.Setup(r => r.GetPaymentByPayOSOrderCodeAsync(supplementalCode))
                .ReturnsAsync(payment);
            _mockOrderRepo.Setup(r => r.UpdatePaymentToPaidWithAmountAsync(order.OrderCode, 10000, "link-supplemental", "ref-supplemental", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(1);
            _mockOrderRepo.Setup(r => r.GetTotalPaidAmountAsync(order.OrderCode))
                .ReturnsAsync(100000);
            _mockOrderRepo.Setup(r => r.UpdateOrderFromUnderpaidToPreparingAsync(order.OrderCode, It.IsAny<DateTime>()))
                .ReturnsAsync(1);

            var result = await _service.ProcessPaymentWebhookAsync(data);

            Assert.Equal(WebhookDispatchResult.OrderHandled, result);
            _mockOrderRepo.Verify(r => r.GetOrderByCodeAsync(It.IsAny<long>()), Times.Never);
            _mockOrderRepo.Verify(r => r.UpdatePaymentToPaidWithAmountAsync(order.OrderCode, 10000, "link-supplemental", "ref-supplemental", It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            _mockOrderRepo.Verify(r => r.UpdateOrderFromUnderpaidToPreparingAsync(order.OrderCode, It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task PayRemainingAmount_OrderNotFound_ReturnsFailure()
        {
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync((Order?)null);

            var result = await _service.PayRemainingAmountAsync(_customerId, _orderCode);

            Assert.False(result.Success);
            Assert.Equal("ORDER_NOT_FOUND", result.ErrorCode);
        }

        [Fact]
        public async Task PayRemainingAmount_AccessDenied_WrongCustomer_ReturnsFailure()
        {
            var otherUser = Guid.NewGuid();
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Underpaid, finalAmount: 100000));

            var result = await _service.PayRemainingAmountAsync(otherUser, _orderCode);

            Assert.False(result.Success);
            Assert.Equal("ORDER_ACCESS_DENIED", result.ErrorCode);
            _mockOrderRepo.Verify(r => r.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task PayRemainingAmount_BoothOwner_CanAccess()
        {
            var order = CreateOrder(OrderStatus.Underpaid, finalAmount: 100000);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(order);
            _mockOrderRepo.Setup(r => r.GetTotalPaidAmountAsync(_orderCode))
                .ReturnsAsync(60000);
            _mockOrderRepo.Setup(r => r.GetPendingPayOSPaymentByOrderIdAsync(_orderId))
                .ReturnsAsync((Payment?)null);
            _mockOrderCodeGenerator.Setup(g => g.GenerateAsync(PayOSOrderSource.Order))
                .ReturnsAsync(999888777666555L);
            _mockPayOS.Setup(p => p.CreatePaymentLinkAsync(It.IsAny<PayOSPaymentRequest>()))
                .ReturnsAsync(new PayOSPaymentResponse
                {
                    OrderCode = 999888777666555L,
                    PaymentLinkId = "link-supplemental",
                    CheckoutUrl = "https://payos.vn/checkout/supplemental",
                    Amount = 40000
                });

            var result = await _service.PayRemainingAmountAsync(_boothOwnerId, _orderCode);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task PayRemainingAmount_OrderNotUnderpaid_ReturnsFailure()
        {
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Placed));

            var result = await _service.PayRemainingAmountAsync(_customerId, _orderCode);

            Assert.False(result.Success);
            Assert.Equal("ORDER_NOT_UNDERPAID", result.ErrorCode);
        }

        [Fact]
        public async Task PayRemainingAmount_AlreadyFullyPaid_ReturnsFailure()
        {
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(CreateOrder(OrderStatus.Underpaid, finalAmount: 100000));
            _mockOrderRepo.Setup(r => r.GetTotalPaidAmountAsync(_orderCode))
                .ReturnsAsync(100000);
            _mockOrderRepo.Setup(r => r.GetPendingPayOSPaymentByOrderIdAsync(_orderId))
                .ReturnsAsync((Payment?)null);

            var result = await _service.PayRemainingAmountAsync(_customerId, _orderCode);

            Assert.False(result.Success);
            Assert.Equal("ORDER_ALREADY_FULLY_PAID", result.ErrorCode);
        }

        [Fact]
        public async Task PayRemainingAmount_ExistingPendingLink_ReturnsExistingLink()
        {
            var order = CreateOrder(OrderStatus.Underpaid, finalAmount: 100000);
            var existingPayment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = _orderId,
                Amount = 40000,
                Status = PaymentStatus.Pending,
                Gateway = PaymentGateway.Payos,
                CheckoutUrl = "https://payos.vn/checkout/existing",
                PayOSOrderCode = 888777666555444L
            };
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(order);
            _mockOrderRepo.Setup(r => r.GetTotalPaidAmountAsync(_orderCode))
                .ReturnsAsync(60000);
            _mockOrderRepo.Setup(r => r.GetPendingPayOSPaymentByOrderIdAsync(_orderId))
                .ReturnsAsync(existingPayment);

            var result = await _service.PayRemainingAmountAsync(_customerId, _orderCode);

            Assert.True(result.Success);
            Assert.Equal("https://payos.vn/checkout/existing", result.Data!.PaymentUrl);
            Assert.Equal(888777666555444L, result.Data.PayOSOrderCode);
            _mockOrderRepo.Verify(r => r.AcquireSupplementalPaymentLockAsync(_orderId), Times.Once);
            _mockPayOS.Verify(p => p.CreatePaymentLinkAsync(It.IsAny<PayOSPaymentRequest>()), Times.Never);
            _mockOrderRepo.Verify(r => r.AddPaymentAsync(It.IsAny<Payment>()), Times.Never);
        }

        [Fact]
        public async Task PayRemainingAmount_ValidUnderpaid_CreatesPaymentAndLink()
        {
            var order = CreateOrder(OrderStatus.Underpaid, finalAmount: 100000);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(order);
            _mockOrderRepo.Setup(r => r.GetTotalPaidAmountAsync(_orderCode))
                .ReturnsAsync(60000);
            _mockOrderRepo.Setup(r => r.GetPendingPayOSPaymentByOrderIdAsync(_orderId))
                .ReturnsAsync((Payment?)null);
            _mockOrderCodeGenerator.Setup(g => g.GenerateAsync(PayOSOrderSource.Order))
                .ReturnsAsync(999888777666555L);
            _mockPayOS.Setup(p => p.CreatePaymentLinkAsync(It.IsAny<PayOSPaymentRequest>()))
                .ReturnsAsync(new PayOSPaymentResponse
                {
                    OrderCode = 999888777666555L,
                    PaymentLinkId = "link-supplemental",
                    CheckoutUrl = "https://payos.vn/checkout/supplemental",
                    Amount = 40000
                });

            var result = await _service.PayRemainingAmountAsync(_customerId, _orderCode);

            Assert.True(result.Success);
            Assert.Equal(40000, result.Data!.RemainingAmount);
            Assert.Equal(60000, result.Data.TotalPaid);
            Assert.Equal(100000, result.Data.FinalAmount);
            Assert.Equal("https://payos.vn/checkout/supplemental", result.Data.PaymentUrl);
            Assert.Equal(999888777666555L, result.Data.PayOSOrderCode);
            _mockOrderRepo.Verify(r => r.AcquireSupplementalPaymentLockAsync(_orderId), Times.Once);
            _mockOrderRepo.Verify(r => r.AddPaymentAsync(It.Is<Payment>(p => p.PayOSOrderCode == 999888777666555L && p.Amount == 40000 && p.Status == PaymentStatus.Pending)), Times.Once);
            _mockOrderRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task PayRemainingAmount_PayOSFails_ReturnsStableErrorNoExceptionDetails()
        {
            var order = CreateOrder(OrderStatus.Underpaid, finalAmount: 100000);
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode))
                .ReturnsAsync(order);
            _mockOrderRepo.Setup(r => r.GetTotalPaidAmountAsync(_orderCode))
                .ReturnsAsync(60000);
            _mockOrderRepo.Setup(r => r.GetPendingPayOSPaymentByOrderIdAsync(_orderId))
                .ReturnsAsync((Payment?)null);
            _mockOrderCodeGenerator.Setup(g => g.GenerateAsync(PayOSOrderSource.Order))
                .ReturnsAsync(999888777666555L);
            _mockPayOS.Setup(p => p.CreatePaymentLinkAsync(It.IsAny<PayOSPaymentRequest>()))
                .ThrowsAsync(new InvalidOperationException("PayOS API key invalid: sk_test_abc123"));

            var result = await _service.PayRemainingAmountAsync(_customerId, _orderCode);

            Assert.False(result.Success);
            Assert.Equal("SUPPLEMENTAL_PAYMENT_LINK_FAILED", result.ErrorCode);
            Assert.DoesNotContain("PayOS API key", result.Message);
            Assert.DoesNotContain("sk_test_abc123", result.Message);
            _mockOrderRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            _mockOrderRepo.Verify(r => r.AddPaymentAsync(It.IsAny<Payment>()), Times.Never);
        }

        [Fact]
        public async Task PayRemainingAmount_PendingPaymentWithoutUrl_RepairsExistingPayment()
        {
            var order = CreateOrder(OrderStatus.Underpaid, finalAmount: 100000);
            var existingPayment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = _orderId,
                BoothOwnerId = _boothOwnerId,
                Amount = 40000,
                Status = PaymentStatus.Pending,
                Gateway = PaymentGateway.Payos,
                CheckoutUrl = null,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode)).ReturnsAsync(order);
            _mockOrderRepo.Setup(r => r.GetTotalPaidAmountAsync(_orderCode)).ReturnsAsync(60000);
            _mockOrderRepo.Setup(r => r.GetPendingPayOSPaymentByOrderIdAsync(_orderId))
                .ReturnsAsync(existingPayment);
            _mockOrderCodeGenerator.Setup(g => g.GenerateAsync(PayOSOrderSource.Order))
                .ReturnsAsync(999888777666555L);
            _mockPayOS.Setup(p => p.CreatePaymentLinkAsync(It.IsAny<PayOSPaymentRequest>()))
                .ReturnsAsync(new PayOSPaymentResponse
                {
                    OrderCode = 999888777666555L,
                    PaymentLinkId = "link-repaired",
                    CheckoutUrl = "https://payos.vn/checkout/repaired",
                    Amount = 40000
                });

            var result = await _service.PayRemainingAmountAsync(_customerId, _orderCode);

            Assert.True(result.Success);
            Assert.Equal("https://payos.vn/checkout/repaired", existingPayment.CheckoutUrl);
            Assert.Equal(999888777666555L, existingPayment.PayOSOrderCode);
            _mockOrderRepo.Verify(r => r.AddPaymentAsync(It.IsAny<Payment>()), Times.Never);
            _mockOrderRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task PayRemainingAmount_SaveFails_CancelsCreatedPayOSLink()
        {
            var order = CreateOrder(OrderStatus.Underpaid, finalAmount: 100000);
            const long supplementalOrderCode = 999888777666555L;
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode)).ReturnsAsync(order);
            _mockOrderRepo.Setup(r => r.GetTotalPaidAmountAsync(_orderCode)).ReturnsAsync(60000);
            _mockOrderRepo.Setup(r => r.GetPendingPayOSPaymentByOrderIdAsync(_orderId))
                .ReturnsAsync((Payment?)null);
            _mockOrderCodeGenerator.Setup(g => g.GenerateAsync(PayOSOrderSource.Order))
                .ReturnsAsync(supplementalOrderCode);
            _mockPayOS.Setup(p => p.CreatePaymentLinkAsync(It.IsAny<PayOSPaymentRequest>()))
                .ReturnsAsync(new PayOSPaymentResponse
                {
                    OrderCode = supplementalOrderCode,
                    PaymentLinkId = "link-orphan",
                    CheckoutUrl = "https://payos.vn/checkout/orphan",
                    Amount = 40000
                });
            _mockOrderRepo.Setup(r => r.SaveChangesAsync())
                .ThrowsAsync(new InvalidOperationException("database unavailable"));

            var result = await _service.PayRemainingAmountAsync(_customerId, _orderCode);

            Assert.False(result.Success);
            Assert.Equal("SUPPLEMENTAL_PAYMENT_LINK_FAILED", result.ErrorCode);
            _mockOrderRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            _mockPayOS.Verify(p => p.CancelPaymentLinkAsync(supplementalOrderCode), Times.Once);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task PayRemainingAmount_SaveAndCancelBothFail_ReturnsStableError()
        {
            var order = CreateOrder(OrderStatus.Underpaid, finalAmount: 100000);
            const long supplementalOrderCode = 999888777666555L;
            _mockOrderRepo.Setup(r => r.GetOrderByCodeAsync(_orderCode)).ReturnsAsync(order);
            _mockOrderRepo.Setup(r => r.GetTotalPaidAmountAsync(_orderCode)).ReturnsAsync(60000);
            _mockOrderRepo.Setup(r => r.GetPendingPayOSPaymentByOrderIdAsync(_orderId))
                .ReturnsAsync((Payment?)null);
            _mockOrderCodeGenerator.Setup(g => g.GenerateAsync(PayOSOrderSource.Order))
                .ReturnsAsync(supplementalOrderCode);
            _mockPayOS.Setup(p => p.CreatePaymentLinkAsync(It.IsAny<PayOSPaymentRequest>()))
                .ReturnsAsync(new PayOSPaymentResponse
                {
                    OrderCode = supplementalOrderCode,
                    PaymentLinkId = "link-orphan",
                    CheckoutUrl = "https://payos.vn/checkout/orphan",
                    Amount = 40000
                });
            _mockOrderRepo.Setup(r => r.SaveChangesAsync())
                .ThrowsAsync(new InvalidOperationException("database unavailable"));
            _mockPayOS.Setup(p => p.CancelPaymentLinkAsync(supplementalOrderCode))
                .ThrowsAsync(new HttpRequestException("PayOS API down"));

            var result = await _service.PayRemainingAmountAsync(_customerId, _orderCode);

            Assert.False(result.Success);
            Assert.Equal("SUPPLEMENTAL_PAYMENT_LINK_FAILED", result.ErrorCode);
            Assert.DoesNotContain("database unavailable", result.Message);
            Assert.DoesNotContain("PayOS API down", result.Message);
            _mockOrderRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            _mockPayOS.Verify(p => p.CancelPaymentLinkAsync(supplementalOrderCode), Times.Once);
            _mockOrderRepo.Verify(r => r.CommitTransactionAsync(), Times.Never);
        }
    }
}

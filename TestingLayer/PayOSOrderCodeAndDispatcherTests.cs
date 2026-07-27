using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ApplicationLayer.Services.PayOS;
using ApplicationLayer.Services.Subscriptions;
using ApplicationLayer.Services.Orders;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using PayOS.Models.Webhooks;

namespace TestingLayer
{
    public class PayOSOrderCodeGeneratorTests
    {
        private readonly Mock<ISequenceRepository> _mockSeqRepo;
        private readonly IPayOSOrderCodeGenerator _generator;

        public PayOSOrderCodeGeneratorTests()
        {
            _mockSeqRepo = new Mock<ISequenceRepository>();
            var counter = 0L;
            _mockSeqRepo.Setup(r => r.NextPayOSOrderCodeAsync())
                .ReturnsAsync(() => Interlocked.Increment(ref counter));
            _generator = new PayOSOrderCodeGenerator(_mockSeqRepo.Object);
        }

        [Fact]
        public async Task Generate_Order_PrefixIs1()
        {
            var code = await _generator.GenerateAsync(PayOSOrderSource.Order);
            var prefix = code / 100_000_000_000_000L;
            Assert.Equal(1, prefix);
        }

        [Fact]
        public async Task Generate_BoothSubscription_PrefixIs2()
        {
            var code = await _generator.GenerateAsync(PayOSOrderSource.BoothSubscription);
            var prefix = code / 100_000_000_000_000L;
            Assert.Equal(2, prefix);
        }

        [Fact]
        public async Task Generate_MarketSubscription_PrefixIs3()
        {
            var code = await _generator.GenerateAsync(PayOSOrderSource.MarketSubscription);
            var prefix = code / 100_000_000_000_000L;
            Assert.Equal(3, prefix);
        }

        [Fact]
        public async Task GetSource_ReturnsCorrectSource()
        {
            var orderCode = await _generator.GenerateAsync(PayOSOrderSource.Order);
            var boothCode = await _generator.GenerateAsync(PayOSOrderSource.BoothSubscription);
            var marketCode = await _generator.GenerateAsync(PayOSOrderSource.MarketSubscription);

            Assert.Equal(PayOSOrderSource.Order, _generator.GetSource(orderCode));
            Assert.Equal(PayOSOrderSource.BoothSubscription, _generator.GetSource(boothCode));
            Assert.Equal(PayOSOrderSource.MarketSubscription, _generator.GetSource(marketCode));
        }

        [Fact]
        public void GetSource_UnknownPrefix_ReturnsNull()
        {
            var unknownCode = 999_000_000_000_000L;
            Assert.Null(_generator.GetSource(unknownCode));
        }

        [Fact]
        public async Task Generate_Concurrent_NoCollisions()
        {
            var codes = new HashSet<long>();
            var tasks = Enumerable.Range(0, 1000)
                .Select(_ => _generator.GenerateAsync(PayOSOrderSource.Order))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            foreach (var code in results)
            {
                codes.Add(code);
            }

            Assert.Equal(1000, codes.Count);
        }

        [Fact]
        public async Task Generate_ThreeSources_NeverOverlap()
        {
            var orderCodes = new HashSet<long>();
            var boothCodes = new HashSet<long>();
            var marketCodes = new HashSet<long>();

            for (int i = 0; i < 100; i++)
            {
                orderCodes.Add(await _generator.GenerateAsync(PayOSOrderSource.Order));
                boothCodes.Add(await _generator.GenerateAsync(PayOSOrderSource.BoothSubscription));
                marketCodes.Add(await _generator.GenerateAsync(PayOSOrderSource.MarketSubscription));
            }

            // No overlap between any two sets
            Assert.Empty(orderCodes.Intersect(boothCodes));
            Assert.Empty(orderCodes.Intersect(marketCodes));
            Assert.Empty(boothCodes.Intersect(marketCodes));
        }

        [Fact]
        public async Task Generate_SequenceIncrements_CodesAreUnique()
        {
            var code1 = await _generator.GenerateAsync(PayOSOrderSource.Order);
            var code2 = await _generator.GenerateAsync(PayOSOrderSource.Order);

            Assert.NotEqual(code1, code2);
            Assert.Equal(1L, code2 - code1); // Sequence increments by 1
        }
    }

    public class PayOSWebhookDispatcherTests
    {
        private readonly Mock<IPayOSService> _mockPayOS;
        private readonly Mock<IPayOSOrderCodeGenerator> _mockGenerator;
        private readonly Mock<IPayOSWebhookService> _mockSubWebhook;
        private readonly Mock<IOrderService> _mockOrderService;
        private readonly Mock<ILogger<PayOSWebhookDispatcher>> _mockLogger;
        private readonly PayOSWebhookDispatcher _dispatcher;

        public PayOSWebhookDispatcherTests()
        {
            _mockPayOS = new Mock<IPayOSService>();
            _mockGenerator = new Mock<IPayOSOrderCodeGenerator>();
            _mockSubWebhook = new Mock<IPayOSWebhookService>();
            _mockOrderService = new Mock<IOrderService>();
            _mockLogger = new Mock<ILogger<PayOSWebhookDispatcher>>();
            _dispatcher = new PayOSWebhookDispatcher(
                _mockPayOS.Object, _mockGenerator.Object,
                _mockSubWebhook.Object, _mockOrderService.Object, _mockLogger.Object);
        }

        private PayOSWebhookData CreateVerifiedData(long orderCode) => new PayOSWebhookData
        {
            OrderCode = orderCode,
            Amount = 100000,
            Code = "00",
            IsSuccessful = true,
            PaymentLinkId = "link-123",
        };

        [Fact]
        public async Task Dispatch_NullBody_ReturnsInvalidSignature()
        {
            var result = await _dispatcher.DispatchAsync(null!);
            Assert.Equal(WebhookDispatchResult.InvalidSignature, result);
        }

        [Fact]
        public async Task Dispatch_InvalidSignature_ReturnsInvalidSignature()
        {
            _mockPayOS.Setup(p => p.VerifyWebhookAsync(It.IsAny<Webhook>()))
                .ReturnsAsync((PayOSWebhookData?)null);

            var result = await _dispatcher.DispatchAsync(new Webhook());

            Assert.Equal(WebhookDispatchResult.InvalidSignature, result);
        }

        [Fact]
        public async Task Dispatch_OrderPrefix_CallsOrderService()
        {
            var orderCode = 123456789012345L;
            _mockPayOS.Setup(p => p.VerifyWebhookAsync(It.IsAny<Webhook>()))
                .ReturnsAsync(CreateVerifiedData(orderCode));
            _mockGenerator.Setup(g => g.GetSource(orderCode))
                .Returns(PayOSOrderSource.Order);
            _mockOrderService.Setup(o => o.ProcessPaymentWebhookAsync(It.IsAny<PayOSWebhookData>()))
                .ReturnsAsync(WebhookDispatchResult.OrderHandled);

            var result = await _dispatcher.DispatchAsync(new Webhook());

            Assert.Equal(WebhookDispatchResult.OrderHandled, result);
            _mockOrderService.Verify(o => o.ProcessPaymentWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Once);
            _mockSubWebhook.Verify(s => s.HandleWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Never);
        }

        [Fact]
        public async Task Dispatch_BoothPrefix_CallsSubscriptionService()
        {
            var orderCode = 223456789012345L;
            _mockPayOS.Setup(p => p.VerifyWebhookAsync(It.IsAny<Webhook>()))
                .ReturnsAsync(CreateVerifiedData(orderCode));
            _mockGenerator.Setup(g => g.GetSource(orderCode))
                .Returns(PayOSOrderSource.BoothSubscription);
            _mockSubWebhook.Setup(s => s.HandleWebhookAsync(It.IsAny<PayOSWebhookData>()))
                .ReturnsAsync(WebhookDispatchResult.SubscriptionHandled);

            var result = await _dispatcher.DispatchAsync(new Webhook());

            Assert.Equal(WebhookDispatchResult.SubscriptionHandled, result);
            _mockSubWebhook.Verify(s => s.HandleWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Once);
            _mockOrderService.Verify(o => o.ProcessPaymentWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Never);
        }

        [Fact]
        public async Task Dispatch_MarketPrefix_CallsSubscriptionService()
        {
            var orderCode = 323456789012345L;
            _mockPayOS.Setup(p => p.VerifyWebhookAsync(It.IsAny<Webhook>()))
                .ReturnsAsync(CreateVerifiedData(orderCode));
            _mockGenerator.Setup(g => g.GetSource(orderCode))
                .Returns(PayOSOrderSource.MarketSubscription);
            _mockSubWebhook.Setup(s => s.HandleWebhookAsync(It.IsAny<PayOSWebhookData>()))
                .ReturnsAsync(WebhookDispatchResult.SubscriptionHandled);

            var result = await _dispatcher.DispatchAsync(new Webhook());

            Assert.Equal(WebhookDispatchResult.SubscriptionHandled, result);
            _mockSubWebhook.Verify(s => s.HandleWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Once);
            _mockOrderService.Verify(o => o.ProcessPaymentWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Never);
        }

        [Fact]
        public async Task Dispatch_UnknownPrefix_NoMatch_ReturnsNotFound()
        {
            var orderCode = 923456789012345L;
            _mockPayOS.Setup(p => p.VerifyWebhookAsync(It.IsAny<Webhook>()))
                .ReturnsAsync(CreateVerifiedData(orderCode));
            _mockGenerator.Setup(g => g.GetSource(orderCode))
                .Returns((PayOSOrderSource?)null);
            _mockOrderService.Setup(o => o.HasOrderWithCodeAsync(orderCode))
                .ReturnsAsync(false);
            _mockSubWebhook.Setup(s => s.HasSubscriptionWithCodeAsync(orderCode))
                .ReturnsAsync(false);

            var result = await _dispatcher.DispatchAsync(new Webhook());

            Assert.Equal(WebhookDispatchResult.NotFound, result);
            _mockOrderService.Verify(o => o.ProcessPaymentWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Never);
            _mockSubWebhook.Verify(s => s.HandleWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Never);
        }

        [Fact]
        public async Task Dispatch_UnknownPrefix_OnlyOrderExists_CallsOrderService()
        {
            var orderCode = 923456789012345L;
            _mockPayOS.Setup(p => p.VerifyWebhookAsync(It.IsAny<Webhook>()))
                .ReturnsAsync(CreateVerifiedData(orderCode));
            _mockGenerator.Setup(g => g.GetSource(orderCode))
                .Returns((PayOSOrderSource?)null);
            _mockOrderService.Setup(o => o.HasOrderWithCodeAsync(orderCode))
                .ReturnsAsync(true);
            _mockSubWebhook.Setup(s => s.HasSubscriptionWithCodeAsync(orderCode))
                .ReturnsAsync(false);
            _mockOrderService.Setup(o => o.ProcessPaymentWebhookAsync(It.IsAny<PayOSWebhookData>()))
                .ReturnsAsync(WebhookDispatchResult.OrderHandled);

            var result = await _dispatcher.DispatchAsync(new Webhook());

            Assert.Equal(WebhookDispatchResult.OrderHandled, result);
            _mockOrderService.Verify(o => o.ProcessPaymentWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Once);
            _mockSubWebhook.Verify(s => s.HandleWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Never);
        }

        [Fact]
        public async Task Dispatch_UnknownPrefix_OnlySubscriptionExists_CallsSubscriptionService()
        {
            var orderCode = 923456789012345L;
            _mockPayOS.Setup(p => p.VerifyWebhookAsync(It.IsAny<Webhook>()))
                .ReturnsAsync(CreateVerifiedData(orderCode));
            _mockGenerator.Setup(g => g.GetSource(orderCode))
                .Returns((PayOSOrderSource?)null);
            _mockOrderService.Setup(o => o.HasOrderWithCodeAsync(orderCode))
                .ReturnsAsync(false);
            _mockSubWebhook.Setup(s => s.HasSubscriptionWithCodeAsync(orderCode))
                .ReturnsAsync(true);
            _mockSubWebhook.Setup(s => s.HandleWebhookAsync(It.IsAny<PayOSWebhookData>()))
                .ReturnsAsync(WebhookDispatchResult.SubscriptionHandled);

            var result = await _dispatcher.DispatchAsync(new Webhook());

            Assert.Equal(WebhookDispatchResult.SubscriptionHandled, result);
            _mockSubWebhook.Verify(s => s.HandleWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Once);
            _mockOrderService.Verify(o => o.ProcessPaymentWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Never);
        }

        [Fact]
        public async Task Dispatch_UnknownPrefix_BothExist_ReturnsConflict()
        {
            var orderCode = 923456789012345L;
            _mockPayOS.Setup(p => p.VerifyWebhookAsync(It.IsAny<Webhook>()))
                .ReturnsAsync(CreateVerifiedData(orderCode));
            _mockGenerator.Setup(g => g.GetSource(orderCode))
                .Returns((PayOSOrderSource?)null);
            _mockOrderService.Setup(o => o.HasOrderWithCodeAsync(orderCode))
                .ReturnsAsync(true);
            _mockSubWebhook.Setup(s => s.HasSubscriptionWithCodeAsync(orderCode))
                .ReturnsAsync(true);

            var result = await _dispatcher.DispatchAsync(new Webhook());

            Assert.Equal(WebhookDispatchResult.Conflict, result);
            _mockOrderService.Verify(o => o.ProcessPaymentWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Never);
            _mockSubWebhook.Verify(s => s.HandleWebhookAsync(It.IsAny<PayOSWebhookData>()), Times.Never);
        }
    }
}

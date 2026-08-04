using Microsoft.Extensions.Logging;
using PayOS.Models.Webhooks;
using System;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.PayOS
{
    public interface IPayOSWebhookDispatcher
    {
        Task<WebhookDispatchResult> DispatchAsync(Webhook webhookBody);
    }

    public class PayOSWebhookDispatcher : IPayOSWebhookDispatcher
    {
        private readonly IPayOSService _payosService;
        private readonly IPayOSOrderCodeGenerator _orderCodeGenerator;
        private readonly Subscriptions.IPayOSWebhookService _subscriptionWebhookService;
        private readonly Orders.IOrderService _orderService;
        private readonly ILogger<PayOSWebhookDispatcher> _logger;
        private readonly IOrderRepository? _orderRepository;

        public PayOSWebhookDispatcher(
            IPayOSService payosService,
            IPayOSOrderCodeGenerator orderCodeGenerator,
            Subscriptions.IPayOSWebhookService subscriptionWebhookService,
            Orders.IOrderService orderService,
            ILogger<PayOSWebhookDispatcher> logger,
            IOrderRepository? orderRepository = null)
        {
            _payosService = payosService;
            _orderCodeGenerator = orderCodeGenerator;
            _subscriptionWebhookService = subscriptionWebhookService;
            _orderService = orderService;
            _logger = logger;
            _orderRepository = orderRepository;
        }

        public async Task<WebhookDispatchResult> DispatchAsync(Webhook webhookBody)
        {
            if (webhookBody == null)
            {
                _logger.LogWarning("Webhook body is null.");
                return WebhookDispatchResult.InvalidSignature;
            }

            // Verify signature once using SDK
            var verifiedData = await _payosService.VerifyWebhookAsync(webhookBody);
            if (verifiedData == null)
            {
                _logger.LogWarning("PayOS webhook signature verification failed.");
                return WebhookDispatchResult.InvalidSignature;
            }

            Guid? auditId = null;
            if (_orderRepository is not null)
            {
                var payload = JsonSerializer.Serialize(webhookBody);
                var signature = "";
                using (var document = JsonDocument.Parse(payload))
                    if (document.RootElement.TryGetProperty("signature", out var value)) signature = value.GetString() ?? "";
                var eventKey = $"{verifiedData.OrderCode}:{verifiedData.Reference ?? verifiedData.PaymentLinkId ?? verifiedData.Code}:{verifiedData.Amount}";
                auditId = await _orderRepository.TryRecordWebhookEventAsync(new PaymentWebhookEvent
                {
                    Id = Guid.NewGuid(),
                    Provider = "PayOS",
                    ProviderEventKey = eventKey,
                    OrderCode = verifiedData.OrderCode,
                    SignatureHash = Hash(signature),
                    PayloadHash = Hash(payload),
                    ProcessingStatus = WebhookProcessingStatus.Received,
                    ReceivedAt = DateTime.UtcNow
                });
                if (!auditId.HasValue) return WebhookDispatchResult.AlreadyProcessed;
            }

            // Dispatch by prefix
            var source = _orderCodeGenerator.GetSource(verifiedData.OrderCode);
            if (source != null)
            {
                _logger.LogInformation("Dispatching webhook for order code {OrderCode}, source={Source}", verifiedData.OrderCode, source);

                var routedResult = source switch
                {
                    PayOSOrderSource.Order => await _orderService.ProcessPaymentWebhookAsync(verifiedData),
                    PayOSOrderSource.BoothSubscription => await _subscriptionWebhookService.HandleWebhookAsync(verifiedData),
                    PayOSOrderSource.MarketSubscription => await _subscriptionWebhookService.HandleWebhookAsync(verifiedData),
                    _ => WebhookDispatchResult.NotFound
                };
                await CompleteAuditAsync(auditId, routedResult);
                return routedResult;
            }

            // Legacy fallback: code has no known prefix (created before prefix system).
            // Query both domains before mutating to avoid misrouting.
            _logger.LogWarning("Webhook order code {OrderCode} has unknown prefix. Attempting legacy fallback.", verifiedData.OrderCode);

            var hasOrder = await _orderService.HasOrderWithCodeAsync(verifiedData.OrderCode);
            var hasSubscription = await _subscriptionWebhookService.HasSubscriptionWithCodeAsync(verifiedData.OrderCode);

            if (hasOrder && hasSubscription)
            {
                _logger.LogError("Legacy fallback conflict: order code {OrderCode} exists in both Order and Subscription domains. Returning Conflict.", verifiedData.OrderCode);
                await CompleteAuditAsync(auditId, WebhookDispatchResult.Conflict);
                return WebhookDispatchResult.Conflict;
            }

            if (hasOrder)
            {
                var result = await _orderService.ProcessPaymentWebhookAsync(verifiedData);
                await CompleteAuditAsync(auditId, result);
                return result;
            }

            if (hasSubscription)
            {
                var result = await _subscriptionWebhookService.HandleWebhookAsync(verifiedData);
                await CompleteAuditAsync(auditId, result);
                return result;
            }

            _logger.LogWarning("Legacy fallback exhausted for order code {OrderCode}. No matching Order or Subscription found.", verifiedData.OrderCode);
            await CompleteAuditAsync(auditId, WebhookDispatchResult.NotFound);
            return WebhookDispatchResult.NotFound;
        }

        private async Task CompleteAuditAsync(Guid? auditId, WebhookDispatchResult result)
        {
            if (auditId.HasValue && _orderRepository is not null)
                await _orderRepository.CompleteWebhookEventAsync(auditId.Value,
                    result is WebhookDispatchResult.OrderHandled or WebhookDispatchResult.SubscriptionHandled or WebhookDispatchResult.AlreadyProcessed
                        ? WebhookProcessingStatus.Processed
                        : WebhookProcessingStatus.Rejected,
                    result is WebhookDispatchResult.OrderHandled or WebhookDispatchResult.SubscriptionHandled or WebhookDispatchResult.AlreadyProcessed ? null : result.ToString(),
                    DateTime.UtcNow);
        }

        private static string Hash(string value)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}

using Microsoft.Extensions.Logging;
using PayOS.Models.Webhooks;
using System;
using System.Threading.Tasks;

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

        public PayOSWebhookDispatcher(
            IPayOSService payosService,
            IPayOSOrderCodeGenerator orderCodeGenerator,
            Subscriptions.IPayOSWebhookService subscriptionWebhookService,
            Orders.IOrderService orderService,
            ILogger<PayOSWebhookDispatcher> logger)
        {
            _payosService = payosService;
            _orderCodeGenerator = orderCodeGenerator;
            _subscriptionWebhookService = subscriptionWebhookService;
            _orderService = orderService;
            _logger = logger;
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

            // Dispatch by prefix
            var source = _orderCodeGenerator.GetSource(verifiedData.OrderCode);
            if (source != null)
            {
                _logger.LogInformation("Dispatching webhook for order code {OrderCode}, source={Source}", verifiedData.OrderCode, source);

                return source switch
                {
                    PayOSOrderSource.Order => await _orderService.ProcessPaymentWebhookAsync(verifiedData),
                    PayOSOrderSource.BoothSubscription => await _subscriptionWebhookService.HandleWebhookAsync(verifiedData),
                    PayOSOrderSource.MarketSubscription => await _subscriptionWebhookService.HandleWebhookAsync(verifiedData),
                    _ => WebhookDispatchResult.NotFound
                };
            }

            // Legacy fallback: code has no known prefix (created before prefix system).
            // Query both domains before mutating to avoid misrouting.
            _logger.LogWarning("Webhook order code {OrderCode} has unknown prefix. Attempting legacy fallback.", verifiedData.OrderCode);

            var hasOrder = await _orderService.HasOrderWithCodeAsync(verifiedData.OrderCode);
            var hasSubscription = await _subscriptionWebhookService.HasSubscriptionWithCodeAsync(verifiedData.OrderCode);

            if (hasOrder && hasSubscription)
            {
                _logger.LogError("Legacy fallback conflict: order code {OrderCode} exists in both Order and Subscription domains. Returning Conflict.", verifiedData.OrderCode);
                return WebhookDispatchResult.Conflict;
            }

            if (hasOrder)
                return await _orderService.ProcessPaymentWebhookAsync(verifiedData);

            if (hasSubscription)
                return await _subscriptionWebhookService.HandleWebhookAsync(verifiedData);

            _logger.LogWarning("Legacy fallback exhausted for order code {OrderCode}. No matching Order or Subscription found.", verifiedData.OrderCode);
            return WebhookDispatchResult.NotFound;
        }
    }
}

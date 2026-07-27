using ApplicationLayer.Configuration;
using ApplicationLayer.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using System;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.PayOS
{
    public class PayOSService : IPayOSService
    {
        private readonly PayOSClient _client;
        private readonly PayOSSettings _settings;
        private readonly ILogger<PayOSService> _logger;

        public PayOSService(
            [Microsoft.Extensions.DependencyInjection.FromKeyedServices("PayIn")] PayOSClient client,
            IOptions<PayOSSettings> settings,
            ILogger<PayOSService> logger)
        {
            _client = client;
            _settings = settings.Value;
            _logger = logger;

            if (string.IsNullOrEmpty(_settings.ClientId) || string.IsNullOrEmpty(_settings.ApiKey) || string.IsNullOrEmpty(_settings.ChecksumKey))
                throw new InvalidOperationException("PayOS is not configured. Set PayOS__ClientId, PayOS__ApiKey, PayOS__ChecksumKey in environment or appsettings.");
        }

        public async Task<PayOSPaymentResponse> CreatePaymentLinkAsync(PayOSPaymentRequest request)
        {
            if (request.Amount <= 0m || request.Amount != decimal.Truncate(request.Amount) || request.Amount > long.MaxValue)
                throw AppException.BadRequest(
                    "PayOS amount must be a positive whole-number VND amount.",
                    "INVALID_PAYOS_AMOUNT");

            var returnUrl = string.IsNullOrEmpty(request.ReturnUrl) ? _settings.ReturnUrl : request.ReturnUrl;
            var cancelUrl = string.IsNullOrEmpty(request.CancelUrl) ? _settings.CancelUrl : request.CancelUrl;
            var amount = decimal.ToInt64(request.Amount);
            // payOS documents a 9-character limit for bank accounts that are not
            // directly linked through payOS. Staying within it works for both modes.
            var description = request.Description.Length > 9 ? request.Description[..9] : request.Description;

            try
            {
                var sdkRequest = new CreatePaymentLinkRequest
                {
                    OrderCode = request.OrderCode,
                    Amount = amount,
                    Description = description,
                    ReturnUrl = returnUrl,
                    CancelUrl = cancelUrl,
                };

                var sdkResponse = await _client.PaymentRequests.CreateAsync(sdkRequest);

                return new PayOSPaymentResponse
                {
                    OrderCode = sdkResponse.OrderCode,
                    PaymentLinkId = sdkResponse.PaymentLinkId ?? "",
                    CheckoutUrl = sdkResponse.CheckoutUrl ?? "",
                    QrCode = sdkResponse.QrCode ?? "",
                    AccountNumber = sdkResponse.AccountNumber ?? "",
                    AccountName = sdkResponse.AccountName ?? "",
                    Amount = sdkResponse.Amount,
                    Description = sdkResponse.Description ?? request.Description,
                    Status = sdkResponse.Status.ToString() ?? "",
                    ExpiresAt = sdkResponse.ExpiredAt.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(sdkResponse.ExpiredAt.Value)
                        : null,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayOS create payment link failed for order {OrderCode}", request.OrderCode);
                throw AppException.BadRequest("Failed to create PayOS payment link.", "PAYOS_CREATE_FAILED");
            }
        }

        public async Task CancelPaymentLinkAsync(long orderCode)
        {
            try
            {
                await _client.PaymentRequests.CancelAsync(orderCode);
                _logger.LogInformation("PayOS payment link for order {OrderCode} cancelled successfully", orderCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cancel PayOS payment link for order {OrderCode}", orderCode);
                throw AppException.BadRequest("Failed to cancel PayOS payment link.", "PAYOS_CANCEL_FAILED");
            }
        }

        public async Task<PayOSWebhookData?> VerifyWebhookAsync(Webhook webhook)
        {
            try
            {
                var verifiedData = await _client.Webhooks.VerifyAsync(webhook);
                if (verifiedData == null)
                {
                    _logger.LogWarning("PayOS webhook verification returned null");
                    return null;
                }

                var code = verifiedData.Code ?? "";
                return new PayOSWebhookData
                {
                    OrderCode = verifiedData.OrderCode,
                    Amount = verifiedData.Amount,
                    Code = code,
                    IsSuccessful = code == "00",
                    TransactionDateTime = verifiedData.TransactionDateTime,
                    PaymentLinkId = verifiedData.PaymentLinkId,
                    Reference = verifiedData.Reference,
                    Description = verifiedData.Description,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayOS webhook signature verification failed");
                return null;
            }
        }

        public async Task<PayOSPaymentStatus?> GetPaymentStatusAsync(long orderCode)
        {
            try
            {
                var paymentLink = await _client.PaymentRequests.GetAsync(orderCode);

                return new PayOSPaymentStatus
                {
                    OrderCode = paymentLink.OrderCode,
                    Status = paymentLink.Status.ToString() ?? "",
                    Amount = paymentLink.Amount,
                    AmountPaid = paymentLink.AmountPaid,
                    AmountRemaining = paymentLink.AmountRemaining,
                    PaymentLinkId = paymentLink.Id,
                    FirstTransactionReference = paymentLink.Transactions?.FirstOrDefault()?.Reference,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayOS get payment status failed for order {OrderCode}", orderCode);
                return null;
            }
        }
    }
}

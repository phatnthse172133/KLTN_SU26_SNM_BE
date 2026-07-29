using ApplicationLayer.Configuration;
using ApplicationLayer.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using System;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.PayOS
{
    public class PayOSService : IPayOSService
    {
        private readonly IServiceProvider _services;
        private readonly PayOSSettings _settings;
        private readonly ILogger<PayOSService> _logger;

        public PayOSService(
            IServiceProvider services,
            IConfiguration configuration,
            ILogger<PayOSService> logger)
        {
            _services = services;
            _settings = configuration.GetSection(PayOSSettings.SectionName).Get<PayOSSettings>() ?? new PayOSSettings();
            _logger = logger;
        }

        public async Task<PayOSPaymentResponse> CreatePaymentLinkAsync(PayOSPaymentRequest request)
        {
            if (request.Amount <= 0m || request.Amount != decimal.Truncate(request.Amount) || request.Amount > long.MaxValue)
                throw AppException.BadRequest(
                    "PayOS amount must be a positive whole-number VND amount.",
                    "INVALID_PAYOS_AMOUNT");

            var returnUrl = string.IsNullOrEmpty(request.ReturnUrl) ? _settings.ReturnUrl : request.ReturnUrl;
            var cancelUrl = string.IsNullOrEmpty(request.CancelUrl) ? _settings.CancelUrl : request.CancelUrl;
            if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out _)
                || !Uri.TryCreate(cancelUrl, UriKind.Absolute, out _))
            {
                throw AppException.ServiceUnavailable(
                    "PayOS return and cancel URLs are not configured correctly.",
                    "PAYOS_URL_INVALID");
            }

            var amount = decimal.ToInt64(request.Amount);
            // payOS documents a 9-character limit for bank accounts that are not
            // directly linked through payOS. Staying within it works for both modes.
            var description = request.Description.Length > 9 ? request.Description[..9] : request.Description;
            var client = GetClient();

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

                var sdkResponse = await client.PaymentRequests.CreateAsync(sdkRequest);

                if (sdkResponse.OrderCode != request.OrderCode
                    || sdkResponse.Amount != amount
                    || string.IsNullOrWhiteSpace(sdkResponse.PaymentLinkId)
                    || !Uri.TryCreate(sdkResponse.CheckoutUrl, UriKind.Absolute, out var checkoutUri)
                    || checkoutUri.Scheme != Uri.UriSchemeHttps)
                {
                    throw AppException.BadGateway(
                        "PayOS returned an invalid payment-link response.",
                        "PAYOS_CREATE_LINK_FAILED");
                }

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
            catch (ApiException ex)
            {
                _logger.LogError(
                    ex,
                    "PayOS create-link failed. OrderCode={OrderCode} Amount={Amount} ProviderStatus={ProviderStatus} ProviderCode={ProviderCode} ProviderMessage={ProviderMessage}",
                    request.OrderCode,
                    amount,
                    ex.StatusCode,
                    ex.ErrorCode,
                    ex.Message);
                throw AppException.BadGateway(
                    "Không thể tạo liên kết thanh toán PayOS. Vui lòng thử lại.",
                    "PAYOS_CREATE_LINK_FAILED",
                    ex);
            }
            catch (PayOSException ex)
            {
                _logger.LogError(
                    ex,
                    "PayOS create-link failed. OrderCode={OrderCode} Amount={Amount} ProviderMessage={ProviderMessage}",
                    request.OrderCode,
                    amount,
                    ex.Message);
                throw AppException.BadGateway(
                    "Không thể tạo liên kết thanh toán PayOS. Vui lòng thử lại.",
                    "PAYOS_CREATE_LINK_FAILED",
                    ex);
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "PayOS create-link failed unexpectedly. OrderCode={OrderCode} Amount={Amount}",
                    request.OrderCode,
                    amount);
                throw AppException.ServiceUnavailable(
                    "Không thể kết nối PayOS. Vui lòng thử lại.",
                    "PAYOS_UNAVAILABLE",
                    ex);
            }
        }

        public async Task CancelPaymentLinkAsync(long orderCode)
        {
            var client = GetClient();
            try
            {
                await client.PaymentRequests.CancelAsync(orderCode);
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
            var client = GetClient();
            try
            {
                var verifiedData = await client.Webhooks.VerifyAsync(webhook);
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
            var client = GetClient();
            try
            {
                var paymentLink = await client.PaymentRequests.GetAsync(orderCode);

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

        private PayOSClient GetClient()
        {
            if (string.IsNullOrWhiteSpace(_settings.ClientId)
                || string.IsNullOrWhiteSpace(_settings.ApiKey)
                || string.IsNullOrWhiteSpace(_settings.ChecksumKey))
            {
                throw AppException.ServiceUnavailable(
                    "PayOS is not configured for this environment.",
                    "PAYOS_NOT_CONFIGURED");
            }

            return _services.GetRequiredKeyedService<PayOSClient>("PayIn");
        }
    }
}

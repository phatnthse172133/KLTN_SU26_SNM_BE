using PayOS.Models.Webhooks;
using System;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.PayOS
{
    public enum WebhookDispatchResult
    {
        SubscriptionHandled,
        OrderHandled,
        AlreadyProcessed,
        NotFound,
        NotSuccessful,
        InvalidSignature,
        Conflict
    }

    public class PayOSPaymentRequest
    {
        public long OrderCode { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }

    public class PayOSPaymentResponse
    {
        public long OrderCode { get; set; }
        public string PaymentLinkId { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset? ExpiresAt { get; set; }
    }

    public class PayOSWebhookData
    {
        public long OrderCode { get; set; }
        public decimal Amount { get; set; }
        public string Code { get; set; } = string.Empty;
        public bool IsSuccessful { get; set; }
        public string? TransactionDateTime { get; set; }
        public string? PaymentLinkId { get; set; }
        public string? Reference { get; set; }
        public string? Description { get; set; }
    }

    public class PayOSPaymentStatus
    {
        public long OrderCode { get; set; }
        public string Status { get; set; } = string.Empty;
        public long Amount { get; set; }
        public long AmountPaid { get; set; }
        public long AmountRemaining { get; set; }
        public string? PaymentLinkId { get; set; }
        public string? FirstTransactionReference { get; set; }
    }

    public interface IPayOSService
    {
        Task<PayOSPaymentResponse> CreatePaymentLinkAsync(PayOSPaymentRequest request);
        Task CancelPaymentLinkAsync(long orderCode);
        Task<PayOSWebhookData?> VerifyWebhookAsync(Webhook webhook);
        Task<PayOSPaymentStatus?> GetPaymentStatusAsync(long orderCode);
    }
}

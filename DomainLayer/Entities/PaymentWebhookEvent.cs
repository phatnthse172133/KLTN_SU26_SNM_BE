using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

public sealed class PaymentWebhookEvent
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "PayOS";
    public string ProviderEventKey { get; set; } = string.Empty;
    public long OrderCode { get; set; }
    public string SignatureHash { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public WebhookProcessingStatus ProcessingStatus { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
}

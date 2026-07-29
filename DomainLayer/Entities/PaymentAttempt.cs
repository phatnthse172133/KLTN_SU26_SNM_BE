using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

public class PaymentAttempt
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public int AttemptNumber { get; set; }
    public long ProviderOrderCode { get; set; }
    public string? ProviderPaymentLinkId { get; set; }
    public PaymentAttemptStatus Status { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? QrCode { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public virtual Payment Payment { get; set; } = null!;
}

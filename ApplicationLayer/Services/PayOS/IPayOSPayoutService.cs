namespace ApplicationLayer.Services.PayOS;

public interface IPayOSPayoutService
{
    Task<PayOSPayoutSnapshot> CreateAsync(PayOSPayoutCommand command, CancellationToken cancellationToken = default);
    Task<PayOSPayoutSnapshot?> FindByReferenceAsync(string referenceId, CancellationToken cancellationToken = default);
    Task<PayOSPayoutSnapshot> GetAsync(string payoutId, CancellationToken cancellationToken = default);
}

public sealed record PayOSPayoutCommand(
    string ReferenceId,
    string IdempotencyKey,
    long Amount,
    string Description,
    string BankBin,
    string AccountNumber);

public sealed record PayOSPayoutSnapshot(
    string PayoutId,
    string ReferenceId,
    long Amount,
    PayOSPayoutOutcome Outcome);

public enum PayOSPayoutOutcome
{
    Processing,
    Succeeded,
    Failed
}

using Microsoft.Extensions.DependencyInjection;
using PayOS;
using PayOS.Models;
using PayOS.Models.V1.Payouts;

namespace ApplicationLayer.Services.PayOS;

public sealed class PayOSPayoutService(
    [FromKeyedServices("PayOut")] PayOSClient client) : IPayOSPayoutService
{
    public async Task<PayOSPayoutSnapshot> CreateAsync(
        PayOSPayoutCommand command,
        CancellationToken cancellationToken = default)
    {
        var payout = await client.Payouts.CreateAsync(
            new PayoutRequest
            {
                ReferenceId = command.ReferenceId,
                Amount = command.Amount,
                Description = command.Description,
                ToBin = command.BankBin,
                ToAccountNumber = command.AccountNumber,
                Category = ["refund"]
            },
            command.IdempotencyKey,
            new RequestOptions<Payout> { CancellationToken = cancellationToken });

        return Map(payout);
    }

    public async Task<PayOSPayoutSnapshot?> FindByReferenceAsync(
        string referenceId,
        CancellationToken cancellationToken = default)
    {
        var page = await client.Payouts.ListAsync(
            new GetPayoutListParam { ReferenceId = referenceId, Limit = 2 },
            new RequestOptions { CancellationToken = cancellationToken });

        var exactMatches = page.Data
            .Where(payout => string.Equals(payout.ReferenceId, referenceId, StringComparison.Ordinal))
            .ToList();
        if (exactMatches.Count > 1)
            throw new InvalidOperationException($"Provider returned duplicate payouts for reference '{referenceId}'.");

        return exactMatches.Count == 0 ? null : Map(exactMatches[0]);
    }

    public async Task<PayOSPayoutSnapshot> GetAsync(
        string payoutId,
        CancellationToken cancellationToken = default)
        => Map(await client.Payouts.GetAsync(
            payoutId,
            new RequestOptions { CancellationToken = cancellationToken }));

    private static PayOSPayoutSnapshot Map(Payout payout)
    {
        var transaction = payout.Transactions.SingleOrDefault();
        var succeeded = payout.ApprovalState == PayoutApprovalState.Completed
            && transaction?.State == PayoutTransactionState.Succeeded;
        var failed = payout.ApprovalState is PayoutApprovalState.Rejected
                or PayoutApprovalState.Cancelled
                or PayoutApprovalState.Failed
            || transaction?.State is PayoutTransactionState.Cancelled
                or PayoutTransactionState.Reversed
                or PayoutTransactionState.Failed;

        return new PayOSPayoutSnapshot(
            payout.Id,
            payout.ReferenceId,
            transaction?.Amount ?? 0L,
            succeeded
                ? PayOSPayoutOutcome.Succeeded
                : failed ? PayOSPayoutOutcome.Failed : PayOSPayoutOutcome.Processing);
    }
}

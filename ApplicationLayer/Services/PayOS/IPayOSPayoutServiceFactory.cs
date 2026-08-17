using ApplicationLayer.Services.PayOutClients;

namespace ApplicationLayer.Services.PayOS;

public interface IPayOSPayoutServiceFactory
{
    Task<IPayOSPayoutService> ForBoothAsync(Guid boothId, CancellationToken cancellationToken = default);
}

public sealed class PayOSPayoutServiceFactory(IPayOSPayoutClientFactory clients) : IPayOSPayoutServiceFactory
{
    public async Task<IPayOSPayoutService> ForBoothAsync(
        Guid boothId,
        CancellationToken cancellationToken = default)
        => new PayOSPayoutService(await clients.CreateClientAsync(boothId, cancellationToken));
}

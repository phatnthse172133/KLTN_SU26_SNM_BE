using DomainLayer.InterfaceRepository;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.PayOS
{
    public enum PayOSOrderSource
    {
        Order = 1,
        BoothSubscription = 2,
        MarketSubscription = 3
    }

    public interface IPayOSOrderCodeGenerator
    {
        Task<long> GenerateAsync(PayOSOrderSource source);
        PayOSOrderSource? GetSource(long orderCode);
    }

    public class PayOSOrderCodeGenerator : IPayOSOrderCodeGenerator
    {
        private readonly ISequenceRepository _sequenceRepo;

        public PayOSOrderCodeGenerator(ISequenceRepository sequenceRepo)
        {
            _sequenceRepo = sequenceRepo;
        }

        public async Task<long> GenerateAsync(PayOSOrderSource source)
        {
            var prefix = (int)source;
            var seqValue = await _sequenceRepo.NextPayOSOrderCodeAsync();
            return checked((long)prefix * 100_000_000_000_000L + seqValue);
        }

        public PayOSOrderSource? GetSource(long orderCode)
        {
            var prefix = orderCode / 100_000_000_000_000L;
            return prefix switch
            {
                1 => PayOSOrderSource.Order,
                2 => PayOSOrderSource.BoothSubscription,
                3 => PayOSOrderSource.MarketSubscription,
                _ => null
            };
        }
    }
}

using PayOS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.PayOutClients
{
    public interface IPayOSPayoutClientFactory
    {
        Task<PayOSClient> CreateClientAsync(Guid boothId, CancellationToken cancellationToken = default);
    }
}

using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.EncryptionServices;
using DomainLayer.InterfaceRepositories;
using PayOS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.PayOutClients
{
    public class PayOSPayoutClientFactory : IPayOSPayoutClientFactory
    {
        private readonly IBoothPayOsCredentialRepository _credentialRepository;
        private readonly IEncryptionService _encryptionService;

        public PayOSPayoutClientFactory(
            IBoothPayOsCredentialRepository credentialRepository,
            IEncryptionService encryptionService)
        {
            _credentialRepository = credentialRepository;
            _encryptionService = encryptionService;
        }

        public async Task<PayOSClient> CreateClientAsync(Guid boothId, CancellationToken cancellationToken = default)
        {
            var credential = await _credentialRepository.GetByBoothIdAsync(boothId, cancellationToken);

            if (string.IsNullOrWhiteSpace(credential?.EncryptedPayoutClientId)
                || string.IsNullOrWhiteSpace(credential?.EncryptedPayoutApiKey)
                || string.IsNullOrWhiteSpace(credential?.EncryptedPayoutChecksumKey))
            {
                throw AppException.ServiceUnavailable(
                    $"Payout credentials for booth {boothId} are not configured.",
                    "PAYOUT_CREDENTIALS_NOT_CONFIGURED");
            }

            var clientId = _encryptionService.Decrypt(credential.EncryptedPayoutClientId.Trim());
            var apiKey = _encryptionService.Decrypt(credential.EncryptedPayoutApiKey.Trim());
            var checksumKey = _encryptionService.Decrypt(credential.EncryptedPayoutChecksumKey.Trim());

            return new PayOSClient(clientId, apiKey, checksumKey);
        }
    }
}

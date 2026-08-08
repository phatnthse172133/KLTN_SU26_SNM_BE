using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainLayer.Entities
{
    public class BoothPayOsCredential
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid BoothId { get; set; }

        //Payment credentials for BoothPayOs
        public string EncryptedClientId { get; set; } = string.Empty;
        public string EncryptedApiKey { get; set; } = string.Empty;
        public string EncryptedChecksumKey { get; set; } = string.Empty;

        //Payout credentials for BoothPayOs
        public string? EncryptedPayoutClientId { get; set; }
        public string? EncryptedPayoutApiKey { get; set; }
        public string? EncryptedPayoutChecksumKey { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}

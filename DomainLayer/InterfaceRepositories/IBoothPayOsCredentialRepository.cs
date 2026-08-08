using DomainLayer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainLayer.InterfaceRepositories
{
    public interface IBoothPayOsCredentialRepository
    {
        Task<BoothPayOsCredential?> GetByBoothIdAsync(Guid boothId, CancellationToken cancellationToken = default);
        Task AddAsync(BoothPayOsCredential credential, CancellationToken cancellationToken = default);
        Task UpdateAsync(BoothPayOsCredential credential, CancellationToken cancellationToken = default);
    }
}

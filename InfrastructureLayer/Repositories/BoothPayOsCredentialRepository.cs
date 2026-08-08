using DomainLayer.Entities;
using DomainLayer.InterfaceRepositories;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfrastructureLayer.Repositories
{
    public class BoothPayOsCredentialRepository : GenericRepository<BoothPayOsCredential>, IBoothPayOsCredentialRepository
    {
        public BoothPayOsCredentialRepository(SNMDbContext context) : base(context)
        {
        }

        public async Task<BoothPayOsCredential?> GetByBoothIdAsync(Guid boothId, CancellationToken cancellationToken = default)
        {
            return await _context.BoothPayOsCredentials
                .FirstOrDefaultAsync(x => x.BoothId == boothId, cancellationToken);
        }

        public async Task AddAsync(BoothPayOsCredential credential, CancellationToken cancellationToken = default)
        {
            await _context.BoothPayOsCredentials.AddAsync(credential, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(BoothPayOsCredential credential, CancellationToken cancellationToken = default)
        {
            _context.BoothPayOsCredentials.Update(credential);
            await _context.SaveChangesAsync(cancellationToken);
        }


    }
}

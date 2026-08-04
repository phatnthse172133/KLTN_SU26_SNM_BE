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
    public class PaymentMethodRepository : GenericRepository<PaymentMethod>, IPaymentMethodRepository
    {
        public PaymentMethodRepository(SNMDbContext context) : base(context)
        {
        }

        public async Task<PaymentMethod?> GetDefaultMethodByUserIdAsync(Guid userId)
        => await _dbSet.FirstOrDefaultAsync(pm => pm.UserId == userId && pm.IsDefault);

    }
}

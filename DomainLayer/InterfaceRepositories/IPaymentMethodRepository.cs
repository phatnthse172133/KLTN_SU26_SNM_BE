using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainLayer.InterfaceRepositories
{
    public interface IPaymentMethodRepository : IGenericRepository<PaymentMethod>
    {
        Task<PaymentMethod?> GetDefaultMethodByUserIdAsync(Guid userId);
    }
}

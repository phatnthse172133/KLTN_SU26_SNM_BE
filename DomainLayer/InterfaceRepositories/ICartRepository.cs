using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface ICartRepository : IGenericRepository<Cart>
{
    Task<Cart?> GetActiveByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}

using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class CartRepository : GenericRepository<Cart>, ICartRepository
{
    public CartRepository(SNMDbContext context) : base(context)
    {
    }

    public Task<Cart?> GetActiveByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
        => ActiveQuery()
            .FirstOrDefaultAsync(cart => cart.CustomerId == customerId, cancellationToken);
}

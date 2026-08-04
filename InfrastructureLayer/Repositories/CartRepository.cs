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

    public Task AcquireCustomerMutationLockAsync(Guid customerId, CancellationToken cancellationToken = default)
        => _context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({'c' + customerId.ToString()}, 0))",
                cancellationToken)
            : Task.CompletedTask;
}

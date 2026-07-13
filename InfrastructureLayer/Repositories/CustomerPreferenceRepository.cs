using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class CustomerPreferenceRepository : GenericRepository<CustomerPreference>, ICustomerPreferenceRepository
{
    public CustomerPreferenceRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyCollection<CustomerPreference>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
        => await _dbSet
            .Include(preference => preference.FoodTag)
            .Where(preference => preference.CustomerId == customerId)
            .OrderBy(preference => preference.PreferenceKind)
            .ThenBy(preference => preference.FoodTag.Name)
            .ToListAsync(cancellationToken);
}

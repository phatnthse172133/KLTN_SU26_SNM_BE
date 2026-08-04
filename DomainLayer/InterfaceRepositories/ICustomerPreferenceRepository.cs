using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface ICustomerPreferenceRepository : IGenericRepository<CustomerPreference>
{
    Task<IReadOnlyCollection<CustomerPreference>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}

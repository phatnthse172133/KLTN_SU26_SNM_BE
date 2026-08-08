using ApplicationLayer.AI.DTOs;
using DomainLayer.Entities;

namespace ApplicationLayer.AI.Services;

public interface ILegacyCustomerPreferenceAdapter
{
    Task<CustomerPreferenceResponse?> GetDerivedAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task AddSupportedAsync(Guid customerId, IReadOnlyCollection<CustomerPreference> preferences, CancellationToken cancellationToken = default);
}

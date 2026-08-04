using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface ICartItemRepository : IGenericRepository<CartItem>
{
    Task<CartItem?> GetActiveByCartAndFoodAsync(
        Guid cartId,
        Guid foodItemId,
        CancellationToken cancellationToken = default);

    Task<CartItem?> GetOwnedActiveByIdAsync(
        Guid customerId,
        Guid cartItemId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CartItem>> GetActiveByCartAsync(
        Guid cartId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CartItem>> GetActiveByCartAndBoothAsync(
        Guid cartId,
        Guid boothId,
        CancellationToken cancellationToken = default);
}

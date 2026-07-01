using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class CartItemRepository : GenericRepository<CartItem>, ICartItemRepository
{
    public CartItemRepository(SNMDbContext context) : base(context)
    {
    }

    public Task<CartItem?> GetActiveByCartAndFoodAsync(
        Guid cartId,
        Guid foodItemId,
        CancellationToken cancellationToken = default)
        => ActiveQuery().FirstOrDefaultAsync(
            item => item.CartId == cartId && item.FoodItemId == foodItemId,
            cancellationToken);

    public Task<CartItem?> GetOwnedActiveByIdAsync(
        Guid customerId,
        Guid cartItemId,
        CancellationToken cancellationToken = default)
        => ActiveQuery()
            .Include(item => item.Cart)
            .Include(item => item.FoodItem)
                .ThenInclude(food => food.Booth)
            .Include(item => item.FoodItem)
                .ThenInclude(food => food.Category)
            .Include(item => item.FoodItem)
                .ThenInclude(food => food.FoodPrices)
            .FirstOrDefaultAsync(
                item => item.Id == cartItemId
                    && item.Cart.CustomerId == customerId
                    && !item.Cart.IsDeleted,
                cancellationToken);

    public async Task<IReadOnlyCollection<CartItem>> GetActiveByCartAsync(
        Guid cartId,
        CancellationToken cancellationToken = default)
        => await CartItemDetailsQuery()
            .Where(item => item.CartId == cartId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<CartItem>> GetActiveByCartAndBoothAsync(
        Guid cartId,
        Guid boothId,
        CancellationToken cancellationToken = default)
        => await CartItemDetailsQuery()
            .Where(item => item.CartId == cartId && item.FoodItem.BoothId == boothId)
            .ToListAsync(cancellationToken);

    private IQueryable<CartItem> CartItemDetailsQuery()
        => ActiveQuery()
            .Include(item => item.FoodItem)
                .ThenInclude(food => food.Booth)
            .Include(item => item.FoodItem)
                .ThenInclude(food => food.Category)
            .Include(item => item.FoodItem)
                .ThenInclude(food => food.FoodPrices)
            .AsSplitQuery();
}

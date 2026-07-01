using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Carts;

public class CartService : ICartService
{
    private readonly ICartRepository _carts;
    private readonly ICartItemRepository _cartItems;
    private readonly IFoodItemRepository _foodItems;
    private readonly IMapper _mapper;

    public CartService(
        ICartRepository carts,
        ICartItemRepository cartItems,
        IFoodItemRepository foodItems,
        IMapper mapper)
    {
        _carts = carts;
        _cartItems = cartItems;
        _foodItems = foodItems;
        _mapper = mapper;
    }

    public async Task<ApiResponse<CartResponse>> GetCurrentAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var cart = await GetCurrentCartAsync(customerId, cancellationToken);
        var items = await _cartItems.GetActiveByCartAsync(cart.Id, cancellationToken);
        var boothGroups = items
            .GroupBy(item => new
            {
                item.FoodItem.BoothId,
                item.FoodItem.Booth.BoothName
            })
            .OrderBy(group => group.Key.BoothName)
            .ThenBy(group => group.Key.BoothId)
            .ToList();

        var pagedBooths = boothGroups
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(group => MapBooth(group.Key.BoothId, group.Key.BoothName, group))
            .ToList();

        var response = new CartResponse
        {
            CartId = cart.Id,
            TotalItemCount = items.Sum(item => (long)item.Quantity),
            TotalAmount = items.Sum(item => GetCurrentPrice(item.FoodItem) * item.Quantity),
            Booths = PaginationResp<CartBoothResponse>.Create(
                pagedBooths,
                boothGroups.Count,
                pagination)
        };

        return ApiResponse<CartResponse>.SuccessResponse(response);
    }

    public async Task<ApiResponse<CartItemResponse>> AddItemAsync(Guid customerId, AddCartItemRequest request,CancellationToken cancellationToken = default)
    {
        ValidateQuantity(request.Quantity);
        if (request.FoodItemId == Guid.Empty)
            throw AppException.BadRequest("Food item id is required.", "FOOD_ITEM_ID_REQUIRED");

        var foodItem = await _foodItems.GetForCartAsync(request.FoodItemId, cancellationToken);
        if (foodItem is null)
            throw AppException.NotFound("Food item was not found.", "FOOD_ITEM_NOT_FOUND");
        EnsureAvailable(foodItem);

        var cart = await _carts.GetActiveByCustomerAsync(customerId, cancellationToken)
            ?? await CreateCartAsync(customerId);
        var cartItem = await _cartItems.GetActiveByCartAndFoodAsync(
            cart.Id,
            foodItem.Id,
            cancellationToken);
        var now = DateTime.UtcNow;

        if (cartItem is null)
        {
            cartItem = new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = cart.Id,
                FoodItemId = foodItem.Id,
                Quantity = request.Quantity,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now,
                FoodItem = foodItem
            };
            await _cartItems.AddAsync(cartItem);
        }
        else
        {
            if (cartItem.Quantity > int.MaxValue - request.Quantity)
                throw AppException.BadRequest("Cart item quantity is too large.", "INVALID_QUANTITY");

            cartItem.Quantity += request.Quantity;
            cartItem.UpdatedAt = now;
            cartItem.FoodItem = foodItem;
        }

        cart.UpdatedAt = now;
        await _cartItems.SaveChangesAsync();

        return ApiResponse<CartItemResponse>.SuccessResponse(
            MapItem(cartItem),
            "Item added to cart successfully.");
    }

    public async Task<ApiResponse<CartItemResponse>> UpdateQuantityAsync(Guid customerId, Guid cartItemId, UpdateCartItemQuantityRequest request, CancellationToken cancellationToken = default)
    {
        ValidateQuantity(request.Quantity);
        var item = await _cartItems.GetOwnedActiveByIdAsync(
            customerId,
            cartItemId,
            cancellationToken);
        if (item is null)
            throw AppException.NotFound("Cart item was not found.", "CART_ITEM_NOT_FOUND");

        item.Quantity = request.Quantity;
        item.UpdatedAt = DateTime.UtcNow;
        item.Cart.UpdatedAt = item.UpdatedAt;
        await _cartItems.SaveChangesAsync();

        return ApiResponse<CartItemResponse>.SuccessResponse(
            MapItem(item),
            "Cart item quantity updated successfully.");
    }

    public async Task<ApiResponse<object>> RemoveItemAsync(
        Guid customerId,
        Guid cartItemId,
        CancellationToken cancellationToken = default)
    {
        var item = await _cartItems.GetOwnedActiveByIdAsync(
            customerId,
            cartItemId,
            cancellationToken);
        if (item is null)
            throw AppException.NotFound("Cart item was not found.", "CART_ITEM_NOT_FOUND");

        item.UpdatedAt = DateTime.UtcNow;
        item.Cart.UpdatedAt = item.UpdatedAt;
        item.IsDeleted = true;
        await _cartItems.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(
            new { item.Id },
            "Cart item removed successfully.");
    }

    public async Task<ApiResponse<object>> RemoveBoothItemsAsync( Guid customerId, Guid boothId, CancellationToken cancellationToken = default)
    {
        var cart = await GetCurrentCartAsync(customerId, cancellationToken);
        var items = await _cartItems.GetActiveByCartAndBoothAsync(
            cart.Id,
            boothId,
            cancellationToken);
        if (items.Count == 0)
            throw AppException.NotFound(
                "The cart does not contain items from this booth.",
                "CART_BOOTH_ITEMS_NOT_FOUND");

        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.UpdatedAt = now;
            item.IsDeleted = true;
        }
        cart.UpdatedAt = now;
        await _cartItems.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(
            new { BoothId = boothId, RemovedItemCount = items.Count },
            "Booth items removed from cart successfully.");
    }

    public async Task<ApiResponse<object>> ClearAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var cart = await GetCurrentCartAsync(customerId, cancellationToken);
        var items = await _cartItems.GetActiveByCartAsync(cart.Id, cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
            item.UpdatedAt = now;
            item.IsDeleted = true;
        }

        cart.UpdatedAt = now;
        cart.IsDeleted = true;
        await _carts.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(
            new { cart.Id, RemovedItemCount = items.Count },
            "Cart cleared successfully.");
    }

    private async Task<Cart> GetCurrentCartAsync(Guid customerId, CancellationToken cancellationToken)
        => await _carts.GetActiveByCustomerAsync(customerId, cancellationToken)
            ?? throw AppException.NotFound("Cart was not found.", "CART_NOT_FOUND");

    private async Task<Cart> CreateCartAsync(Guid customerId)
    {
        var now = DateTime.UtcNow;
        var cart = new Cart
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _carts.AddAsync(cart);
        return cart;
    }

    private CartBoothResponse MapBooth(Guid boothId, string boothName, IEnumerable<CartItem> boothItems)
    {
        var items = boothItems.ToList();
        return new CartBoothResponse
        {
            BoothId = boothId,
            BoothName = boothName,
            Subtotal = items.Sum(item => GetCurrentPrice(item.FoodItem) * item.Quantity),
            Categories = items
                .GroupBy(item => new
                {
                    item.FoodItem.CategoryId,
                    item.FoodItem.Category.Name
                })
                .OrderBy(group => group.Key.Name)
                .ThenBy(group => group.Key.CategoryId)
                .Select(group => new CartCategoryResponse
                {
                    CategoryId = group.Key.CategoryId,
                    CategoryName = group.Key.Name,
                    Items = group
                        .OrderBy(item => item.FoodItem.Name)
                        .ThenBy(item => item.Id)
                        .Select(MapItem)
                        .ToList()
                })
                .ToList()
        };
    }

    private CartItemResponse MapItem(CartItem item)
    {
        var response = _mapper.Map<CartItemResponse>(item);
        response.CurrentUnitPrice = GetCurrentPrice(item.FoodItem);
        response.LineTotal = response.CurrentUnitPrice * item.Quantity;
        return response;
    }

    private static decimal GetCurrentPrice(FoodItem foodItem)
    {
        var now = DateTime.UtcNow;
        return foodItem.FoodPrices
            .Where(price => !price.IsDeleted
                && (!price.StartDate.HasValue || price.StartDate.Value <= now)
                && (!price.EndDate.HasValue || price.EndDate.Value >= now))
            .OrderByDescending(price => price.StartDate)
            .ThenByDescending(price => price.CreatedAt)
            .Select(price => (decimal?)price.Price)
            .FirstOrDefault() ?? foodItem.Price;
    }

    private static void EnsureAvailable(FoodItem foodItem)
    {
        if (!foodItem.IsAvailable
            || foodItem.Category.IsDeleted
            || foodItem.Booth.Status != BoothStatus.Active)
        {
            throw AppException.Conflict(
                "Food item is currently unavailable.",
                "FOOD_ITEM_UNAVAILABLE");
        }
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
            throw AppException.BadRequest(
                "Quantity must be greater than zero.",
                "INVALID_QUANTITY");
    }
}

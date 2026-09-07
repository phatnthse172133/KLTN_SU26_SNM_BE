using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.Common;
using DomainLayer.InterfaceRepository;
using ApplicationLayer.Services.Booths;
using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.NightMarkets;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Carts;

public class CartService : ICartService
{
    private readonly ICartRepository _carts;
    private readonly ICartItemRepository _cartItems;
    private readonly IFoodItemRepository _foodItems;
    private readonly IMapper _mapper;
    private readonly TimeProvider _timeProvider;

    public CartService(
        ICartRepository carts,
        ICartItemRepository cartItems,
        IFoodItemRepository foodItems,
        IMapper mapper,
        TimeProvider timeProvider)
    {
        _carts = carts;
        _cartItems = cartItems;
        _foodItems = foodItems;
        _mapper = mapper;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<CartResponse>> GetCurrentAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var cart = await _carts.GetActiveByCustomerAsync(customerId, cancellationToken)
            ?? await CreateCartAsync(customerId);
        var items = await _cartItems.GetActiveByCartAsync(cart.Id, cancellationToken);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var boothGroups = items
            .GroupBy(item => item.FoodItem.Booth)
            .OrderBy(group => group.Key.BoothName)
            .ThenBy(group => group.Key.Id)
            .ToList();

        var pagedBooths = boothGroups
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(group => MapBooth(group.Key, group, utcNow))
            .ToList();

        var response = new CartResponse
        {
            CartId = cart.Id,
            TotalItemCount = items.Sum(item => (long)item.Quantity),
            TotalAmount = items.Sum(item => GetCurrentPrice(item.FoodItem, utcNow) * item.Quantity),
            // Checkout is performed per booth. A closed booth keeps its cart items, but
            // must not prevent checkout for another booth that is currently open.
            CanCheckout = items.Count > 0
                && items.Any(item => CustomerOrderability.Evaluate(item.FoodItem, utcNow).CanOrder),
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
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        EnsureOrderable(foodItem, utcNow);

        var cart = await _carts.GetActiveByCustomerAsync(customerId, cancellationToken)
            ?? await CreateCartAsync(customerId);
        var cartItem = await _cartItems.GetActiveByCartAndFoodAsync(
            cart.Id,
            foodItem.Id,
            cancellationToken);
        var now = utcNow;

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
            MapItem(cartItem, utcNow),
            "Item added to cart successfully.");
    }

    public async Task<ApiResponse<CartBatchAddResponse>> AddItemsAsync(
        Guid customerId,
        AddCartItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
            throw AppException.BadRequest("At least one cart item is required.", "CART_ITEMS_REQUIRED");

        var normalized = request.Items
            .GroupBy(item => item.FoodItemId)
            .Select(group => new AddCartItemRequest
            {
                FoodItemId = group.Key,
                Quantity = group.Aggregate(0, (total, item) => checked(total + item.Quantity))
            })
            .ToArray();

        foreach (var item in normalized)
        {
            ValidateQuantity(item.Quantity);
            if (item.FoodItemId == Guid.Empty)
                throw AppException.BadRequest("Food item id is required.", "FOOD_ITEM_ID_REQUIRED");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var foods = new Dictionary<Guid, FoodItem>();
        foreach (var item in normalized)
        {
            var food = await _foodItems.GetForCartAsync(item.FoodItemId, cancellationToken)
                ?? throw AppException.NotFound("Food item was not found.", "FOOD_ITEM_NOT_FOUND");
            EnsureOrderable(food, now);
            foods.Add(food.Id, food);
        }

        await _carts.AcquireCustomerMutationLockAsync(customerId, cancellationToken);
        var cart = await _carts.GetActiveByCustomerAsync(customerId, cancellationToken);
        if (cart is null)
        {
            cart = NewCart(customerId, now);
            await _carts.AddAsync(cart);
        }

        var added = new List<Guid>();
        var merged = new List<Guid>();
        foreach (var item in normalized)
        {
            var existing = await _cartItems.GetActiveByCartAndFoodAsync(cart.Id, item.FoodItemId, cancellationToken);
            if (existing is null)
            {
                await _cartItems.AddAsync(new CartItem
                {
                    Id = Guid.NewGuid(), CartId = cart.Id, FoodItemId = item.FoodItemId,
                    Quantity = item.Quantity, IsDeleted = false, CreatedAt = now, UpdatedAt = now,
                    FoodItem = foods[item.FoodItemId]
                });
                added.Add(item.FoodItemId);
            }
            else
            {
                if (existing.Quantity > int.MaxValue - item.Quantity)
                    throw AppException.BadRequest("Cart item quantity is too large.", "INVALID_QUANTITY");
                existing.Quantity += item.Quantity;
                existing.UpdatedAt = now;
                merged.Add(item.FoodItemId);
            }
        }

        cart.UpdatedAt = now;
        await _cartItems.SaveChangesAsync();
        var current = await GetCurrentAsync(customerId, new PaginationReq { Page = 1, PageSize = 100 }, cancellationToken);
        return ApiResponse<CartBatchAddResponse>.SuccessResponse(new CartBatchAddResponse
        {
            Cart = current.Data!, AddedFoodItemIds = added, MergedFoodItemIds = merged
        }, "Items added to cart successfully.");
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

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (request.Quantity > item.Quantity)
            EnsureOrderable(item.FoodItem, utcNow);

        item.Quantity = request.Quantity;
        item.UpdatedAt = utcNow;
        item.Cart.UpdatedAt = item.UpdatedAt;
        await _cartItems.SaveChangesAsync();

        return ApiResponse<CartItemResponse>.SuccessResponse(
            MapItem(item, utcNow),
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

        item.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
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

        var now = _timeProvider.GetUtcNow().UtcDateTime;
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
        var now = _timeProvider.GetUtcNow().UtcDateTime;

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
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var cart = NewCart(customerId, now);
        await _carts.AddAsync(cart);
        await _carts.SaveChangesAsync();
        return cart;
    }

    private static Cart NewCart(Guid customerId, DateTime now) => new()
    {
        Id = Guid.NewGuid(), CustomerId = customerId, IsDeleted = false,
        CreatedAt = now, UpdatedAt = now
    };

    private CartBoothResponse MapBooth(
        Booth booth,
        IEnumerable<CartItem> boothItems,
        DateTime utcNow)
    {
        var items = boothItems.ToList();
        var openState = EvaluateCartBoothOpenState(booth, utcNow);
        return new CartBoothResponse
        {
            BoothId = booth.Id,
            BoothName = booth.BoothName,
            IsOpen = openState.IsOpen,
            NextOpenAt = openState.NextOpenAt,
            CloseReason = openState.CloseReason,
            Subtotal = items.Sum(item => GetCurrentPrice(item.FoodItem, utcNow) * item.Quantity),
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
                        .Select(item => MapItem(item, utcNow))
                        .ToList()
                })
                .ToList()
        };
    }

    /// <summary>
    /// Cart booth open flag must match place-order eligibility hours:
    /// market operational ∩ market schedule ∩ booth schedule (null booth hours = inherit market).
    /// </summary>
    public static (bool IsOpen, string? NextOpenAt, string? CloseReason) EvaluateCartBoothOpenState(
        Booth booth,
        DateTime utcNow)
    {
        if (booth.Status != BoothStatus.Active)
        {
            return (false, null, booth.Status == BoothStatus.Banned ? "Banned" : "Paused");
        }

        var market = booth.NightMarket;
        var marketOperational = market is not null
            && !market.IsDeleted
            && market.ModerationStatus == ModerationStatus.Active
            && market.Status == NightMarketStatus.Active;

        var localTime = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(utcNow));
        var isOpen = CustomerAvailability.IsOpenNow(
            marketOperational,
            market?.OpeningHours,
            market?.ClosingHours,
            booth.OpenTime,
            booth.CloseTime,
            localTime);

        if (isOpen)
            return (true, null, null);

        if (!marketOperational)
            return (false, FormatClock(market?.OpeningHours), "Market unavailable");

        if (!CustomerAvailability.IsWithinInterval(market?.OpeningHours, market?.ClosingHours, localTime))
            return (false, FormatClock(market?.OpeningHours), "Market closed");

        if (booth.OpenTime.HasValue != booth.CloseTime.HasValue)
            return (false, null, "Operating hours not set");

        if (booth.OpenTime is { } boothOpen && booth.CloseTime is { } boothClose
            && !CustomerAvailability.IsWithinInterval(boothOpen, boothClose, localTime))
        {
            var next = BoothOperatingHoursEvaluator.ComputeNextOpenAt(boothOpen, boothClose, localTime);
            return (false, BoothOperatingHoursEvaluator.FormatNextOpenAt(next), "Closed");
        }

        return (false, FormatClock(market?.OpeningHours), "Closed");
    }

    private static string? FormatClock(TimeOnly? time)
        => time.HasValue ? time.Value.ToString("HH:mm") : null;

    private CartItemResponse MapItem(CartItem item, DateTime utcNow)
    {
        var response = _mapper.Map<CartItemResponse>(item);
        var orderability = CustomerOrderability.Evaluate(item.FoodItem, utcNow);
        response.CurrentUnitPrice = GetCurrentPrice(item.FoodItem, utcNow);
        response.LineTotal = response.CurrentUnitPrice * item.Quantity;
        response.CanOrder = orderability.CanOrder;
        response.ReasonCode = orderability.ReasonCode;
        return response;
    }

    private static decimal GetCurrentPrice(FoodItem foodItem, DateTime utcNow)
        => FoodPriceResolver.GetCurrentPrice(foodItem, utcNow);

    private static void EnsureOrderable(FoodItem foodItem, DateTime utcNow)
    {
        var result = CustomerOrderability.EvaluateForCartAdd(foodItem, utcNow);
        if (!result.CanOrder)
            throw AppException.Conflict(
                CustomerOrderability.GetPublicMessage(result.ReasonCode!),
                result.ReasonCode!);
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
            throw AppException.BadRequest(
                "Quantity must be greater than zero.",
                "INVALID_QUANTITY");
    }
}

using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.CustomerDiscovery;
using DomainLayer.InterfaceRepository;
using DomainLayer.Common;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Orders;

public sealed class CustomerCheckoutService : ICustomerCheckoutService
{
    private readonly ICartRepository _carts;
    private readonly ICartItemRepository _cartItems;
    private readonly IBoothRepository _booths;
    private readonly IOrderService _orders;
    private readonly IOrderRepository _orderRepository;

    public CustomerCheckoutService(ICartRepository carts, ICartItemRepository cartItems, IBoothRepository booths, IOrderService orders, IOrderRepository orderRepository)
        => (_carts, _cartItems, _booths, _orders, _orderRepository) = (carts, cartItems, booths, orders, orderRepository);

    public async Task<ApiResponse<OrderResponseDto>> CheckoutBoothAsync(
        Guid customerId,
        CheckoutCartBoothRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CheckoutRequestId == Guid.Empty)
            throw AppException.BadRequest("CheckoutRequestId is required.", "CHECKOUT_REQUEST_ID_REQUIRED");
        if (request.BoothId == Guid.Empty)
            throw AppException.BadRequest("Booth id is required.", "BOOTH_ID_REQUIRED");
        if (request.PaymentMethod is not PaymentType.PayOS and not PaymentType.Cash)
            throw AppException.BadRequest("Payment method is invalid.", "PAYMENT_METHOD_INVALID");

        var existing = await _orderRepository.GetByCheckoutRequestAsync(customerId, request.CheckoutRequestId);
        if (existing is not null)
        {
            var existingPayment = existing.Payments.OrderByDescending(payment => payment.CreatedAt).FirstOrDefault();
            return ApiResponse<OrderResponseDto>.SuccessResponse(new()
            {
                OrderId = existing.Id,
                OrderCode = existing.OrderCode,
                Status = existing.Status,
                PaymentUrl = existingPayment?.CheckoutUrl
            }, "The existing idempotent checkout result was returned.");
        }

        var cart = await _carts.GetActiveByCustomerAsync(customerId, cancellationToken)
            ?? throw AppException.NotFound("Cart was not found.", "CART_NOT_FOUND");
        var items = await _cartItems.GetActiveByCartAndBoothAsync(cart.Id, request.BoothId, cancellationToken);
        if (items.Count == 0)
            throw AppException.BadRequest("The cart does not contain items from this booth.", "CART_BOOTH_EMPTY");
        if (items.Any(item => item.Quantity <= 0 || item.FoodItem is null))
            throw AppException.Conflict("The cart contains invalid items. Refresh the cart.", "CART_CHANGED");

        var booth = await _booths.GetByIdAsync(request.BoothId)
            ?? throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");
        var now = DateTime.UtcNow;
        var dto = new CreateOrderDto
        {
            CheckoutRequestId = request.CheckoutRequestId,
            CustomerId = customerId,
            BoothId = booth.Id,
            BoothOwnerId = booth.BoothOwnerId,
            IsCreatedByBooth = false,
            PaymentMethod = request.PaymentMethod,
            PromotionCode = string.IsNullOrWhiteSpace(request.PromotionCode) ? null : request.PromotionCode.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Items = items.Select(item => new CartItemDto
            {
                FoodItemId = item.FoodItemId,
                Quantity = item.Quantity,
                UnitPrice = FoodPriceResolver.GetCurrentPrice(item.FoodItem, now)
            }).ToList()
        };
        var response = await _orders.CreateOrderAsync(dto);
        var updatedAt = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.IsDeleted = true;
            item.UpdatedAt = updatedAt;
        }
        cart.UpdatedAt = updatedAt;
        await _cartItems.SaveChangesAsync();
        return response;
    }
}

using System.Security.Cryptography;
using System.Text;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.Promotions;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Orders;

public sealed class CustomerCheckoutService : ICustomerCheckoutService
{
    private readonly ICartRepository _carts;
    private readonly ICartItemRepository _cartItems;
    private readonly IBoothRepository _booths;
    private readonly IOrderService _orders;
    private readonly IOrderRepository _orderRepository;
    private readonly IPromotionRepository _promotions;
    private readonly IPromotionValidationService _promotionValidation;

    public CustomerCheckoutService(ICartRepository carts, ICartItemRepository cartItems, IBoothRepository booths,
        IOrderService orders, IOrderRepository orderRepository, IPromotionRepository promotions,
        IPromotionValidationService promotionValidation)
        => (_carts, _cartItems, _booths, _orders, _orderRepository, _promotions, _promotionValidation)
            = (carts, cartItems, booths, orders, orderRepository, promotions, promotionValidation);

    [Obsolete("Test-only compatibility constructor. Production DI uses the complete constructor.")]
    public CustomerCheckoutService(ICartRepository carts, ICartItemRepository cartItems, IBoothRepository booths,
        IOrderService orders, IOrderRepository orderRepository)
        => (_carts, _cartItems, _booths, _orders, _orderRepository, _promotions, _promotionValidation)
            = (carts, cartItems, booths, orders, orderRepository, null!, null!);

    public async Task<CheckoutPreviewResponse> GetPreviewAsync(Guid customerId, Guid boothId, Guid? promotionId = null,
        CancellationToken cancellationToken = default)
    {
        var (cart, items) = await LoadOrderableCartAsync(customerId, boothId, cancellationToken);
        var now = DateTime.UtcNow;
        var groups = items.GroupBy(item => item.FoodItem.Booth).ToList();
        var subtotal = items.Sum(item => FoodPriceResolver.GetCurrentPrice(item.FoodItem, now) * item.Quantity);
        var eligible = new List<EligiblePromotionResponse>();

        foreach (var promotion in await _promotions.GetAvailableAsync(groups.Select(group => group.Key.Id).ToArray(), now, cancellationToken))
        {
            var groupItems = groups.First(group => group.Key.Id == promotion.BoothId).ToArray();
            try
            {
                var validation = await _promotionValidation.ValidateAsync(customerId, promotion, groupItems, cancellationToken);
                eligible.Add(new EligiblePromotionResponse
                {
                    PromotionId = promotion.Id,
                    Code = promotion.PromotionCode ?? string.Empty,
                    Title = promotion.Title,
                    DiscountAmount = validation.DiscountAmount
                });
            }
            catch (AppException) { }
        }

        var selectedDiscount = promotionId.HasValue
            ? eligible.FirstOrDefault(item => item.PromotionId == promotionId.Value)?.DiscountAmount
                ?? throw AppException.UnprocessableEntity("Promotion is not eligible for this cart.", "PROMOTION_INVALID")
            : 0m;

        return new CheckoutPreviewResponse
        {
            CartId = cart.Id,
            BoothGroups = groups.Select(group => new CheckoutBoothGroupResponse
            {
                BoothId = group.Key.Id,
                BoothName = group.Key.BoothName,
                Items = group.Select(item =>
                {
                    var unitPrice = FoodPriceResolver.GetCurrentPrice(item.FoodItem, now);
                    return new CheckoutItemResponse { FoodId = item.FoodItemId, FoodName = item.FoodItem.Name,
                        Quantity = item.Quantity, UnitPrice = unitPrice, LineTotal = unitPrice * item.Quantity };
                }).ToArray(),
                Subtotal = group.Sum(item => FoodPriceResolver.GetCurrentPrice(item.FoodItem, now) * item.Quantity)
            }).ToArray(),
            Subtotal = subtotal,
            Discount = selectedDiscount,
            FinalAmount = subtotal - selectedDiscount,
            EligiblePromotions = eligible
        };
    }

    public async Task<ApiResponse<OrderResponseDto>> CreateOrderAsync(Guid customerId, CreateCustomerOrderRequest request,
        string? headerIdempotencyKey, CancellationToken cancellationToken = default)
    {
        var key = string.IsNullOrWhiteSpace(headerIdempotencyKey) ? request.IdempotencyKey : headerIdempotencyKey;
        if (!Guid.TryParse(key, out var checkoutRequestId))
            throw AppException.BadRequest("A UUID idempotency key is required.", "IDEMPOTENCY_KEY_REQUIRED");
        if (request.BoothId == Guid.Empty)
            throw AppException.BadRequest("Booth id is required.", "BOOTH_ID_REQUIRED");
        if (request.PaymentMethod is not PaymentType.PayOS and not PaymentType.Cash)
            throw AppException.BadRequest("Payment method must be CASH or PAYOS.", "PAYMENT_METHOD_INVALID");

        var requestHash = ComputeRequestHash(request);
        var existing = await _orderRepository.GetByCheckoutRequestAsync(customerId, checkoutRequestId);
        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(existing.RequestHash) && existing.RequestHash != requestHash)
                throw AppException.Conflict("The idempotency key was already used for a different request.", "ORDER_DUPLICATE_REQUEST");
            return ExistingResult(existing);
        }

        var (cart, items) = await LoadOrderableCartAsync(customerId, request.BoothId, cancellationToken);
        var booth = await _booths.GetByIdAsync(request.BoothId) ?? throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");

        Promotion? promotion = null;
        if (request.PromotionId.HasValue)
        {
            promotion = await _promotions.GetDetailsAsync(request.PromotionId.Value, cancellationToken)
                ?? throw AppException.UnprocessableEntity("Promotion is not valid.", "PROMOTION_INVALID");
            if (promotion.BoothId != booth.Id)
                throw AppException.UnprocessableEntity("Promotion is not valid for this booth.", "PROMOTION_INVALID");
        }

        var now = DateTime.UtcNow;
        var dto = new CreateOrderDto
        {
            CheckoutRequestId = checkoutRequestId, IdempotencyKey = checkoutRequestId.ToString("D"), RequestHash = requestHash,
            CheckoutCartItemIds = items.Select(item => item.Id).ToArray(),
            CustomerId = customerId, BoothId = booth.Id, BoothOwnerId = booth.BoothOwnerId, IsCreatedByBooth = false,
            PaymentMethod = request.PaymentMethod, PromotionCode = promotion?.PromotionCode,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Items = items.Select(item => new CartItemDto { FoodItemId = item.FoodItemId, Quantity = item.Quantity,
                UnitPrice = FoodPriceResolver.GetCurrentPrice(item.FoodItem, now) }).ToList()
        };

        var response = await _orders.CreateOrderAsync(dto);
        return response;
    }

    [Obsolete("Use CreateOrderAsync with the customer order contract.")]
    public async Task<ApiResponse<OrderResponseDto>> CheckoutBoothAsync(Guid customerId, CheckoutCartBoothRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CheckoutRequestId == Guid.Empty)
            throw AppException.BadRequest("CheckoutRequestId is required.", "CHECKOUT_REQUEST_ID_REQUIRED");
        var existing = await _orderRepository.GetByCheckoutRequestAsync(customerId, request.CheckoutRequestId);
        if (existing is not null)
        {
            var existingPayment = existing.Payments.OrderByDescending(item => item.CreatedAt).FirstOrDefault();
            return ApiResponse<OrderResponseDto>.SuccessResponse(new OrderResponseDto
            {
                OrderId = existing.Id, OrderCode = existing.OrderCode, Status = existing.Status,
                PaymentUrl = existingPayment?.CheckoutUrl
            });
        }
        var cart = await _carts.GetActiveByCustomerAsync(customerId, cancellationToken)
            ?? throw AppException.NotFound("Cart was not found.", "CART_NOT_FOUND");
        var items = await _cartItems.GetActiveByCartAndBoothAsync(cart.Id, request.BoothId, cancellationToken);
        if (items.Count == 0) throw AppException.BadRequest("The cart does not contain items from this booth.", "CART_BOOTH_EMPTY");
        var booth = await _booths.GetByIdAsync(request.BoothId)
            ?? throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");
        var now = DateTime.UtcNow;
        var response = await _orders.CreateOrderAsync(new CreateOrderDto
        {
            CheckoutRequestId = request.CheckoutRequestId, CustomerId = customerId, BoothId = booth.Id,
            IdempotencyKey = request.CheckoutRequestId.ToString("D"),
            CheckoutCartItemIds = items.Select(item => item.Id).ToArray(),
            BoothOwnerId = booth.BoothOwnerId, PaymentMethod = request.PaymentMethod,
            PromotionCode = string.IsNullOrWhiteSpace(request.PromotionCode) ? null : request.PromotionCode.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Items = items.Select(item => new CartItemDto { FoodItemId = item.FoodItemId, Quantity = item.Quantity,
                UnitPrice = FoodPriceResolver.GetCurrentPrice(item.FoodItem, now) }).ToList()
        });
        return response;
    }

    private async Task<(Cart Cart, IReadOnlyCollection<CartItem> Items)> LoadOrderableCartAsync(Guid customerId, Guid boothId, CancellationToken cancellationToken)
    {
        if (boothId == Guid.Empty)
            throw AppException.BadRequest("Booth id is required.", "BOOTH_ID_REQUIRED");
        var cart = await _carts.GetActiveByCustomerAsync(customerId, cancellationToken)
            ?? throw AppException.UnprocessableEntity("Cart is empty.", "CART_EMPTY");
        var items = await _cartItems.GetActiveByCartAndBoothAsync(cart.Id, boothId, cancellationToken);
        if (items.Count == 0) throw AppException.UnprocessableEntity("The cart does not contain items from this booth.", "CART_BOOTH_EMPTY");
        foreach (var item in items)
        {
            if (item.Quantity <= 0 || item.FoodItem is null) throw AppException.Conflict("Cart changed. Refresh it and try again.", "PRICE_CHANGED");
            var orderability = CustomerOrderability.Evaluate(item.FoodItem, DateTime.UtcNow);
            if (!orderability.CanOrder) throw AppException.UnprocessableEntity(CustomerOrderability.GetPublicMessage(orderability.ReasonCode!), "FOOD_UNAVAILABLE");
        }
        return (cart, items);
    }

    private static string ComputeRequestHash(CreateCustomerOrderRequest request)
    {
        var canonical = $"{request.BoothId:D}|{request.PaymentMethod}|{request.PromotionId?.ToString("D") ?? ""}|{request.Note?.Trim() ?? ""}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static ApiResponse<OrderResponseDto> ExistingResult(Order order)
    {
        var payment = order.Payments.OrderByDescending(item => item.CreatedAt).First();
        return ApiResponse<OrderResponseDto>.SuccessResponse(new OrderResponseDto { OrderId = order.Id, OrderCode = order.OrderCode,
            Status = order.Status, PaymentId = payment.Id, PaymentMethod = payment.Type, PaymentStatus = payment.Status,
            TotalAmount = order.FinalAmount, PaymentUrl = payment.CheckoutUrl, CheckoutUrl = payment.CheckoutUrl,
            QrCode = payment.QrCode, ExpiresAt = payment.ExpiresAt }, "The existing idempotent order was returned.");
    }
}
